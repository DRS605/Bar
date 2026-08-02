using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Api.Servicios;

/// <summary>Ajustes del proceso de facturación automática periódica.</summary>
public sealed class OpcionesFacturacionRecurrente
{
    public const string Seccion = "FacturacionRecurrente";

    /// <summary>Si está activo el proceso en segundo plano.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Espera inicial antes de la primera pasada tras arrancar.</summary>
    public TimeSpan RetardoInicial { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Cada cuánto se revisan las recurrencias vencidas.</summary>
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Proceso en segundo plano que emite automáticamente las facturas recurrentes vencidas de
/// <b>todas las empresas</b>. En cada pasada busca las empresas con recurrencias vencidas y, para
/// cada una, abre su propio ámbito con la empresa fijada (aislamiento multiempresa) y ejecuta el
/// caso de uso de emisión. Es tolerante a fallos: un error en una empresa no detiene al resto.
/// </summary>
public sealed class ServicioFacturacionRecurrente : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly IReloj _reloj;
    private readonly ILogger<ServicioFacturacionRecurrente> _log;
    private readonly OpcionesFacturacionRecurrente _opciones;

    public ServicioFacturacionRecurrente(
        IServiceScopeFactory ambitos,
        IReloj reloj,
        ILogger<ServicioFacturacionRecurrente> log,
        Microsoft.Extensions.Options.IOptions<OpcionesFacturacionRecurrente> opciones)
    {
        _ambitos = ambitos;
        _reloj = reloj;
        _log = log;
        _opciones = opciones.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Activo)
        {
            return;
        }

        try
        {
            await Task.Delay(_opciones.RetardoInicial, stoppingToken).ConfigureAwait(false);
            using var temporizador = new PeriodicTimer(_opciones.Intervalo);
            do
            {
                await ProcesarTodasLasEmpresasAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
    }

    private async Task ProcesarTodasLasEmpresasAsync(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);

        IReadOnlyList<Guid> empresas;
        using (var ambito = _ambitos.CreateScope())
        {
            var repositorio = ambito.ServiceProvider.GetRequiredService<IRepositorioFacturasRecurrentes>();
            empresas = await repositorio.EmpresasConVencidasAsync(hoy, ct).ConfigureAwait(false);
        }

        if (empresas.Count == 0)
        {
            return;
        }

        var totalEmitidas = 0;
        foreach (var empresaId in empresas)
        {
            try
            {
                using var ambito = _ambitos.CreateScope();
                ambito.ServiceProvider.GetRequiredService<IContextoEmpresaMutable>().Fijar(empresaId);
                var caso = ambito.ServiceProvider.GetRequiredService<EmitirFacturasRecurrentesVencidas>();
                var resultado = await caso.EjecutarAsync(empresaId, ct).ConfigureAwait(false);
                if (resultado.EsCorrecto)
                {
                    totalEmitidas += resultado.Valor.Emitidas;
                }
            }
#pragma warning disable CA1031 // Un fallo en una empresa no debe tumbar el proceso ni afectar a las demás.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError(ex, "Fallo al emitir facturas recurrentes de la empresa {EmpresaId}.", empresaId);
            }
        }

        if (totalEmitidas > 0)
        {
            _log.LogInformation("Facturación periódica: {Total} factura(s) emitida(s) en {Empresas} empresa(s).", totalEmitidas, empresas.Count);
        }
    }
}

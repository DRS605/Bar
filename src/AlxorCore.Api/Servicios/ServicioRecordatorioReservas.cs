using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Reservas.Aplicacion;

namespace AlxorCore.Api.Servicios;

/// <summary>Ajustes del proceso de recordatorios de reservas.</summary>
public sealed class OpcionesRecordatorioReservas
{
    public const string Seccion = "RecordatorioReservas";

    /// <summary>Si está activo el proceso en segundo plano.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Espera inicial antes de la primera pasada tras arrancar.</summary>
    public TimeSpan RetardoInicial { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Cada cuánto se revisan las reservas próximas.</summary>
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Proceso en segundo plano que envía los <b>recordatorios</b> de las reservas próximas de todas las
/// empresas. En cada pasada busca las empresas con reservas dentro de la ventana de aviso y, para cada
/// una, abre su propio ámbito con la empresa fijada (aislamiento multiempresa) y ejecuta el caso de
/// uso. Es tolerante a fallos: un error en una empresa no detiene al resto.
/// </summary>
public sealed class ServicioRecordatorioReservas : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly IReloj _reloj;
    private readonly ILogger<ServicioRecordatorioReservas> _log;
    private readonly OpcionesRecordatorioReservas _opciones;

    public ServicioRecordatorioReservas(
        IServiceScopeFactory ambitos,
        IReloj reloj,
        ILogger<ServicioRecordatorioReservas> log,
        Microsoft.Extensions.Options.IOptions<OpcionesRecordatorioReservas> opciones)
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
        var ahora = _reloj.AhoraUtc;
        var hasta = ahora.AddHours(EnviarRecordatoriosReservas.HorasAntes);

        IReadOnlyList<Guid> empresas;
        using (var ambito = _ambitos.CreateScope())
        {
            var repositorio = ambito.ServiceProvider.GetRequiredService<IRepositorioReservas>();
            empresas = await repositorio.EmpresasConRecordatorioAsync(ahora, hasta, ct).ConfigureAwait(false);
        }

        if (empresas.Count == 0)
        {
            return;
        }

        var total = 0;
        foreach (var empresaId in empresas)
        {
            try
            {
                using var ambito = _ambitos.CreateScope();
                ambito.ServiceProvider.GetRequiredService<IContextoEmpresaMutable>().Fijar(empresaId);
                var caso = ambito.ServiceProvider.GetRequiredService<EnviarRecordatoriosReservas>();
                total += await caso.EjecutarAsync(empresaId, ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Un fallo en una empresa no debe tumbar el proceso ni afectar a las demás.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.LogError(ex, "Fallo al enviar recordatorios de reservas de la empresa {EmpresaId}.", empresaId);
            }
        }

        if (total > 0)
        {
            _log.LogInformation("Recordatorios de reservas: {Total} enviado(s) en {Empresas} empresa(s).", total, empresas.Count);
        }
    }
}

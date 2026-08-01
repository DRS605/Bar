using AlxorCore.Api.Comun;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Tesoreria.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Tesorería (cobros y pagos).</summary>
public static class EndpointsTesoreria
{
    public static IEndpointRouteBuilder MapearTesoreria(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapPost("/cobros", CobrarAsync)
            .WithTags("Tesorería").WithSummary("Registra un cobro contra una factura.")
            .RequierePermiso(Permisos.CobroRegistrar);

        rutas.MapPost("/pagos", PagarAsync)
            .WithTags("Tesorería").WithSummary("Registra un pago contra un gasto.")
            .RequierePermiso(Permisos.PagoRegistrar);

        rutas.MapGet("/facturas/{id:guid}/saldo", SaldoFacturaAsync)
            .WithTags("Tesorería").WithSummary("Saldo de una factura.")
            .RequierePermiso(Permisos.FacturaLeer);

        rutas.MapGet("/gastos/{id:guid}/saldo", SaldoGastoAsync)
            .WithTags("Tesorería").WithSummary("Saldo de un gasto.")
            .RequierePermiso(Permisos.GastoLeer);

        return rutas;
    }

    private static async Task<IResult> CobrarAsync(RegistrarCobroComando comando, IContextoEmpresa contexto, RegistrarCobro caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> PagarAsync(RegistrarPagoComando comando, IContextoEmpresa contexto, RegistrarPago caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> SaldoFacturaAsync(Guid id, ConsultarSaldo caso, CancellationToken ct) =>
        (await caso.DeFacturaAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> SaldoGastoAsync(Guid id, ConsultarSaldo caso, CancellationToken ct) =>
        (await caso.DeGastoAsync(id, ct).ConfigureAwait(false)).AOk();
}

using AlxorCore.Api.Comun;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Tesoreria.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Petición para conciliar un extracto bancario en formato Norma 43.</summary>
public sealed record ConciliarPeticion(string Contenido);

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

        rutas.MapPost("/tesoreria/conciliacion", ConciliarAsync)
            .WithTags("Tesorería").WithSummary("Lee un extracto bancario (Norma 43) y propone casaciones con facturas y gastos pendientes.")
            .RequierePermiso(Permisos.CobroRegistrar);

        rutas.MapPost("/tesoreria/remesa", RemesaAsync)
            .WithTags("Tesorería").WithSummary("Genera una remesa de adeudos SEPA (pain.008 / Norma 19) para las facturas indicadas.")
            .RequierePermiso(Permisos.CobroRegistrar);

        return rutas;
    }

    private static async Task<IResult> RemesaAsync(GenerarRemesaComando comando, IContextoEmpresa contexto, GenerarRemesaSepa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> ConciliarAsync(ConciliarPeticion peticion, IContextoEmpresa contexto, ConciliarExtracto caso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(peticion);
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, peticion.Contenido, ct).ConfigureAwait(false);
        return resultado.AOk();
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

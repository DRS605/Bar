using AlxorCore.Api.Comun;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Facturación.</summary>
public static class EndpointsFacturacion
{
    public static IEndpointRouteBuilder MapearFacturacion(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var facturas = rutas.MapGroup("/facturas").WithTags("Facturas");

        facturas.MapPost("", EmitirAsync)
            .WithSummary("Emite una factura.")
            .RequierePermiso(Permisos.FacturaEmitir);

        facturas.MapGet("", ListarAsync)
            .WithSummary("Lista las facturas de la empresa activa.")
            .RequierePermiso(Permisos.FacturaLeer);

        facturas.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene una factura con sus líneas.")
            .RequierePermiso(Permisos.FacturaLeer);

        return rutas;
    }

    private static async Task<IResult> EmitirAsync(EmitirFacturaComando comando, IContextoEmpresa contexto, EmitirFactura caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarFacturas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerFactura caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();
}

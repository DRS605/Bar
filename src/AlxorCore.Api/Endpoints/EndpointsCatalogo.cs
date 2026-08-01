using AlxorCore.Api.Comun;
using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Catálogo (productos e impuestos).</summary>
public static class EndpointsCatalogo
{
    public static IEndpointRouteBuilder MapearCatalogo(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var productos = rutas.MapGroup("/productos").WithTags("Productos");

        productos.MapGet("", ListarAsync)
            .WithSummary("Lista los productos de la empresa activa.")
            .RequireAuthorization();

        productos.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un producto.")
            .RequireAuthorization();

        productos.MapPost("", CrearAsync)
            .WithSummary("Crea un producto.")
            .RequierePermiso(Permisos.ProductoGestionar);

        productos.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza un producto.")
            .RequierePermiso(Permisos.ProductoGestionar);

        rutas.MapGet("/impuestos", () => Results.Ok(ListarImpuestos.Ejecutar()))
            .WithTags("Impuestos")
            .WithSummary("Lista los tipos de IVA disponibles.")
            .RequireAuthorization();

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarProductos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerProducto caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosProducto datos, IContextoEmpresa contexto, CrearProducto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/productos/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosProducto datos, ActualizarProducto caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();
}

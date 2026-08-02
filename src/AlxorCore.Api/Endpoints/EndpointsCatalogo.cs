using AlxorCore.Api.Comun;
using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Catalogo.Dominio;
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

        productos.MapPost("/importar", ImportarAsync)
            .WithSummary("Importa productos desde CSV (previsualiza o confirma).")
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

    private static async Task<IResult> ImportarAsync(ImportarCsvPeticion peticion, IContextoEmpresa contexto, ImportarProductos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var filas = new List<FilaImportacionProducto>();
        foreach (var fila in LectorCsv.Parsear(peticion.Contenido ?? string.Empty))
        {
            var tipoTexto = LectorCsv.Normalizar(fila.Campo("tipo") ?? string.Empty);
            var tipo = tipoTexto is "bien" or "producto" or "articulo" ? TipoProducto.Bien : TipoProducto.Servicio;
            var datos = new DatosProducto(
                Nombre: fila.Campo("nombre", "producto", "articulo", "descripcion") ?? string.Empty,
                PrecioUnitario: ImportacionCsv.Numero(fila.Campo("precio", "precio unitario", "importe", "pvp")),
                Referencia: fila.Campo("referencia", "codigo", "ean", "sku", "código"),
                Tipo: tipo,
                CodigoIva: ImportacionCsv.CodigoIva(fila.Campo("iva", "codigo iva", "tipo iva")),
                Unidad: fila.Campo("unidad"));
            filas.Add(new FilaImportacionProducto(fila.Numero, datos));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, filas, peticion.Previsualizar, ct).ConfigureAwait(false);
        return Results.Ok(resultado);
    }
}

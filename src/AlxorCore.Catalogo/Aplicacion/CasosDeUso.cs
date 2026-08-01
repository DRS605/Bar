using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Catalogo.Aplicacion;

/// <summary>Datos de un producto para crear o actualizar.</summary>
public sealed record DatosProducto(
    string Nombre,
    decimal PrecioUnitario,
    string? Referencia = null,
    TipoProducto Tipo = TipoProducto.Servicio,
    string? CodigoIva = null,
    string? Unidad = null);

/// <summary>Caso de uso: crear un producto en la empresa activa.</summary>
public sealed class CrearProducto
{
    private readonly IRepositorioProductos _productos;
    private readonly IUnidadDeTrabajoCatalogo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearProducto(IRepositorioProductos productos, IUnidadDeTrabajoCatalogo unidadDeTrabajo, IReloj reloj)
    {
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ProductoDto>> EjecutarAsync(Guid empresaId, DatosProducto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var producto = Producto.Crear(empresaId, datos.Referencia, datos.Nombre, datos.Tipo, datos.PrecioUnitario, datos.CodigoIva, datos.Unidad, _reloj);
        if (producto.EsFallo)
        {
            return Resultado.Fallo<ProductoDto>(producto.Error);
        }

        _productos.Agregar(producto.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProductoDto.Desde(producto.Valor));
    }
}

/// <summary>Caso de uso: actualizar un producto.</summary>
public sealed class ActualizarProducto
{
    private readonly IRepositorioProductos _productos;
    private readonly IUnidadDeTrabajoCatalogo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarProducto(IRepositorioProductos productos, IUnidadDeTrabajoCatalogo unidadDeTrabajo, IReloj reloj)
    {
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ProductoDto>> EjecutarAsync(Guid productoId, DatosProducto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var producto = await _productos.ObtenerPorIdAsync(productoId, ct).ConfigureAwait(false);
        if (producto is null)
        {
            return Resultado.Fallo<ProductoDto>(Error.NoEncontrado("producto.no_encontrado", "El producto no existe."));
        }

        var r = producto.Actualizar(datos.Referencia, datos.Nombre, datos.Tipo, datos.PrecioUnitario, datos.CodigoIva, datos.Unidad, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ProductoDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProductoDto.Desde(producto));
    }
}

/// <summary>Caso de uso: listar los productos de la empresa activa.</summary>
public sealed class ListarProductos
{
    private readonly IConsultaProductos _consulta;

    public ListarProductos(IConsultaProductos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ProductoDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivos: false, ct);
}

/// <summary>Caso de uso: obtener un producto por su identificador.</summary>
public sealed class ObtenerProducto
{
    private readonly IConsultaProductos _consulta;

    public ObtenerProducto(IConsultaProductos consulta) => _consulta = consulta;

    public async Task<Resultado<ProductoDto>> EjecutarAsync(Guid productoId, CancellationToken ct = default)
    {
        var producto = await _consulta.ObtenerAsync(productoId, ct).ConfigureAwait(false);
        return producto is null
            ? Resultado.Fallo<ProductoDto>(Error.NoEncontrado("producto.no_encontrado", "El producto no existe."))
            : Resultado.Ok(producto);
    }
}

/// <summary>Caso de uso: listar el catálogo de tipos de IVA disponibles.</summary>
public static class ListarImpuestos
{
    public static IReadOnlyList<ImpuestoDto> Ejecutar() => Impuesto.TodosIva.Select(ImpuestoDto.Desde).ToList();
}

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
    string? Unidad = null,
    decimal PrecioCompra = 0m,
    Guid? ProveedorHabitualId = null);

/// <summary>Caso de uso: crear un producto en la empresa activa.</summary>
public sealed class CrearProducto
{
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioHistoricoPrecios _historico;
    private readonly IUnidadDeTrabajoCatalogo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearProducto(IRepositorioProductos productos, IRepositorioHistoricoPrecios historico, IUnidadDeTrabajoCatalogo unidadDeTrabajo, IReloj reloj)
    {
        _productos = productos;
        _historico = historico;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ProductoDto>> EjecutarAsync(Guid empresaId, DatosProducto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var producto = Producto.Crear(empresaId, datos.Referencia, datos.Nombre, datos.Tipo, datos.PrecioUnitario, datos.PrecioCompra, datos.CodigoIva, datos.Unidad, _reloj, datos.ProveedorHabitualId);
        if (producto.EsFallo)
        {
            return Resultado.Fallo<ProductoDto>(producto.Error);
        }

        _productos.Agregar(producto.Valor);
        _historico.Agregar(HistoricoPrecio.Registrar(empresaId, producto.Valor.Id, producto.Valor.PrecioUnitario, producto.Valor.PrecioCompra, _reloj.AhoraUtc));
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProductoDto.Desde(producto.Valor));
    }
}

/// <summary>Caso de uso: actualizar un producto.</summary>
public sealed class ActualizarProducto
{
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioHistoricoPrecios _historico;
    private readonly IUnidadDeTrabajoCatalogo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarProducto(IRepositorioProductos productos, IRepositorioHistoricoPrecios historico, IUnidadDeTrabajoCatalogo unidadDeTrabajo, IReloj reloj)
    {
        _productos = productos;
        _historico = historico;
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

        var precioVentaAnterior = producto.PrecioUnitario;
        var precioCompraAnterior = producto.PrecioCompra;

        var r = producto.Actualizar(datos.Referencia, datos.Nombre, datos.Tipo, datos.PrecioUnitario, datos.PrecioCompra, datos.CodigoIva, datos.Unidad, _reloj, datos.ProveedorHabitualId);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ProductoDto>(r.Error);
        }

        // Solo dejamos rastro en el histórico si algún precio cambió.
        if (producto.PrecioUnitario != precioVentaAnterior || producto.PrecioCompra != precioCompraAnterior)
        {
            _historico.Agregar(HistoricoPrecio.Registrar(producto.EmpresaId, producto.Id, producto.PrecioUnitario, producto.PrecioCompra, _reloj.AhoraUtc));
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProductoDto.Desde(producto));
    }
}

/// <summary>Caso de uso: listar el histórico de precios de un producto (más reciente primero).</summary>
public sealed class ListarHistoricoPrecios
{
    private readonly IConsultaHistoricoPrecios _consulta;

    public ListarHistoricoPrecios(IConsultaHistoricoPrecios consulta) => _consulta = consulta;

    public Task<IReadOnlyList<HistoricoPrecioDto>> EjecutarAsync(Guid productoId, CancellationToken ct = default) =>
        _consulta.ListarPorProductoAsync(productoId, ct);
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

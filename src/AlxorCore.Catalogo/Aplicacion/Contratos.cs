using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Catalogo.Aplicacion;

/// <summary>Vista de un producto (incluye el porcentaje de IVA resuelto del catálogo).</summary>
public sealed record ProductoDto(
    Guid Id,
    string? Referencia,
    string Nombre,
    TipoProducto Tipo,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    string Unidad,
    bool Activo,
    decimal PrecioCompra,
    Guid? ProveedorHabitualId,
    bool ControlarStock,
    decimal Stock,
    string? Categoria)
{
    public static ProductoDto Desde(Producto p)
    {
        var porcentaje = Impuesto.PorCodigoImpuesto(p.CodigoIva).Valor.Porcentaje;
        return new ProductoDto(p.Id, p.Referencia, p.Nombre, p.Tipo, p.PrecioUnitario, p.CodigoIva, porcentaje, p.Unidad, p.Activo, p.PrecioCompra, p.ProveedorHabitualId, p.ControlarStock, p.Stock, p.Categoria);
    }
}

/// <summary>Fila del histórico de movimientos de stock de un producto.</summary>
public sealed record MovimientoStockDto(DateTimeOffset Fecha, string Tipo, decimal Cantidad, decimal StockResultante, string? Motivo)
{
    public static MovimientoStockDto Desde(MovimientoStock m) => new(m.CreadoEn, m.Tipo.ToString(), m.Cantidad, m.StockResultante, m.Motivo);
}

/// <summary>Fila del histórico de precios de un producto.</summary>
public sealed record HistoricoPrecioDto(DateTimeOffset RegistradoEn, decimal PrecioVenta, decimal PrecioCompra)
{
    public static HistoricoPrecioDto Desde(HistoricoPrecio h) => new(h.RegistradoEn, h.PrecioVenta, h.PrecioCompra);
}

/// <summary>Vista de un tipo de impuesto del catálogo.</summary>
public sealed record ImpuestoDto(string Codigo, string Nombre, TipoImpuesto Tipo, decimal Porcentaje)
{
    public static ImpuestoDto Desde(Impuesto i) => new(i.Codigo, i.Nombre, i.Tipo, i.Porcentaje);
}

/// <summary>Repositorio de productos (escritura).</summary>
public interface IRepositorioProductos
{
    Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Producto producto);
}

/// <summary>Consultas de lectura de productos (las usan la API y Facturación).</summary>
public interface IConsultaProductos
{
    Task<ProductoDto?> ObtenerAsync(Guid productoId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Repositorio del histórico de precios (solo escritura: se añaden filas).</summary>
public interface IRepositorioHistoricoPrecios
{
    void Agregar(HistoricoPrecio historico);
}

/// <summary>Consulta del histórico de precios de un producto.</summary>
public interface IConsultaHistoricoPrecios
{
    Task<IReadOnlyList<HistoricoPrecioDto>> ListarPorProductoAsync(Guid productoId, CancellationToken ct = default);
}

/// <summary>Repositorio de movimientos de stock (solo escritura: se añaden filas).</summary>
public interface IRepositorioMovimientosStock
{
    void Agregar(MovimientoStock movimiento);
}

/// <summary>Consulta del histórico de movimientos de stock de un producto.</summary>
public interface IConsultaMovimientosStock
{
    Task<IReadOnlyList<MovimientoStockDto>> ListarPorProductoAsync(Guid productoId, CancellationToken ct = default);
}

/// <summary>Una línea vendida que puede descontar existencias (producto + cantidad).</summary>
public sealed record LineaVenta(Guid ProductoId, decimal Cantidad);

/// <summary>
/// Puerto que descuenta existencias al vender. Lo usa Facturación tras emitir una factura o ticket,
/// sin conocer los detalles del módulo Catálogo. Es tolerante: ignora productos sin control de stock.
/// </summary>
public interface IStockVentas
{
    Task DescontarVentaAsync(Guid empresaId, IReadOnlyList<LineaVenta> lineas, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Catálogo.</summary>
public interface IUnidadDeTrabajoCatalogo : IUnidadDeTrabajo;

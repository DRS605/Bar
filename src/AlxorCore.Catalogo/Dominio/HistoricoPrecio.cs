using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Catalogo.Dominio;

/// <summary>
/// Registro histórico de los precios (compra y venta) de un producto en un instante dado. Se añade
/// una fila al crear el producto y cada vez que cambia alguno de sus precios, de modo que se puede
/// consultar la evolución de precios a lo largo del tiempo. Es inmutable: solo se añaden filas.
/// </summary>
public sealed class HistoricoPrecio : RaizAgregadoEmpresa<Guid>
{
    private HistoricoPrecio(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private HistoricoPrecio(Guid id, Guid empresaId, Guid productoId, decimal precioVenta, decimal precioCompra, DateTimeOffset registradoEn)
        : base(id, empresaId)
    {
        ProductoId = productoId;
        PrecioVenta = precioVenta;
        PrecioCompra = precioCompra;
        RegistradoEn = registradoEn;
    }

    public Guid ProductoId { get; private set; }

    public decimal PrecioVenta { get; private set; }

    public decimal PrecioCompra { get; private set; }

    public DateTimeOffset RegistradoEn { get; private set; }

    public static HistoricoPrecio Registrar(Guid empresaId, Guid productoId, decimal precioVenta, decimal precioCompra, DateTimeOffset registradoEn) =>
        new(Guid.NewGuid(), empresaId, productoId, precioVenta, precioCompra, registradoEn);
}

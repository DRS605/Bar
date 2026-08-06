using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Catalogo.Dominio;

/// <summary>Tipo de movimiento de stock (existencias) de un producto.</summary>
public enum TipoMovimientoStock
{
    /// <summary>Entrada de mercancía (compra, reposición).</summary>
    Entrada = 1,

    /// <summary>Salida manual (merma, autoconsumo, rotura).</summary>
    Salida = 2,

    /// <summary>Ajuste a un stock contado (recuento de inventario).</summary>
    Ajuste = 3,

    /// <summary>Salida automática por una venta (factura o ticket).</summary>
    Venta = 4,
}

/// <summary>
/// Movimiento de existencias de un producto. Es un registro inmutable (histórico): guarda la
/// variación aplicada (<see cref="Cantidad"/>, con signo) y el stock resultante tras aplicarla.
/// </summary>
public sealed class MovimientoStock : RaizAgregadoEmpresa<Guid>
{
    private MovimientoStock(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private MovimientoStock(Guid id, Guid empresaId, Guid productoId, TipoMovimientoStock tipo, decimal cantidad, decimal stockResultante, string? motivo, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ProductoId = productoId;
        Tipo = tipo;
        Cantidad = cantidad;
        StockResultante = stockResultante;
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        CreadoEn = ahora;
    }

    public Guid ProductoId { get; private set; }

    public TipoMovimientoStock Tipo { get; private set; }

    /// <summary>Variación aplicada al stock, con signo (positiva = entrada, negativa = salida).</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Stock del producto después de aplicar este movimiento.</summary>
    public decimal StockResultante { get; private set; }

    public string? Motivo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    internal static MovimientoStock Registrar(
        Guid empresaId, Guid productoId, TipoMovimientoStock tipo, decimal cantidad, decimal stockResultante, string? motivo, DateTimeOffset ahora) =>
        new(Guid.NewGuid(), empresaId, productoId, tipo, cantidad, stockResultante, motivo, ahora);
}

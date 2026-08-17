using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>
/// Línea de una comanda: un producto pedido con su cantidad. El precio y el IVA se «congelan» al
/// añadirla (tomados del catálogo en ese momento), de modo que un cambio de tarifa posterior no
/// altere una cuenta ya en marcha. Pertenece al agregado <see cref="Comanda"/>.
/// </summary>
public sealed class LineaComanda : EntidadBase<Guid>
{
    public const int LongitudMaximaDescripcion = 200;

    private LineaComanda(Guid id)
        : base(id)
    {
        Descripcion = null!;
        CodigoIva = null!;
    }

    internal LineaComanda(Guid empresaId, Guid comandaId, Guid productoId, string descripcion, decimal cantidad, decimal precioUnitario, string codigoIva, decimal porcentajeIva)
        : base(Guid.NewGuid())
    {
        EmpresaId = empresaId;
        ComandaId = comandaId;
        ProductoId = productoId;
        Descripcion = descripcion.Length > LongitudMaximaDescripcion ? descripcion[..LongitudMaximaDescripcion] : descripcion;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        CodigoIva = codigoIva;
        PorcentajeIva = porcentajeIva;

        Base = Redondeo.Dos(Cantidad * PrecioUnitario);
        CuotaIva = Redondeo.Dos(Base * PorcentajeIva / 100m);
    }

    /// <summary>Empresa (para el aislamiento multiempresa de la tabla de líneas).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Comanda a la que pertenece la línea.</summary>
    public Guid ComandaId { get; private set; }

    /// <summary>Producto del catálogo pedido (permite descontar existencias al cobrar).</summary>
    public Guid ProductoId { get; private set; }

    public string Descripcion { get; private set; }

    public decimal Cantidad { get; private set; }

    public decimal PrecioUnitario { get; private set; }

    public string CodigoIva { get; private set; }

    public decimal PorcentajeIva { get; private set; }

    /// <summary>Base imponible de la línea (cantidad × precio).</summary>
    public decimal Base { get; private set; }

    /// <summary>Cuota de IVA de la línea.</summary>
    public decimal CuotaIva { get; private set; }

    /// <summary>Importe total de la línea con IVA incluido.</summary>
    public decimal Total => Redondeo.Dos(Base + CuotaIva);

    /// <summary>Cantidad de esta línea ya enviada a cocina/barra (para acumular reenvíos parciales).</summary>
    public decimal CantidadEnviadaCocina { get; private set; }

    /// <summary>Cantidad pendiente de enviar a cocina (lo pedido menos lo ya enviado).</summary>
    public decimal CantidadPendienteCocina => Cantidad > CantidadEnviadaCocina ? Cantidad - CantidadEnviadaCocina : 0m;

    /// <summary>Cantidad de esta línea ya cobrada en tickets parciales (reparto de la cuenta por artículos).</summary>
    public decimal CantidadCobrada { get; private set; }

    /// <summary>Cantidad todavía pendiente de cobro (lo pedido menos lo ya cobrado).</summary>
    public decimal CantidadPendienteCobro => Cantidad > CantidadCobrada ? Cantidad - CantidadCobrada : 0m;

    /// <summary>Base imponible de la parte pendiente de cobro.</summary>
    public decimal BasePendiente => Redondeo.Dos(CantidadPendienteCobro * PrecioUnitario);

    /// <summary>Cuota de IVA de la parte pendiente de cobro.</summary>
    public decimal CuotaIvaPendiente => Redondeo.Dos(BasePendiente * PorcentajeIva / 100m);

    /// <summary>Importe total (con IVA) de la parte pendiente de cobro.</summary>
    public decimal TotalPendiente => Redondeo.Dos(BasePendiente + CuotaIvaPendiente);

    /// <summary>Suma cantidad a la parte ya cobrada de la línea (al emitir un ticket parcial).</summary>
    internal void RegistrarCobrado(decimal cantidad) => CantidadCobrada += cantidad;

    /// <summary>Marca como enviada la cantidad pendiente y devuelve cuánto se envía ahora (0 si nada).</summary>
    internal decimal MarcarEnviadaCocina()
    {
        var nueva = CantidadPendienteCocina;
        if (nueva > 0)
        {
            CantidadEnviadaCocina = Cantidad;
        }

        return nueva;
    }

    /// <summary>
    /// Suma cantidad a la línea (pedir otra unidad del mismo producto). Lo usa la comanda para acumular
    /// en una sola línea las consumiciones repetidas, en vez de duplicarlas.
    /// </summary>
    internal void Incrementar(decimal cantidad) => FijarCantidad(Cantidad + cantidad);

    /// <summary>Fija la cantidad de la línea (edición directa desde la comanda) y recalcula base e IVA.</summary>
    internal void FijarCantidad(decimal cantidad)
    {
        Cantidad = cantidad;
        Base = Redondeo.Dos(Cantidad * PrecioUnitario);
        CuotaIva = Redondeo.Dos(Base * PorcentajeIva / 100m);
    }
}

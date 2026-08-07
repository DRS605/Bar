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
}

using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Facturacion.Dominio;

/// <summary>Estado de un presupuesto.</summary>
public enum EstadoPresupuesto
{
    /// <summary>Editable; aún no aceptado ni rechazado.</summary>
    Borrador = 1,

    /// <summary>Aceptado: se ha convertido en factura.</summary>
    Aceptado = 2,

    /// <summary>Rechazado por el cliente.</summary>
    Rechazado = 3,
}

/// <summary>Línea de un presupuesto (no fiscal; se recalcula al editar).</summary>
public sealed class LineaPresupuesto : EntidadBase<Guid>
{
    private LineaPresupuesto(Guid id)
        : base(id)
    {
        Descripcion = null!;
        CodigoIva = null!;
    }

    internal LineaPresupuesto(Guid empresaId, NuevaLinea datos)
        : base(Guid.NewGuid())
    {
        EmpresaId = empresaId;
        ProductoId = datos.ProductoId;
        Descripcion = datos.Descripcion.Trim();
        Cantidad = datos.Cantidad;
        PrecioUnitario = datos.PrecioUnitario;
        PorcentajeDescuento = datos.PorcentajeDescuento;
        CodigoIva = datos.CodigoIva;
        PorcentajeIva = datos.PorcentajeIva;
        Base = Redondeo.Dos(Cantidad * PrecioUnitario * (1 - (PorcentajeDescuento / 100m)));
        CuotaIva = Redondeo.Dos(Base * PorcentajeIva / 100m);
    }

    public Guid EmpresaId { get; private set; }

    public Guid? ProductoId { get; private set; }

    public string Descripcion { get; private set; }

    public decimal Cantidad { get; private set; }

    public decimal PrecioUnitario { get; private set; }

    public decimal PorcentajeDescuento { get; private set; }

    public string CodigoIva { get; private set; }

    public decimal PorcentajeIva { get; private set; }

    public decimal Base { get; private set; }

    public decimal CuotaIva { get; private set; }
}

/// <summary>
/// Presupuesto (oferta) a un cliente. <b>No es un documento fiscal</b>: es editable, no lleva
/// numeración correlativa legal ni VeriFactu. Cuando el cliente lo acepta, se <b>convierte en una
/// factura</b> real (que sí aplica todos los invariantes fiscales).
/// </summary>
public sealed class Presupuesto : RaizAgregadoEmpresa<Guid>
{
    private readonly List<LineaPresupuesto> _lineas = [];

    private Presupuesto(Guid id)
        : base(id, Guid.Empty)
    {
        NumeroCompleto = null!;
        ClienteNombre = null!;
    }

    private Presupuesto(Guid id, Guid empresaId, string numeroCompleto, Guid clienteId, string clienteNombre, DateOnly fecha, DateOnly validez, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        NumeroCompleto = numeroCompleto;
        ClienteId = clienteId;
        ClienteNombre = clienteNombre;
        Fecha = fecha;
        Validez = validez;
        Estado = EstadoPresupuesto.Borrador;
        CreadoEn = ahora;
    }

    public string NumeroCompleto { get; private set; }

    public Guid ClienteId { get; private set; }

    public string ClienteNombre { get; private set; }

    public DateOnly Fecha { get; private set; }

    /// <summary>Fecha hasta la que es válido el presupuesto.</summary>
    public DateOnly Validez { get; private set; }

    public EstadoPresupuesto Estado { get; private set; }

    public decimal BaseImponible { get; private set; }

    public decimal CuotaIva { get; private set; }

    public decimal Total { get; private set; }

    /// <summary>Factura resultante si se aceptó (conversión), o null.</summary>
    public Guid? FacturaId { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public IReadOnlyList<LineaPresupuesto> Lineas => _lineas.AsReadOnly();

    public static Resultado<Presupuesto> Crear(
        Guid empresaId, string numeroCompleto, Guid clienteId, string clienteNombre, DateOnly fecha, DateOnly validez, IReadOnlyList<NuevaLinea> lineas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(reloj);

        if (lineas.Count == 0)
        {
            return Resultado.Fallo<Presupuesto>(Error.Validacion("presupuesto.sin_lineas", "El presupuesto debe tener al menos una línea."));
        }

        var presupuesto = new Presupuesto(Guid.NewGuid(), empresaId, numeroCompleto, clienteId, clienteNombre, fecha, validez, reloj.AhoraUtc);
        presupuesto.EstablecerLineas(lineas);
        return Resultado.Ok(presupuesto);
    }

    public Resultado Actualizar(Guid clienteId, string clienteNombre, DateOnly validez, IReadOnlyList<NuevaLinea> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        if (Estado != EstadoPresupuesto.Borrador)
        {
            return Resultado.Fallo(Error.Conflicto("presupuesto.no_editable", "Solo se puede editar un presupuesto en borrador."));
        }

        if (lineas.Count == 0)
        {
            return Resultado.Fallo(Error.Validacion("presupuesto.sin_lineas", "El presupuesto debe tener al menos una línea."));
        }

        ClienteId = clienteId;
        ClienteNombre = clienteNombre;
        Validez = validez;
        EstablecerLineas(lineas);
        return Resultado.Ok();
    }

    public Resultado MarcarAceptado(Guid facturaId)
    {
        if (Estado != EstadoPresupuesto.Borrador)
        {
            return Resultado.Fallo(Error.Conflicto("presupuesto.no_borrador", "Solo se puede aceptar un presupuesto en borrador."));
        }

        Estado = EstadoPresupuesto.Aceptado;
        FacturaId = facturaId;
        return Resultado.Ok();
    }

    public Resultado MarcarRechazado()
    {
        if (Estado != EstadoPresupuesto.Borrador)
        {
            return Resultado.Fallo(Error.Conflicto("presupuesto.no_borrador", "Solo se puede rechazar un presupuesto en borrador."));
        }

        Estado = EstadoPresupuesto.Rechazado;
        return Resultado.Ok();
    }

    private void EstablecerLineas(IReadOnlyList<NuevaLinea> lineas)
    {
        _lineas.Clear();
        foreach (var datos in lineas)
        {
            _lineas.Add(new LineaPresupuesto(EmpresaId, datos));
        }

        BaseImponible = Redondeo.Dos(_lineas.Sum(l => l.Base));
        CuotaIva = Redondeo.Dos(_lineas.Sum(l => l.CuotaIva));
        Total = Redondeo.Dos(BaseImponible + CuotaIva);
    }
}

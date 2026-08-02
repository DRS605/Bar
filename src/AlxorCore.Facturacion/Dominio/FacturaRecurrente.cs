using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Facturacion.Dominio;

/// <summary>Cada cuánto se emite automáticamente una factura periódica.</summary>
public enum Periodicidad
{
    Semanal = 1,
    Mensual = 2,
    Trimestral = 3,
    Semestral = 4,
    Anual = 5,
}

/// <summary>Operaciones sobre la periodicidad (cálculo de la siguiente fecha).</summary>
public static class PeriodicidadExtensiones
{
    /// <summary>Avanza una fecha según la periodicidad. <c>AddMonths</c> ajusta fin de mes (31→28).</summary>
    public static DateOnly Avanzar(this Periodicidad periodicidad, DateOnly desde) => periodicidad switch
    {
        Periodicidad.Semanal => desde.AddDays(7),
        Periodicidad.Mensual => desde.AddMonths(1),
        Periodicidad.Trimestral => desde.AddMonths(3),
        Periodicidad.Semestral => desde.AddMonths(6),
        Periodicidad.Anual => desde.AddYears(1),
        _ => desde.AddMonths(1),
    };
}

/// <summary>
/// Plantilla de factura recurrente (una suscripción/contrato). No es una factura fiscal: es la
/// definición desde la que ALXOR Core <b>emite automáticamente</b> facturas ordinarias cuando llega
/// su fecha. Cada emisión produce una <see cref="Factura"/> real e inmutable con su número
/// correlativo; esta plantilla solo guarda qué facturar, a quién y con qué cadencia.
/// </summary>
public sealed class FacturaRecurrente : RaizAgregadoEmpresa<Guid>
{
    public const decimal IrpfMaximo = 60m;

    private readonly List<LineaRecurrente> _lineas = [];

    private FacturaRecurrente(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private FacturaRecurrente(
        Guid id, Guid empresaId, string nombre, Guid clienteId, Periodicidad periodicidad,
        DateOnly proximaEmision, DateOnly? fechaFin, decimal porcentajeIrpf, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        ClienteId = clienteId;
        Periodicidad = periodicidad;
        ProximaEmision = proximaEmision;
        FechaFin = fechaFin;
        PorcentajeIrpf = porcentajeIrpf;
        Activa = true;
        FacturasGeneradas = 0;
        CreadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public Guid ClienteId { get; private set; }

    public Periodicidad Periodicidad { get; private set; }

    /// <summary>Fecha en la que corresponde emitir la próxima factura.</summary>
    public DateOnly ProximaEmision { get; private set; }

    /// <summary>Fecha en la que la recurrencia deja de emitir (opcional).</summary>
    public DateOnly? FechaFin { get; private set; }

    public decimal PorcentajeIrpf { get; private set; }

    /// <summary>Si está pausada no se emite aunque llegue su fecha.</summary>
    public bool Activa { get; private set; }

    public int FacturasGeneradas { get; private set; }

    public DateOnly? UltimaEmision { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public IReadOnlyList<LineaRecurrente> Lineas => _lineas.AsReadOnly();

    /// <summary>Crea una factura recurrente con su plantilla de líneas.</summary>
    public static Resultado<FacturaRecurrente> Crear(
        Guid empresaId,
        string? nombre,
        Guid clienteId,
        Periodicidad periodicidad,
        DateOnly primeraEmision,
        DateOnly? fechaFin,
        decimal porcentajeIrpf,
        IReadOnlyList<LineaPlantilla> lineas,
        IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(reloj);

        var validacion = Validar(nombre, periodicidad, primeraEmision, fechaFin, porcentajeIrpf, lineas);
        if (validacion is not null)
        {
            return Resultado.Fallo<FacturaRecurrente>(validacion);
        }

        var recurrente = new FacturaRecurrente(
            Guid.NewGuid(), empresaId, nombre!.Trim(), clienteId, periodicidad, primeraEmision, fechaFin, porcentajeIrpf, reloj.AhoraUtc);
        foreach (var linea in lineas)
        {
            recurrente._lineas.Add(new LineaRecurrente(empresaId, linea));
        }

        recurrente.RegistrarEvento(new FacturaRecurrenteCreada(recurrente.Id, empresaId, recurrente.Nombre, reloj.AhoraUtc));
        return Resultado.Ok(recurrente);
    }

    /// <summary>Actualiza los datos y la plantilla de líneas de la recurrencia.</summary>
    public Resultado Actualizar(
        string? nombre,
        Periodicidad periodicidad,
        DateOnly proximaEmision,
        DateOnly? fechaFin,
        decimal porcentajeIrpf,
        IReadOnlyList<LineaPlantilla> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        var validacion = Validar(nombre, periodicidad, proximaEmision, fechaFin, porcentajeIrpf, lineas);
        if (validacion is not null)
        {
            return Resultado.Fallo(validacion);
        }

        Nombre = nombre!.Trim();
        Periodicidad = periodicidad;
        ProximaEmision = proximaEmision;
        FechaFin = fechaFin;
        PorcentajeIrpf = porcentajeIrpf;

        _lineas.Clear();
        foreach (var linea in lineas)
        {
            _lineas.Add(new LineaRecurrente(EmpresaId, linea));
        }

        return Resultado.Ok();
    }

    /// <summary>Reanuda la emisión automática.</summary>
    public void Activar() => Activa = true;

    /// <summary>Pausa la emisión automática (no se borra el histórico).</summary>
    public void Desactivar() => Activa = false;

    /// <summary>¿Corresponde emitir hoy? (activa, con fecha vencida y dentro del rango de fin).</summary>
    public bool EstaVencida(DateOnly hoy) =>
        Activa && ProximaEmision <= hoy && (FechaFin is null || ProximaEmision <= FechaFin);

    /// <summary>
    /// Registra que se ha emitido una factura para esta recurrencia. Avanza la próxima fecha a la
    /// siguiente ocurrencia <b>estrictamente posterior</b> a la fecha emitida: así se emite una sola
    /// factura por pasada aunque la recurrencia estuviera muy atrasada (no se generan facturas de
    /// meses pasados de golpe). Si la próxima fecha supera la fecha de fin, se desactiva sola.
    /// </summary>
    public void RegistrarEmision(DateOnly fechaEmitida)
    {
        UltimaEmision = fechaEmitida;
        FacturasGeneradas++;
        do
        {
            ProximaEmision = Periodicidad.Avanzar(ProximaEmision);
        }
        while (ProximaEmision <= fechaEmitida);

        if (FechaFin is not null && ProximaEmision > FechaFin)
        {
            Activa = false;
        }
    }

    private static Error? Validar(
        string? nombre, Periodicidad periodicidad, DateOnly proximaEmision, DateOnly? fechaFin,
        decimal porcentajeIrpf, IReadOnlyList<LineaPlantilla> lineas)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("recurrente.sin_nombre", "La factura recurrente necesita un nombre.");
        }

        if (!Enum.IsDefined(periodicidad))
        {
            return Error.Validacion("recurrente.periodicidad", "La periodicidad no es válida.");
        }

        if (fechaFin is not null && fechaFin < proximaEmision)
        {
            return Error.Validacion("recurrente.fecha_fin", "La fecha de fin no puede ser anterior a la próxima emisión.");
        }

        if (porcentajeIrpf is < 0 or > IrpfMaximo)
        {
            return Error.Validacion("recurrente.irpf_invalido", "El porcentaje de IRPF no es válido.");
        }

        if (lineas.Count == 0)
        {
            return Error.Validacion("recurrente.sin_lineas", "La factura recurrente debe tener al menos una línea.");
        }

        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea.Descripcion))
            {
                return Error.Validacion("recurrente.linea_sin_descripcion", "Cada línea necesita una descripción.");
            }

            if (linea.Cantidad <= 0)
            {
                return Error.Validacion("recurrente.linea_cantidad", "La cantidad debe ser mayor que cero.");
            }

            if (linea.PrecioUnitario < 0)
            {
                return Error.Validacion("recurrente.linea_precio", "El precio no puede ser negativo.");
            }

            if (linea.PorcentajeDescuento is < 0 or > 100)
            {
                return Error.Validacion("recurrente.linea_descuento", "El descuento debe estar entre 0 y 100.");
            }
        }

        return null;
    }
}

/// <summary>Datos de una línea de la plantilla recurrente (entrada del caso de uso).</summary>
public sealed record LineaPlantilla(
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null);

/// <summary>
/// Línea de la plantilla de una factura recurrente. Se copia a cada factura emitida; sus importes
/// aquí son solo orientativos (previsualización), la factura real los recalcula al emitir.
/// </summary>
public sealed class LineaRecurrente : EntidadBase<Guid>
{
    private LineaRecurrente(Guid id)
        : base(id)
    {
        Descripcion = null!;
        CodigoIva = null!;
    }

    internal LineaRecurrente(Guid empresaId, LineaPlantilla datos)
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

/// <summary>Se ha creado una factura recurrente.</summary>
public sealed record FacturaRecurrenteCreada(
    Guid FacturaRecurrenteId,
    Guid EmpresaId,
    string Nombre,
    DateTimeOffset OcurridoEn) : IEventoDominio;

using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Estado de una comanda a lo largo de su ciclo de vida.</summary>
public enum EstadoComanda
{
    /// <summary>Abierta: se pueden añadir o quitar líneas. Es el estado de trabajo.</summary>
    Abierta = 1,

    /// <summary>Cobrada: se ha emitido el ticket y la mesa queda libre. Es inmutable.</summary>
    Cobrada = 2,

    /// <summary>Anulada: la comanda se cerró sin cobrar (error, mesa que se marcha sin consumir…).</summary>
    Anulada = 3,
}

/// <summary>Forma de cobro de una comanda.</summary>
public enum MetodoCobro
{
    /// <summary>Pago en efectivo.</summary>
    Efectivo = 1,

    /// <summary>Pago con tarjeta.</summary>
    Tarjeta = 2,

    /// <summary>Otro medio (transferencia, vale…).</summary>
    Otro = 3,
}

/// <summary>Se ha abierto una comanda en una mesa.</summary>
public sealed record ComandaAbierta(Guid ComandaId, Guid EmpresaId, Guid MesaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Se ha cobrado una comanda (con el ticket generado).</summary>
public sealed record ComandaCobrada(Guid ComandaId, Guid EmpresaId, Guid MesaId, Guid FacturaId, decimal Total, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Comanda (cuenta abierta) de una mesa. Es el agregado central del módulo Hostelería: acumula las
/// líneas de lo consumido mientras está <see cref="EstadoComanda.Abierta"/> y, al cobrarse, se
/// convierte en un ticket (factura simplificada) del módulo Facturación. Sus importes se recalculan
/// en cada cambio de línea y se congelan al cobrar.
/// </summary>
public sealed class Comanda : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNotas = 500;

    private readonly List<LineaComanda> _lineas = [];

    private Comanda(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private Comanda(Guid id, Guid empresaId, Guid mesaId, string? notas, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        MesaId = mesaId;
        Notas = notas;
        Estado = EstadoComanda.Abierta;
        AbiertaEn = ahora;
    }

    public Guid MesaId { get; private set; }

    public EstadoComanda Estado { get; private set; }

    /// <summary>Notas de la comanda (alergias, «sin hielo», comensal, etc.).</summary>
    public string? Notas { get; private set; }

    public DateTimeOffset AbiertaEn { get; private set; }

    /// <summary>Instante en el que se cobró o anuló la comanda; nulo mientras está abierta.</summary>
    public DateTimeOffset? CerradaEn { get; private set; }

    /// <summary>Base imponible acumulada de las líneas.</summary>
    public decimal BaseImponible { get; private set; }

    /// <summary>Cuota de IVA acumulada de las líneas.</summary>
    public decimal CuotaIva { get; private set; }

    /// <summary>Total a pagar (base + IVA).</summary>
    public decimal Total { get; private set; }

    /// <summary>Forma de cobro empleada; nulo mientras no se ha cobrado.</summary>
    public MetodoCobro? MetodoCobro { get; private set; }

    /// <summary>Factura (ticket) generada al cobrar; nulo mientras no se ha cobrado.</summary>
    public Guid? FacturaId { get; private set; }

    /// <summary>Número completo del ticket generado al cobrar (para mostrarlo sin cruzar módulos).</summary>
    public string? NumeroTicket { get; private set; }

    /// <summary>Líneas de la comanda (solo lectura; se manipulan con los métodos del agregado).</summary>
    public IReadOnlyList<LineaComanda> Lineas => _lineas.AsReadOnly();

    public static Comanda Abrir(Guid empresaId, Guid mesaId, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var comanda = new Comanda(Guid.NewGuid(), empresaId, mesaId, Normalizar(notas), reloj.AhoraUtc);
        comanda.RegistrarEvento(new ComandaAbierta(comanda.Id, empresaId, mesaId, reloj.AhoraUtc));
        return comanda;
    }

    /// <summary>
    /// Añade una línea a la comanda a partir de un producto ya resuelto (nombre, precio e IVA
    /// «congelados» en el momento de pedirlo). Solo es posible mientras la comanda está abierta.
    /// </summary>
    public Resultado<LineaComanda> AgregarLinea(Guid productoId, string? descripcion, decimal cantidad, decimal precioUnitario, string codigoIva, decimal porcentajeIva, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.no_abierta", "Solo se pueden añadir líneas a una comanda abierta."));
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.linea_sin_descripcion", "Cada línea necesita una descripción."));
        }

        if (cantidad <= 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.cantidad_invalida", "La cantidad debe ser mayor que cero."));
        }

        if (precioUnitario < 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.precio_negativo", "El precio no puede ser negativo."));
        }

        // Si ya se pidió el mismo producto al mismo precio e IVA, se acumula en su línea (una comanda
        // muestra «Caña ×3», no tres líneas de «Caña»). Un precio distinto (tarifa cambiada) abre línea.
        var existente = _lineas.FirstOrDefault(
            l => l.ProductoId == productoId && l.PrecioUnitario == precioUnitario && l.CodigoIva == codigoIva);
        if (existente is not null)
        {
            existente.Incrementar(cantidad);
            Recalcular(reloj);
            return Resultado.Ok(existente);
        }

        var linea = new LineaComanda(EmpresaId, Id, productoId, descripcion!.Trim(), cantidad, precioUnitario, codigoIva, porcentajeIva);
        _lineas.Add(linea);
        Recalcular(reloj);
        return Resultado.Ok(linea);
    }

    /// <summary>Quita una línea de la comanda. Solo es posible mientras está abierta.</summary>
    public Resultado QuitarLinea(Guid lineaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se pueden quitar líneas de una comanda abierta."));
        }

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId);
        if (linea is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
        }

        _lineas.Remove(linea);
        Recalcular(reloj);
        return Resultado.Ok();
    }

    /// <summary>
    /// Fija la cantidad de una línea (ajuste directo con los botones +/− del TPV). Solo mientras la
    /// comanda está abierta. Para dejarla en cero, usa <see cref="QuitarLinea"/>.
    /// </summary>
    public Resultado<LineaComanda> FijarCantidadLinea(Guid lineaId, decimal cantidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.no_abierta", "Solo se pueden ajustar líneas de una comanda abierta."));
        }

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId);
        if (linea is null)
        {
            return Resultado.Fallo<LineaComanda>(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
        }

        if (cantidad <= 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.cantidad_invalida", "La cantidad debe ser mayor que cero; para eliminar la línea, quítala."));
        }

        linea.FijarCantidad(cantidad);
        Recalcular(reloj);
        return Resultado.Ok(linea);
    }

    /// <summary>Actualiza las notas de la comanda mientras está abierta.</summary>
    public Resultado ActualizarNotas(string? notas)
    {
        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se pueden editar las notas de una comanda abierta."));
        }

        Notas = Normalizar(notas);
        return Resultado.Ok();
    }

    /// <summary>
    /// Marca la comanda como cobrada, congelando el ticket generado y la forma de cobro. La emisión
    /// del ticket la orquesta el caso de uso; aquí solo se asienta el resultado en el agregado.
    /// </summary>
    public Resultado MarcarCobrada(Guid facturaId, string numeroTicket, MetodoCobro metodo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede cobrar una comanda abierta."));
        }

        if (_lineas.Count == 0)
        {
            return Resultado.Fallo(Error.Validacion("comanda.sin_lineas", "No se puede cobrar una comanda vacía."));
        }

        Estado = EstadoComanda.Cobrada;
        FacturaId = facturaId;
        NumeroTicket = numeroTicket;
        MetodoCobro = metodo;
        CerradaEn = reloj.AhoraUtc;
        RegistrarEvento(new ComandaCobrada(Id, EmpresaId, MesaId, facturaId, Total, reloj.AhoraUtc));
        return Resultado.Ok();
    }

    /// <summary>Anula la comanda sin cobrarla (libera la mesa). Solo es posible si está abierta.</summary>
    public Resultado Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede anular una comanda abierta."));
        }

        Estado = EstadoComanda.Anulada;
        CerradaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    private void Recalcular(IReloj reloj)
    {
        _ = reloj;
        BaseImponible = Redondeo.Dos(_lineas.Sum(l => l.Base));
        CuotaIva = Redondeo.Dos(_lineas.Sum(l => l.CuotaIva));
        Total = Redondeo.Dos(BaseImponible + CuotaIva);
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Tesoreria.Dominio;

/// <summary>Documento al que se asocia un movimiento de tesorería.</summary>
public enum TipoDocumentoTesoreria
{
    Factura = 1,
    Gasto = 2,
}

/// <summary>Sentido del movimiento.</summary>
public enum SentidoMovimiento
{
    /// <summary>Cobro (entra dinero; asociado a una factura).</summary>
    Cobro = 1,

    /// <summary>Pago (sale dinero; asociado a un gasto).</summary>
    Pago = 2,
}

/// <summary>Estado de cobro/pago de un documento, derivado del saldo (invariante P2).</summary>
public enum EstadoSaldo
{
    Pendiente = 1,
    Parcial = 2,
    Liquidado = 3,
}

/// <summary>Se ha registrado un movimiento de tesorería.</summary>
public sealed record MovimientoRegistrado(Guid MovimientoId, Guid EmpresaId, decimal Importe, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Movimiento de tesorería: un cobro (contra una factura) o un pago (contra un gasto), total o
/// parcial. El estado de saldo del documento se deriva de la suma de sus movimientos, no se fija a
/// mano (invariante P2). El caso de uso impide el sobrepago (invariante P1).
/// </summary>
public sealed class Movimiento : RaizAgregadoEmpresa<Guid>
{
    private Movimiento(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private Movimiento(Guid id, Guid empresaId, TipoDocumentoTesoreria tipoDocumento, Guid documentoId, SentidoMovimiento sentido, decimal importe, DateOnly fecha, string? metodo, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        TipoDocumento = tipoDocumento;
        DocumentoId = documentoId;
        Sentido = sentido;
        Importe = importe;
        Fecha = fecha;
        Metodo = metodo;
        CreadoEn = ahora;
    }

    public TipoDocumentoTesoreria TipoDocumento { get; private set; }

    public Guid DocumentoId { get; private set; }

    public SentidoMovimiento Sentido { get; private set; }

    public decimal Importe { get; private set; }

    public DateOnly Fecha { get; private set; }

    public string? Metodo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Resultado<Movimiento> Crear(
        Guid empresaId, TipoDocumentoTesoreria tipoDocumento, Guid documentoId, SentidoMovimiento sentido, decimal importe, DateOnly fecha, string? metodo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (importe <= 0)
        {
            return Resultado.Fallo<Movimiento>(Error.Validacion("movimiento.importe_invalido", "El importe debe ser mayor que cero."));
        }

        var movimiento = new Movimiento(
            Guid.NewGuid(), empresaId, tipoDocumento, documentoId, sentido, Redondeo.Dos(importe), fecha,
            string.IsNullOrWhiteSpace(metodo) ? null : metodo.Trim(), reloj.AhoraUtc);
        movimiento.RegistrarEvento(new MovimientoRegistrado(movimiento.Id, empresaId, movimiento.Importe, reloj.AhoraUtc));
        return Resultado.Ok(movimiento);
    }

    /// <summary>Deriva el estado de saldo de un documento a partir de su total y lo ya liquidado (P2).</summary>
    public static EstadoSaldo DerivarEstado(decimal total, decimal liquidado)
    {
        if (liquidado <= 0)
        {
            return EstadoSaldo.Pendiente;
        }

        return liquidado >= total ? EstadoSaldo.Liquidado : EstadoSaldo.Parcial;
    }
}

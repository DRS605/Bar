using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Tipo de movimiento manual de efectivo en la caja.</summary>
public enum TipoMovimientoCaja
{
    /// <summary>Fondo de caja al abrir (cambio inicial).</summary>
    FondoInicial = 1,

    /// <summary>Entrada de efectivo (meter cambio, aporte).</summary>
    Entrada = 2,

    /// <summary>Salida de efectivo (pagar a un proveedor, retirar al banco).</summary>
    Salida = 3,
}

/// <summary>
/// Movimiento manual de efectivo en la caja de un día (fondo inicial, entradas y salidas), para poder
/// hacer el arqueo real: comparar el efectivo contado con el teórico (fondo + cobros en efectivo +
/// entradas − salidas).
/// </summary>
public sealed class MovimientoCaja : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaConcepto = 200;

    private MovimientoCaja(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private MovimientoCaja(Guid id, Guid empresaId, DateOnly fecha, TipoMovimientoCaja tipo, decimal importe, string? concepto, Guid? usuarioId, string? usuarioNombre, DateTimeOffset momento)
        : base(id, empresaId)
    {
        Fecha = fecha;
        Tipo = tipo;
        Importe = importe;
        Concepto = concepto;
        UsuarioId = usuarioId;
        UsuarioNombre = usuarioNombre;
        Momento = momento;
    }

    /// <summary>Día de caja al que pertenece (facilita el arqueo diario).</summary>
    public DateOnly Fecha { get; private set; }

    public TipoMovimientoCaja Tipo { get; private set; }

    /// <summary>Importe en positivo; el signo lo da el <see cref="Tipo"/>.</summary>
    public decimal Importe { get; private set; }

    public string? Concepto { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string? UsuarioNombre { get; private set; }

    public DateTimeOffset Momento { get; private set; }

    public static Resultado<MovimientoCaja> Crear(Guid empresaId, TipoMovimientoCaja tipo, decimal importe, string? concepto, Guid? usuarioId, string? usuarioNombre, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (importe <= 0m)
        {
            return Resultado.Fallo<MovimientoCaja>(Error.Validacion("caja.importe_invalido", "El importe debe ser mayor que cero."));
        }

        var c = (concepto ?? string.Empty).Trim();
        var conceptoNorm = c.Length == 0 ? null : (c.Length > LongitudMaximaConcepto ? c[..LongitudMaximaConcepto] : c);
        var ahora = reloj.AhoraUtc;
        var nombre = string.IsNullOrWhiteSpace(usuarioNombre) ? null : (usuarioNombre!.Length > 60 ? usuarioNombre[..60] : usuarioNombre);

        return Resultado.Ok(new MovimientoCaja(Guid.NewGuid(), empresaId, DateOnly.FromDateTime(ahora.UtcDateTime), tipo, importe, conceptoNorm, usuarioId, nombre, ahora));
    }

    /// <summary>Importe con signo según el tipo (+ fondo/entrada, − salida).</summary>
    public decimal ImporteConSigno => Tipo == TipoMovimientoCaja.Salida ? -Importe : Importe;
}

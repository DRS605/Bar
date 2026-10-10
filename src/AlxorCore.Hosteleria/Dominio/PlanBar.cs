using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Plan (tarifa) contratado por un local. Determina qué funciones están disponibles.</summary>
public enum PlanBar
{
    /// <summary>
    /// Essential: lo esencial para la caja. Catálogo, mesas, comandas y cobro con ticket (IVA incluido)
    /// y cierre de caja. <b>Sin</b> carta QR con autopedido, comandas a cocina ni reservas.
    /// </summary>
    Essential = 0,

    /// <summary>Pro: todo lo de Essential + carta QR con autopedido, comandas a cocina y reservas.</summary>
    Pro = 1,
}

/// <summary>Utilidades sobre los planes contratables.</summary>
public static class PlanesBar
{
    /// <summary>
    /// Plan por defecto cuando un local aún no tiene suscripción grabada. Es <see cref="PlanBar.Pro"/>
    /// a propósito: los locales ya existentes conservan todas sus funciones (no se capan al introducir planes).
    /// </summary>
    public const PlanBar PorDefecto = PlanBar.Pro;

    /// <summary>Identificadores válidos de plan.</summary>
    public static readonly IReadOnlyList<string> Validos = new[] { nameof(PlanBar.Essential), nameof(PlanBar.Pro) };

    /// <summary>Normaliza un identificador de plan: si no es válido, devuelve el de por defecto.</summary>
    public static PlanBar Normalizar(string? plan) =>
        Enum.TryParse<PlanBar>((plan ?? string.Empty).Trim(), ignoreCase: true, out var p) && Enum.IsDefined(p)
            ? p
            : PorDefecto;

    /// <summary>Indica si un plan incluye las funciones «Pro» (autopedido, cocina, reservas).</summary>
    public static bool IncluyeFuncionesPro(PlanBar plan) => plan == PlanBar.Pro;
}

/// <summary>
/// Suscripción de un local (una por empresa): el <see cref="Plan"/> contratado. Si no hay suscripción
/// grabada, el local se considera en el plan por defecto (<see cref="PlanesBar.PorDefecto"/>).
/// </summary>
public sealed class SuscripcionBar : RaizAgregadoEmpresa<Guid>
{
    private SuscripcionBar(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private SuscripcionBar(Guid id, Guid empresaId, PlanBar plan, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Plan = plan;
        ActualizadaEn = ahora;
    }

    /// <summary>Plan contratado por el local.</summary>
    public PlanBar Plan { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public static SuscripcionBar Crear(Guid empresaId, PlanBar plan, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new SuscripcionBar(Guid.NewGuid(), empresaId, plan, reloj.AhoraUtc);
    }

    /// <summary>Cambia el plan contratado del local.</summary>
    public void CambiarPlan(PlanBar plan, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Plan = plan;
        ActualizadaEn = reloj.AhoraUtc;
    }
}

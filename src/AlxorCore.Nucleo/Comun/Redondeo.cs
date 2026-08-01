using System.Globalization;

namespace AlxorCore.Nucleo.Comun;

/// <summary>
/// Redondeo monetario. En facturación se redondea a 2 decimales con la regla "mitad hacia arriba"
/// (<see cref="MidpointRounding.AwayFromZero"/>), habitual en el cálculo de impuestos en España.
/// </summary>
public static class Redondeo
{
    /// <summary>Redondea un importe a 2 decimales (mitad hacia arriba).</summary>
    public static decimal Dos(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Formatea un importe con 2 decimales y coma decimal (formato español).</summary>
    public static string Formatear(decimal valor) =>
        Dos(valor).ToString("N2", CultureInfo.GetCultureInfo("es-ES"));
}

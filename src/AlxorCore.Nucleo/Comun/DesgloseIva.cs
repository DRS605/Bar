namespace AlxorCore.Nucleo.Comun;

/// <summary>
/// Desglose de un importe <b>con IVA incluido</b> (precio final, «PVP») en base imponible, cuota de
/// IVA y, en su caso, recargo de equivalencia. Pensado para hostelería y comercio, donde el precio
/// que ve y paga el cliente ya lleva el impuesto: la base se deriva hacia atrás.
/// </summary>
/// <remarks>
/// Garantiza el cuadre al céntimo: <c>Base + CuotaIva + CuotaRecargo == bruto</c>. Sin recargo (el
/// caso normal de un bar), el céntimo residual del redondeo se imputa al IVA; con recargo, al recargo.
/// El redondeo es el monetario del proyecto (<see cref="Redondeo.Dos"/>, mitad hacia arriba).
/// </remarks>
public static class DesgloseIva
{
    /// <summary>
    /// Desglosa un importe bruto (IVA incluido) en (base, cuota de IVA, cuota de recargo).
    /// </summary>
    /// <param name="bruto">Importe total con IVA (y recargo) incluido.</param>
    /// <param name="porcentajeIva">Tipo de IVA aplicado (p. ej. 10 para el 10 %).</param>
    /// <param name="porcentajeRecargo">Recargo de equivalencia, 0 si no aplica.</param>
    public static (decimal Base, decimal CuotaIva, decimal CuotaRecargo) DesdeBruto(
        decimal bruto, decimal porcentajeIva, decimal porcentajeRecargo = 0m)
    {
        var baseImponible = Redondeo.Dos(bruto / (1m + ((porcentajeIva + porcentajeRecargo) / 100m)));

        if (porcentajeRecargo == 0m)
        {
            // Todo el resto es IVA; así base + IVA == bruto exactamente.
            return (baseImponible, Redondeo.Dos(bruto - baseImponible), 0m);
        }

        var cuotaIva = Redondeo.Dos(baseImponible * porcentajeIva / 100m);
        // El céntimo residual va al recargo, para que base + IVA + recargo == bruto exactamente.
        var cuotaRecargo = Redondeo.Dos(bruto - baseImponible - cuotaIva);
        return (baseImponible, cuotaIva, cuotaRecargo);
    }
}

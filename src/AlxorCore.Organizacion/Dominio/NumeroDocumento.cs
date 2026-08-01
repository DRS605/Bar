namespace AlxorCore.Organizacion.Dominio;

/// <summary>
/// Número de documento asignado por una serie: prefijo + ejercicio + número correlativo.
/// Value object inmutable. Ejemplo de representación: <c>FA2026/000001</c>.
/// </summary>
public sealed record NumeroDocumento(string Prefijo, int Ejercicio, long Numero)
{
    /// <summary>Representación legible del número completo.</summary>
    public string Formateado => $"{Prefijo}{Ejercicio}/{Numero:D6}";

    public override string ToString() => Formateado;
}

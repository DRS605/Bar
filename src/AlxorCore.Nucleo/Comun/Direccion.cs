namespace AlxorCore.Nucleo.Comun;

/// <summary>Dirección postal. Value object sencillo; todos los campos son opcionales salvo el país (ES por defecto).</summary>
public sealed record Direccion(string Calle, string CodigoPostal, string Poblacion, string Provincia, string Pais)
{
    public static Direccion Crear(string? calle, string? codigoPostal, string? poblacion, string? provincia, string? pais = "ES") =>
        new(
            (calle ?? string.Empty).Trim(),
            (codigoPostal ?? string.Empty).Trim(),
            (poblacion ?? string.Empty).Trim(),
            (provincia ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(pais) ? "ES" : pais.Trim().ToUpperInvariant());

    /// <summary>Dirección vacía (solo país España).</summary>
    public static Direccion Vacia => new(string.Empty, string.Empty, string.Empty, string.Empty, "ES");
}

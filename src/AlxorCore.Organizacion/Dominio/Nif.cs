using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Organizacion.Dominio;

/// <summary>
/// Número de identificación fiscal español (NIF/DNI, NIE o CIF) válido y normalizado
/// (mayúsculas, sin espacios). Value object: valida el dígito/letra de control en la creación.
/// </summary>
public sealed record Nif
{
    private const string LetrasDni = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string LetrasControlCif = "JABCDEFGHI";

    private Nif(string valor) => Valor = valor;

    /// <summary>Valor normalizado del NIF.</summary>
    public string Valor { get; }

    /// <summary>Reconstruye un <see cref="Nif"/> ya validado (rehidratación desde la base de datos).</summary>
    public static Nif Rehidratar(string valor) => new(valor);

    public static Resultado<Nif> Crear(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return Resultado.Fallo<Nif>(Error.Validacion("nif.vacio", "El NIF es obligatorio."));
        }

        var v = entrada.Trim().ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        if (EsDniValido(v) || EsNieValido(v) || EsCifValido(v))
        {
            return Resultado.Ok(new Nif(v));
        }

        return Resultado.Fallo<Nif>(Error.Validacion("nif.invalido", "El NIF/CIF no es válido."));
    }

    private static bool EsDniValido(string v)
    {
        if (v.Length != 9 || !v[..8].All(char.IsDigit) || !char.IsLetter(v[8]))
        {
            return false;
        }

        var numero = int.Parse(v[..8], System.Globalization.CultureInfo.InvariantCulture);
        return LetrasDni[numero % 23] == v[8];
    }

    private static bool EsNieValido(string v)
    {
        if (v.Length != 9 || !"XYZ".Contains(v[0], StringComparison.Ordinal) || !v[1..8].All(char.IsDigit) || !char.IsLetter(v[8]))
        {
            return false;
        }

        var prefijo = v[0] switch { 'X' => "0", 'Y' => "1", _ => "2" };
        var numero = int.Parse(prefijo + v[1..8], System.Globalization.CultureInfo.InvariantCulture);
        return LetrasDni[numero % 23] == v[8];
    }

    private static bool EsCifValido(string v)
    {
        if (v.Length != 9 || !"ABCDEFGHJNPQRSUVW".Contains(v[0], StringComparison.Ordinal) || !v[1..8].All(char.IsDigit))
        {
            return false;
        }

        var suma = 0;
        for (var i = 1; i <= 7; i++)
        {
            var digito = v[i] - '0';
            if (i % 2 == 1)
            {
                // Posiciones impares (1,3,5,7): se duplican y se suman sus cifras.
                digito *= 2;
                digito = (digito / 10) + (digito % 10);
            }

            suma += digito;
        }

        var control = (10 - (suma % 10)) % 10;
        var caracterControl = v[8];

        // El control puede expresarse como dígito o como letra según el tipo de organización.
        return caracterControl == (char)('0' + control) || caracterControl == LetrasControlCif[control];
    }

    public override string ToString() => Valor;
}

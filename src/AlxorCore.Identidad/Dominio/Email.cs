using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Dirección de correo electrónico válida y normalizada (recortada y en minúsculas).
/// Es un value object: dos correos con el mismo valor normalizado son iguales. Se construye
/// siempre a través de <see cref="Crear"/>, que garantiza la invariante de formato.
/// </summary>
public sealed record Email
{
    public const int LongitudMaxima = 254;

    private Email(string valor) => Valor = valor;

    /// <summary>Valor normalizado del correo.</summary>
    public string Valor { get; }

    /// <summary>
    /// Reconstruye un <see cref="Email"/> a partir de un valor ya validado y almacenado
    /// (rehidratación desde la base de datos). No revalida: úsese solo en la persistencia.
    /// </summary>
    public static Email Rehidratar(string valor) => new(valor);

    /// <summary>Crea un <see cref="Email"/> validando y normalizando la entrada.</summary>
    public static Resultado<Email> Crear(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return Resultado.Fallo<Email>(Error.Validacion("email.vacio", "El correo electrónico es obligatorio."));
        }

        var normalizado = entrada.Trim().ToLowerInvariant();

        if (normalizado.Length > LongitudMaxima)
        {
            return Resultado.Fallo<Email>(Error.Validacion("email.demasiado_largo", "El correo electrónico es demasiado largo."));
        }

        if (!EsFormatoValido(normalizado))
        {
            return Resultado.Fallo<Email>(Error.Validacion("email.formato", "El correo electrónico no tiene un formato válido."));
        }

        return Resultado.Ok(new Email(normalizado));
    }

    private static bool EsFormatoValido(string valor)
    {
        var arroba = valor.IndexOf('@', StringComparison.Ordinal);

        // Debe haber una única '@', con parte local y dominio no vacíos.
        if (arroba <= 0 || arroba != valor.LastIndexOf('@') || arroba == valor.Length - 1)
        {
            return false;
        }

        var dominio = valor[(arroba + 1)..];

        // El dominio debe tener al menos un punto que no esté al principio ni al final,
        // y no debe contener espacios en ninguna parte.
        var punto = dominio.IndexOf('.', StringComparison.Ordinal);
        return punto > 0
            && punto < dominio.Length - 1
            && !valor.Contains(' ', StringComparison.Ordinal);
    }

    public override string ToString() => Valor;
}

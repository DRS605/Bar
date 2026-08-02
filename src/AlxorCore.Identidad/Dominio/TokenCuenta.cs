using System.Security.Cryptography;
using System.Text;

namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Tokens de cuenta de un solo uso (verificación de correo y restablecimiento de contraseña). Se
/// genera un valor aleatorio que se envía al usuario (por correo), y se almacena solo su <b>hash</b>
/// (SHA-256): así, aunque se filtre la base de datos, el token en claro no queda expuesto.
/// </summary>
public static class TokenCuenta
{
    /// <summary>Genera un token aleatorio seguro y apto para URL (32 bytes, base64url).</summary>
    public static string Nuevo()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Hash (SHA-256, hex) del token, que es lo único que se almacena.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}

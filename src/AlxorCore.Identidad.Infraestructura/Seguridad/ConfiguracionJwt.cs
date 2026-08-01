using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AlxorCore.Identidad.Infraestructura.Seguridad;

/// <summary>
/// Utilidades para configurar la validación de tokens JWT de forma coherente con su emisión
/// (misma clave, emisor y audiencia). Lo usa el host de la API al registrar la autenticación.
/// </summary>
public static class ConfiguracionJwt
{
    /// <summary>Construye los parámetros de validación de tokens a partir de las opciones.</summary>
    public static TokenValidationParameters ConstruirParametrosValidacion(OpcionesJwt opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opciones.Emisor,
            ValidateAudience = true,
            ValidAudience = opciones.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.ClaveSecreta)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }
}

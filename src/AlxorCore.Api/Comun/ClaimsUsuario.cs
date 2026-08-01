using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using AlxorCore.Nucleo.Seguridad;

namespace AlxorCore.Api.Comun;

/// <summary>Utilidades para leer la identidad del usuario autenticado desde el token.</summary>
public static class ClaimsUsuario
{
    /// <summary>Obtiene el identificador del usuario (claim <c>sub</c>) o <c>null</c> si no está presente.</summary>
    public static Guid? ObtenerUsuarioId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var valor = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out var id) ? id : null;
    }

    /// <summary>Construye la identidad mínima del usuario a partir de sus claims.</summary>
    public static IdentidadUsuario? ObtenerIdentidad(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var id = principal.ObtenerUsuarioId();
        if (id is null)
        {
            return null;
        }

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
        var nombre = principal.FindFirstValue("nombre") ?? string.Empty;
        var verificado = string.Equals(principal.FindFirstValue("email_verificado"), "true", StringComparison.OrdinalIgnoreCase);

        return new IdentidadUsuario(id.Value, email, nombre, verificado);
    }
}

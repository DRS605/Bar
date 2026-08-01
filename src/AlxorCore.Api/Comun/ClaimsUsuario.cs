using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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
}

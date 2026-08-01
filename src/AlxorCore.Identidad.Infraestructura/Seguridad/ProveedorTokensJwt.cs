using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Tiempo;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlxorCore.Identidad.Infraestructura.Seguridad;

/// <summary>Emite tokens JWT firmados con HMAC-SHA256 para los usuarios autenticados.</summary>
internal sealed class ProveedorTokensJwt : IProveedorTokens
{
    private readonly OpcionesJwt _opciones;
    private readonly IReloj _reloj;

    public ProveedorTokensJwt(IOptions<OpcionesJwt> opciones, IReloj reloj)
    {
        _opciones = opciones.Value;
        _reloj = reloj;
    }

    public TokenAcceso GenerarToken(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var ahora = _reloj.AhoraUtc;
        var expira = ahora.AddMinutes(_opciones.MinutosExpiracion);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email.Valor),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("nombre", usuario.Nombre),
            new("email_verificado", usuario.EmailVerificado ? "true" : "false"),
        };

        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.ClaveSecreta));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: expira.UtcDateTime,
            signingCredentials: credenciales);

        var textoToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenAcceso(textoToken, expira);
    }
}

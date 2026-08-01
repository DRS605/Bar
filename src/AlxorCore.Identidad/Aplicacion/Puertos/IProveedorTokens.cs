using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>Token de acceso emitido para un usuario autenticado.</summary>
public sealed record TokenAcceso(string Token, DateTimeOffset ExpiraEn);

/// <summary>
/// Puerto de emisión de tokens de acceso (JWT). La firma y la configuración concretas viven
/// en la infraestructura.
/// </summary>
public interface IProveedorTokens
{
    /// <summary>Genera un token de acceso para el usuario indicado.</summary>
    TokenAcceso GenerarToken(Usuario usuario);
}

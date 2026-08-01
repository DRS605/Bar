namespace AlxorCore.Nucleo.Seguridad;

/// <summary>Identidad mínima de un usuario necesaria para emitir un token, sin acoplarse al agregado.</summary>
public sealed record IdentidadUsuario(Guid Id, string Email, string Nombre, bool EmailVerificado);

/// <summary>
/// Alcance de empresa que se incrusta en el token cuando el usuario opera dentro de una empresa:
/// la empresa activa, su rol y los permisos concedidos. Otros módulos lo usan para autorizar.
/// </summary>
public sealed record AlcanceEmpresa(Guid EmpresaId, string RolCodigo, IReadOnlyCollection<string> Permisos);

/// <summary>Token de acceso emitido para un usuario autenticado.</summary>
public sealed record TokenAcceso(string Token, DateTimeOffset ExpiraEn);

/// <summary>
/// Puerto de emisión de tokens de acceso (JWT). La firma y la configuración concretas viven
/// en la infraestructura. El token puede incluir opcionalmente el alcance de una empresa.
/// </summary>
public interface IProveedorTokens
{
    /// <summary>Genera un token para el usuario, opcionalmente con el alcance de una empresa activa.</summary>
    TokenAcceso GenerarToken(IdentidadUsuario usuario, AlcanceEmpresa? alcance = null);
}

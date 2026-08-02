namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>Vista ligera de un usuario para listados y composición entre módulos.</summary>
public sealed record UsuarioResumen(Guid Id, string Email, string Nombre, bool EmailVerificado);

/// <summary>
/// Consultas de lectura de usuarios que otros módulos (p. ej. Organización, al listar los miembros
/// de una empresa) necesitan sin acceder al agregado. La aplicación solo conoce este contrato.
/// </summary>
public interface IConsultaUsuarios
{
    /// <summary>Resumen del usuario con ese correo, o <c>null</c> si no existe.</summary>
    Task<UsuarioResumen?> ObtenerResumenPorEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Resúmenes de los usuarios cuyos identificadores se indican.</summary>
    Task<IReadOnlyList<UsuarioResumen>> ListarResumenesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

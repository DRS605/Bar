using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>
/// Puerto de acceso al almacén de usuarios. La implementación (EF Core) vive en la capa de
/// infraestructura; la aplicación solo conoce este contrato.
/// </summary>
public interface IRepositorioUsuarios
{
    /// <summary>Busca un usuario por su correo, o <c>null</c> si no existe.</summary>
    Task<Usuario?> ObtenerPorEmailAsync(Email email, CancellationToken ct = default);

    /// <summary>Busca un usuario por su identificador, o <c>null</c> si no existe.</summary>
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Indica si ya existe un usuario con ese correo.</summary>
    Task<bool> ExisteEmailAsync(Email email, CancellationToken ct = default);

    /// <summary>Añade un usuario nuevo al contexto de persistencia (no confirma la transacción).</summary>
    void Agregar(Usuario usuario);
}

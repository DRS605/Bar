using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Tests.Dobles;

/// <summary>Repositorio de usuarios en memoria para tests de aplicación.</summary>
public sealed class FakeRepositorioUsuarios : IRepositorioUsuarios
{
    private readonly List<Usuario> _usuarios = [];

    public IReadOnlyList<Usuario> Usuarios => _usuarios;

    public Task<Usuario?> ObtenerPorEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u => u.Email == email));

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u => u.Id == id));

    public Task<Usuario?> ObtenerPorTokenVerificacionAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u => u.TokenVerificacionHash == tokenHash));

    public Task<Usuario?> ObtenerPorTokenRestablecimientoAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u => u.TokenRestablecimientoHash == tokenHash));

    public Task<bool> ExisteEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.Any(u => u.Email == email));

    public void Agregar(Usuario usuario) => _usuarios.Add(usuario);
}

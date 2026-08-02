using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Identidad.Infraestructura.Persistencia;

/// <summary>Implementación con EF Core del repositorio de usuarios.</summary>
internal sealed class RepositorioUsuarios : IRepositorioUsuarios
{
    private readonly IdentidadDbContext _contexto;

    public RepositorioUsuarios(IdentidadDbContext contexto) => _contexto = contexto;

    public Task<Usuario?> ObtenerPorEmailAsync(Email email, CancellationToken ct = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<Usuario?> ObtenerPorTokenVerificacionAsync(string tokenHash, CancellationToken ct = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(u => u.TokenVerificacionHash == tokenHash, ct);

    public Task<Usuario?> ObtenerPorTokenRestablecimientoAsync(string tokenHash, CancellationToken ct = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(u => u.TokenRestablecimientoHash == tokenHash, ct);

    public Task<bool> ExisteEmailAsync(Email email, CancellationToken ct = default) =>
        _contexto.Usuarios.AnyAsync(u => u.Email == email, ct);

    public void Agregar(Usuario usuario) => _contexto.Usuarios.Add(usuario);
}

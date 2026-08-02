using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Identidad.Infraestructura.Persistencia;

/// <summary>Consultas de lectura de usuarios (resúmenes) sobre el contexto de Identidad.</summary>
internal sealed class ConsultaUsuarios : IConsultaUsuarios
{
    private readonly IdentidadDbContext _contexto;

    public ConsultaUsuarios(IdentidadDbContext contexto) => _contexto = contexto;

    public async Task<UsuarioResumen?> ObtenerResumenPorEmailAsync(string email, CancellationToken ct = default)
    {
        var creado = Email.Crear(email);
        if (creado.EsFallo)
        {
            return null;
        }

        return await _contexto.Usuarios
            .Where(u => u.Email == creado.Valor)
            .Select(u => new UsuarioResumen(u.Id, u.Email.Valor, u.Nombre, u.EmailVerificado))
            .SingleOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UsuarioResumen>> ListarResumenesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        return await _contexto.Usuarios
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UsuarioResumen(u.Id, u.Email.Valor, u.Nombre, u.EmailVerificado))
            .ToListAsync(ct).ConfigureAwait(false);
    }
}

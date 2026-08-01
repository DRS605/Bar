using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Identidad.Infraestructura.Persistencia;

/// <summary>
/// Contexto de persistencia del módulo Identidad. En el monolito modular cada módulo posee su
/// propio <see cref="DbContext"/> y su propio conjunto de tablas dentro de la base de datos
/// compartida, sin acceder a las tablas de otros módulos.
/// El usuario es una entidad global (no multiempresa), por lo que hereda de la base sin filtro
/// por empresa.
/// </summary>
public sealed class IdentidadDbContext : DbContextBase, AlxorCore.Identidad.Aplicacion.Puertos.IUnidadDeTrabajoIdentidad
{
    public IdentidadDbContext(DbContextOptions<IdentidadDbContext> opciones, IPublicadorEventos publicadorEventos)
        : base(opciones, publicadorEventos)
    {
    }

    /// <summary>Esquema propio del módulo dentro de la base de datos compartida.</summary>
    public const string Esquema = "identidad";

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentidadDbContext).Assembly);
    }
}

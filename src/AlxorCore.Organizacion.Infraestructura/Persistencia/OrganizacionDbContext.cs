using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Organizacion.Dominio;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Organizacion.Infraestructura.Persistencia;

/// <summary>
/// Contexto de persistencia del módulo Organización. <see cref="SerieNumeracion"/> es dato
/// multiempresa (filtrado por empresa); <see cref="Empresa"/> y <see cref="Membresia"/> son las
/// tablas que definen el tenant y su acceso, por lo que no se filtran por empresa.
/// </summary>
public sealed class OrganizacionDbContext : DbContextEmpresaBase, AlxorCore.Organizacion.Aplicacion.Puertos.IUnidadDeTrabajoOrganizacion
{
    public OrganizacionDbContext(
        DbContextOptions<OrganizacionDbContext> opciones,
        IPublicadorEventos publicadorEventos,
        IContextoEmpresa contextoEmpresa)
        : base(opciones, publicadorEventos, contextoEmpresa)
    {
    }

    public const string Esquema = "organizacion";

    public DbSet<Empresa> Empresas => Set<Empresa>();

    public DbSet<Membresia> Membresias => Set<Membresia>();

    public DbSet<SerieNumeracion> Series => Set<SerieNumeracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizacionDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

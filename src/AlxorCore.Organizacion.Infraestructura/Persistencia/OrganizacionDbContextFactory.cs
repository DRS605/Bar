using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlxorCore.Organizacion.Infraestructura.Persistencia;

/// <summary>Factoría en tiempo de diseño para las herramientas de EF Core (crear migraciones).</summary>
public sealed class OrganizacionDbContextFactory : IDesignTimeDbContextFactory<OrganizacionDbContext>
{
    public OrganizacionDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";

        var opciones = new DbContextOptionsBuilder<OrganizacionDbContext>()
            .UseNpgsql(conexion)
            .Options;

        return new OrganizacionDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
    }

    private sealed class PublicadorInactivo : IPublicadorEventos
    {
        public Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ContextoVacio : IContextoEmpresa
    {
        public Guid? EmpresaId => null;
    }
}

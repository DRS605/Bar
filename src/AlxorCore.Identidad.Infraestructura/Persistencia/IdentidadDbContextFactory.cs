using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlxorCore.Identidad.Infraestructura.Persistencia;

/// <summary>
/// Factoría en tiempo de diseño usada por las herramientas de EF Core (<c>dotnet ef</c>) para
/// crear migraciones sin arrancar la aplicación. La cadena de conexión se toma de la variable
/// de entorno <c>ALXOR_MIGRACIONES_CONEXION</c> o, en su defecto, de un valor local por defecto.
/// </summary>
public sealed class IdentidadDbContextFactory : IDesignTimeDbContextFactory<IdentidadDbContext>
{
    public IdentidadDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";

        var opciones = new DbContextOptionsBuilder<IdentidadDbContext>()
            .UseNpgsql(conexion)
            .Options;

        return new IdentidadDbContext(opciones, new PublicadorEventosInactivo());
    }

    /// <summary>Publicador vacío: en tiempo de diseño no se publican eventos.</summary>
    private sealed class PublicadorEventosInactivo : IPublicadorEventos
    {
        public Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}

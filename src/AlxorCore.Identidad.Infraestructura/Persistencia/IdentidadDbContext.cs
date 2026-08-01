using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Identidad.Infraestructura.Persistencia;

/// <summary>
/// Contexto de persistencia del módulo Identidad. En el monolito modular cada módulo posee su
/// propio <see cref="DbContext"/> y su propio conjunto de tablas dentro de la base de datos
/// compartida, sin acceder a las tablas de otros módulos.
/// Actúa además como Unidad de Trabajo: al guardar, confirma los cambios y publica los eventos
/// de dominio acumulados por los agregados.
/// </summary>
public sealed class IdentidadDbContext : DbContext, IUnidadDeTrabajo
{
    private readonly IPublicadorEventos _publicadorEventos;

    public IdentidadDbContext(DbContextOptions<IdentidadDbContext> opciones, IPublicadorEventos publicadorEventos)
        : base(opciones)
    {
        _publicadorEventos = publicadorEventos;
    }

    /// <summary>Esquema propio del módulo dentro de la base de datos compartida.</summary>
    public const string Esquema = "identidad";

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public async Task<int> GuardarCambiosAsync(CancellationToken ct = default)
    {
        var agregados = ChangeTracker
            .Entries<RaizAgregado<Guid>>()
            .Where(e => e.Entity.EventosDominio.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var eventos = agregados.SelectMany(a => a.EventosDominio).ToList();

        var filas = await SaveChangesAsync(ct).ConfigureAwait(false);

        if (eventos.Count > 0)
        {
            await _publicadorEventos.PublicarAsync(eventos, ct).ConfigureAwait(false);
            foreach (var agregado in agregados)
            {
                agregado.LimpiarEventos();
            }
        }

        return filas;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentidadDbContext).Assembly);
    }
}

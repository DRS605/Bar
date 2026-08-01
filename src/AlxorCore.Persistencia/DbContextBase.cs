using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Persistencia;

/// <summary>
/// Base común de los <see cref="DbContext"/> de los módulos. Actúa como Unidad de Trabajo:
/// al guardar, confirma los cambios y publica los eventos de dominio acumulados por los
/// agregados dentro del mismo flujo, dejando negocio y auditoría consistentes.
/// </summary>
public abstract class DbContextBase : DbContext, IUnidadDeTrabajo
{
    private readonly IPublicadorEventos _publicadorEventos;

    protected DbContextBase(DbContextOptions opciones, IPublicadorEventos publicadorEventos)
        : base(opciones)
    {
        _publicadorEventos = publicadorEventos;
    }

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
}

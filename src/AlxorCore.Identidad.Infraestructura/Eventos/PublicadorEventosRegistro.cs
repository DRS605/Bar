using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Identidad.Infraestructura.Eventos;

/// <summary>
/// Publicador de eventos provisional que los registra en el log. Cuando exista el módulo de
/// Auditoría, se sustituirá por un despachador que escriba en <c>registro_auditoria</c> y
/// alimente las integraciones, sin tocar los módulos que emiten los eventos.
/// </summary>
internal sealed class PublicadorEventosRegistro : IPublicadorEventos
{
    private readonly ILogger<PublicadorEventosRegistro> _log;

    public PublicadorEventosRegistro(ILogger<PublicadorEventosRegistro> log) => _log = log;

    public Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        foreach (var evento in eventos)
        {
            _log.LogInformation("Evento de dominio: {Evento}", evento.GetType().Name);
        }

        return Task.CompletedTask;
    }
}

using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Nucleo.Aplicacion;

/// <summary>
/// Publica los eventos de dominio acumulados por los agregados tras persistir. El módulo de
/// Auditoría (y, en el futuro, las integraciones con otros productos ALXOR) se suscribirán a
/// través de este puerto sin acoplarse a los módulos que emiten los eventos.
/// </summary>
public interface IPublicadorEventos
{
    Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default);
}

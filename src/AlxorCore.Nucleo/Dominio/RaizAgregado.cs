namespace AlxorCore.Nucleo.Dominio;

/// <summary>
/// Raíz de un agregado: la única entidad por la que se accede al agregado y la responsable
/// de mantener sus invariantes. Acumula eventos de dominio que se publican tras persistir.
/// </summary>
/// <typeparam name="TId">Tipo del identificador.</typeparam>
public abstract class RaizAgregado<TId> : EntidadBase<TId>
    where TId : notnull
{
    private readonly List<IEventoDominio> _eventos = [];

    protected RaizAgregado(TId id)
        : base(id)
    {
    }

    /// <summary>Eventos de dominio pendientes de publicar.</summary>
    public IReadOnlyCollection<IEventoDominio> EventosDominio => _eventos.AsReadOnly();

    /// <summary>Registra un evento de dominio para su publicación posterior.</summary>
    protected void RegistrarEvento(IEventoDominio evento) => _eventos.Add(evento);

    /// <summary>Vacía la lista de eventos (se invoca tras publicarlos).</summary>
    public void LimpiarEventos() => _eventos.Clear();
}

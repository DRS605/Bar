namespace AlxorCore.Nucleo.Dominio;

/// <summary>
/// Base de todas las entidades del dominio. La identidad se define por el <see cref="Id"/>,
/// no por sus atributos: dos entidades con el mismo identificador son la misma entidad.
/// </summary>
/// <typeparam name="TId">Tipo del identificador (habitualmente <see cref="Guid"/>).</typeparam>
public abstract class EntidadBase<TId>
    where TId : notnull
{
    protected EntidadBase(TId id)
    {
        Id = id;
    }

    /// <summary>Identificador único de la entidad.</summary>
    public TId Id { get; protected init; }

    public override bool Equals(object? obj)
        => obj is EntidadBase<TId> otra && otra.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(otra.Id, Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}

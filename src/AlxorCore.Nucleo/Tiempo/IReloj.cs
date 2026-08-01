namespace AlxorCore.Nucleo.Tiempo;

/// <summary>
/// Abstracción del tiempo actual. Inyectarla (en lugar de usar <c>DateTimeOffset.UtcNow</c>
/// directamente) mantiene el dominio determinista y testeable.
/// </summary>
public interface IReloj
{
    /// <summary>Instante actual en UTC.</summary>
    DateTimeOffset AhoraUtc { get; }
}

/// <summary>Reloj del sistema basado en el reloj real de la máquina.</summary>
public sealed class RelojSistema : IReloj
{
    public DateTimeOffset AhoraUtc => DateTimeOffset.UtcNow;
}

using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Tests.Dobles;

/// <summary>Reloj determinista para los tests.</summary>
public sealed class RelojFijo : IReloj
{
    public RelojFijo(DateTimeOffset ahora) => AhoraUtc = ahora;

    public DateTimeOffset AhoraUtc { get; set; }

    public static RelojFijo Predeterminado() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
}

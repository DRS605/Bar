using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Temas visuales disponibles para la carta pública del cliente.</summary>
public static class TemasCarta
{
    /// <summary>Tema por defecto.</summary>
    public const string PorDefecto = "verde";

    /// <summary>Temas admitidos (identificador → se aplica en la carta del cliente).</summary>
    public static readonly IReadOnlyList<string> Validos = new[]
    {
        "verde",      // Verde bistró (por defecto)
        "noche",      // Oscuro elegante
        "terracota",  // Cálido mediterráneo
        "marino",     // Azul marino
        "vino",       // Burdeos / bar de vinos
        "minimal",    // Minimalista blanco y negro
    };

    /// <summary>Normaliza un identificador de tema: si no es válido, devuelve el de por defecto.</summary>
    public static string Normalizar(string? tema)
    {
        var t = (tema ?? string.Empty).Trim().ToLowerInvariant();
        return Validos.Contains(t) ? t : PorDefecto;
    }
}

/// <summary>
/// Configuración de la carta pública de un local (una por empresa). De momento, el <see cref="Tema"/>
/// visual con el que el cliente ve la carta en su móvil.
/// </summary>
public sealed class ConfiguracionCarta : RaizAgregadoEmpresa<Guid>
{
    private ConfiguracionCarta(Guid id)
        : base(id, Guid.Empty)
    {
        Tema = null!;
    }

    private ConfiguracionCarta(Guid id, Guid empresaId, string tema, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Tema = tema;
        ActualizadaEn = ahora;
    }

    /// <summary>Identificador del tema visual de la carta (ver <see cref="TemasCarta"/>).</summary>
    public string Tema { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public static ConfiguracionCarta Crear(Guid empresaId, string? tema, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new ConfiguracionCarta(Guid.NewGuid(), empresaId, TemasCarta.Normalizar(tema), reloj.AhoraUtc);
    }

    /// <summary>Cambia el tema visual de la carta (se normaliza a uno válido).</summary>
    public void FijarTema(string? tema, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Tema = TemasCarta.Normalizar(tema);
        ActualizadaEn = reloj.AhoraUtc;
    }
}

using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>A qué se refiere una traducción de la carta.</summary>
public enum AmbitoTraduccion
{
    /// <summary>Nombre (y descripción) de un producto; la clave es el id del producto.</summary>
    Producto = 1,

    /// <summary>Nombre de una categoría; la clave es el nombre de la categoría en español.</summary>
    Categoria = 2,
}

/// <summary>
/// Traducción de un texto de la carta (nombre de un producto, su descripción, o el nombre de una
/// categoría) a un <see cref="IdiomaCarta"/>. El español es el idioma base (se toma del catálogo);
/// aquí se guardan solo las traducciones a inglés y francés que el local añade.
/// </summary>
public sealed class TraduccionCarta : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const int LongitudMaximaDescripcion = 500;
    public const int LongitudMaximaClave = 80;

    private TraduccionCarta(Guid id)
        : base(id, Guid.Empty)
    {
        Clave = null!;
        Nombre = null!;
    }

    private TraduccionCarta(Guid id, Guid empresaId, AmbitoTraduccion ambito, string clave, IdiomaCarta idioma, string nombre, string? descripcion, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Ambito = ambito;
        Clave = clave;
        Idioma = idioma;
        Nombre = nombre;
        Descripcion = descripcion;
        ActualizadaEn = ahora;
    }

    /// <summary>Si la traducción es de un producto o de una categoría.</summary>
    public AmbitoTraduccion Ambito { get; private set; }

    /// <summary>Clave del elemento traducido: id de producto (Producto) o nombre de categoría (Categoria).</summary>
    public string Clave { get; private set; }

    /// <summary>Idioma de esta traducción.</summary>
    public IdiomaCarta Idioma { get; private set; }

    /// <summary>Texto traducido (nombre del producto o de la categoría).</summary>
    public string Nombre { get; private set; }

    /// <summary>Descripción traducida (solo para productos; opcional).</summary>
    public string? Descripcion { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public static TraduccionCarta Crear(Guid empresaId, AmbitoTraduccion ambito, string clave, IdiomaCarta idioma, string nombre, string? descripcion, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new TraduccionCarta(Guid.NewGuid(), empresaId, ambito, Recortar(clave, LongitudMaximaClave)!, idioma,
            Recortar(nombre, LongitudMaximaNombre)!, Recortar(descripcion, LongitudMaximaDescripcion), reloj.AhoraUtc);
    }

    /// <summary>Cambia el texto traducido.</summary>
    public void Actualizar(string nombre, string? descripcion, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Nombre = Recortar(nombre, LongitudMaximaNombre)!;
        Descripcion = Recortar(descripcion, LongitudMaximaDescripcion);
        ActualizadaEn = reloj.AhoraUtc;
    }

    private static string? Recortar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        valor = valor.Trim();
        return valor.Length > max ? valor[..max] : valor;
    }
}

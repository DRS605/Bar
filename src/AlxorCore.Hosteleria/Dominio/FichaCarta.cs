using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>
/// Los 14 alérgenos de declaración obligatoria (Reglamento UE 1169/2011, anexo II). Es un enum de
/// banderas: una ficha puede tener varios.
/// </summary>
[Flags]
public enum Alergeno
{
    Ninguno = 0,
    Gluten = 1,
    Crustaceos = 1 << 1,
    Huevos = 1 << 2,
    Pescado = 1 << 3,
    Cacahuetes = 1 << 4,
    Soja = 1 << 5,
    Lacteos = 1 << 6,
    FrutosCascara = 1 << 7,
    Apio = 1 << 8,
    Mostaza = 1 << 9,
    Sesamo = 1 << 10,
    Sulfitos = 1 << 11,
    Altramuces = 1 << 12,
    Moluscos = 1 << 13,
}

/// <summary>Utilidades para convertir el conjunto de alérgenos entre banderas y nombres.</summary>
public static class Alergenos
{
    /// <summary>Todos los alérgenos declarables, en orden del anexo II.</summary>
    public static readonly IReadOnlyList<Alergeno> Todos = new[]
    {
        Alergeno.Gluten, Alergeno.Crustaceos, Alergeno.Huevos, Alergeno.Pescado, Alergeno.Cacahuetes,
        Alergeno.Soja, Alergeno.Lacteos, Alergeno.FrutosCascara, Alergeno.Apio, Alergeno.Mostaza,
        Alergeno.Sesamo, Alergeno.Sulfitos, Alergeno.Altramuces, Alergeno.Moluscos,
    };

    /// <summary>Descompone las banderas en la lista de nombres canónicos (el nombre del enum).</summary>
    public static IReadOnlyList<string> ANombres(Alergeno valor) =>
        Todos.Where(a => valor.HasFlag(a)).Select(a => a.ToString()).ToList();

    /// <summary>Recompone las banderas a partir de una lista de nombres (ignora los desconocidos).</summary>
    public static Alergeno DeNombres(IEnumerable<string>? nombres)
    {
        var valor = Alergeno.Ninguno;
        if (nombres is null)
        {
            return valor;
        }

        foreach (var nombre in nombres)
        {
            if (Enum.TryParse<Alergeno>(nombre?.Trim(), ignoreCase: true, out var a) && a != Alergeno.Ninguno)
            {
                valor |= a;
            }
        }

        return valor;
    }
}

/// <summary>
/// Ficha de carta de un producto: sus <see cref="Alergenos"/> y una <see cref="Foto"/> para la carta
/// pública. Es información de presentación de la carta (no fiscal), por eso vive en Hostelería y no en
/// el catálogo. Una por producto y empresa.
/// </summary>
public sealed class FichaCarta : RaizAgregadoEmpresa<Guid>
{
    /// <summary>Tamaño máximo de la foto (bytes). El cliente la reduce antes de subirla.</summary>
    public const int TamanoMaximoFoto = 3 * 1024 * 1024;

    private static readonly string[] TiposFotoValidos = { "image/jpeg", "image/png", "image/webp" };

    private FichaCarta(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private FichaCarta(Guid id, Guid empresaId, Guid productoId, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ProductoId = productoId;
        Alergenos = Alergeno.Ninguno;
        ActualizadaEn = ahora;
    }

    /// <summary>Producto al que pertenece la ficha.</summary>
    public Guid ProductoId { get; private set; }

    /// <summary>Alérgenos del producto (banderas).</summary>
    public Alergeno Alergenos { get; private set; }

    /// <summary>Imagen del producto para la carta (bytes), o null si no tiene.</summary>
    public byte[]? Foto { get; private set; }

    /// <summary>Tipo MIME de la foto (image/jpeg, image/png, image/webp).</summary>
    public string? FotoTipo { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    /// <summary>Si la ficha tiene una foto guardada.</summary>
    public bool TieneFoto => Foto is { Length: > 0 };

    public static FichaCarta Crear(Guid empresaId, Guid productoId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new FichaCarta(Guid.NewGuid(), empresaId, productoId, reloj.AhoraUtc);
    }

    public void FijarAlergenos(Alergeno alergenos, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Alergenos = alergenos;
        ActualizadaEn = reloj.AhoraUtc;
    }

    /// <summary>Fija (o cambia) la foto del producto. Valida el tipo y el tamaño.</summary>
    public Resultado FijarFoto(byte[] datos, string tipo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (datos is null || datos.Length == 0)
        {
            return Resultado.Fallo(Error.Validacion("ficha.foto_vacia", "La imagen está vacía."));
        }

        if (datos.Length > TamanoMaximoFoto)
        {
            return Resultado.Fallo(Error.Validacion("ficha.foto_grande", "La imagen es demasiado grande (máximo 3 MB)."));
        }

        var tipoNormalizado = (tipo ?? string.Empty).Trim().ToLowerInvariant();
        if (!TiposFotoValidos.Contains(tipoNormalizado))
        {
            return Resultado.Fallo(Error.Validacion("ficha.foto_tipo", "El formato de imagen no es válido (usa JPG, PNG o WebP)."));
        }

        Foto = datos;
        FotoTipo = tipoNormalizado;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Quita la foto del producto.</summary>
    public void QuitarFoto(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Foto = null;
        FotoTipo = null;
        ActualizadaEn = reloj.AhoraUtc;
    }
}

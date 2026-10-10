using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>A qué se aplica una promoción.</summary>
public enum AmbitoPromocion
{
    /// <summary>A toda la carta.</summary>
    Todo = 1,

    /// <summary>A una categoría de productos.</summary>
    Categoria = 2,

    /// <summary>A un producto concreto.</summary>
    Producto = 3,
}

/// <summary>
/// Promoción de descuento: un porcentaje que se aplica a toda la carta, a una categoría o a un
/// producto, opcionalmente limitado a ciertos días de la semana y a una franja horaria (happy hour).
/// Se aplica sola al añadir el producto a la comanda.
/// </summary>
public sealed class Promocion : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 80;

    private Promocion(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Promocion(Guid id, Guid empresaId, string nombre, decimal porcentaje, AmbitoPromocion ambito, string? categoria, Guid? productoId, string? dias, TimeOnly? horaInicio, TimeOnly? horaFin, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        Porcentaje = porcentaje;
        Ambito = ambito;
        Categoria = categoria;
        ProductoId = productoId;
        Dias = dias;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        Activa = true;
        CreadaEn = ahora;
    }

    public string Nombre { get; private set; }

    /// <summary>Descuento aplicado, en porcentaje (0–100).</summary>
    public decimal Porcentaje { get; private set; }

    public AmbitoPromocion Ambito { get; private set; }

    /// <summary>Categoría a la que aplica (si <see cref="Ambito"/> es Categoria).</summary>
    public string? Categoria { get; private set; }

    /// <summary>Producto al que aplica (si <see cref="Ambito"/> es Producto).</summary>
    public Guid? ProductoId { get; private set; }

    /// <summary>Días de la semana (0=domingo..6=sábado) separados por comas; vacío/nulo = todos los días.</summary>
    public string? Dias { get; private set; }

    /// <summary>Inicio de la franja horaria (happy hour); nulo = todo el día.</summary>
    public TimeOnly? HoraInicio { get; private set; }

    /// <summary>Fin de la franja horaria; nulo = todo el día.</summary>
    public TimeOnly? HoraFin { get; private set; }

    public bool Activa { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public static Resultado<Promocion> Crear(Guid empresaId, string? nombre, decimal porcentaje, AmbitoPromocion ambito, string? categoria, Guid? productoId, string? dias, TimeOnly? horaInicio, TimeOnly? horaFin, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var n = (nombre ?? string.Empty).Trim();
        if (n.Length == 0)
        {
            return Resultado.Fallo<Promocion>(Error.Validacion("promocion.sin_nombre", "La promoción necesita un nombre."));
        }

        if (porcentaje is <= 0m or > 100m)
        {
            return Resultado.Fallo<Promocion>(Error.Validacion("promocion.porcentaje_invalido", "El descuento debe estar entre 0 y 100 %."));
        }

        if (ambito == AmbitoPromocion.Categoria && string.IsNullOrWhiteSpace(categoria))
        {
            return Resultado.Fallo<Promocion>(Error.Validacion("promocion.sin_categoria", "Indica la categoría de la promoción."));
        }

        if (ambito == AmbitoPromocion.Producto && (productoId is null || productoId == Guid.Empty))
        {
            return Resultado.Fallo<Promocion>(Error.Validacion("promocion.sin_producto", "Indica el producto de la promoción."));
        }

        if (n.Length > LongitudMaximaNombre)
        {
            n = n[..LongitudMaximaNombre];
        }

        return Resultado.Ok(new Promocion(Guid.NewGuid(), empresaId, n, porcentaje, ambito,
            ambito == AmbitoPromocion.Categoria ? categoria!.Trim() : null,
            ambito == AmbitoPromocion.Producto ? productoId : null,
            string.IsNullOrWhiteSpace(dias) ? null : dias.Trim(), horaInicio, horaFin, reloj.AhoraUtc));
    }

    public void CambiarActiva(bool activa) => Activa = activa;

    /// <summary>Indica si la promoción aplica a un producto en un momento (día y hora locales) dados.</summary>
    public bool Aplica(string? categoriaProducto, Guid productoId, DayOfWeek dia, TimeOnly hora)
    {
        if (!Activa)
        {
            return false;
        }

        var ambitoOk = Ambito switch
        {
            AmbitoPromocion.Todo => true,
            AmbitoPromocion.Categoria => !string.IsNullOrWhiteSpace(categoriaProducto) && string.Equals(categoriaProducto, Categoria, StringComparison.OrdinalIgnoreCase),
            AmbitoPromocion.Producto => ProductoId == productoId,
            _ => false,
        };
        if (!ambitoOk)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Dias))
        {
            var dias = Dias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!dias.Contains(((int)dia).ToString()))
            {
                return false;
            }
        }

        if (HoraInicio is { } ini && HoraFin is { } fin)
        {
            var dentro = ini <= fin ? (hora >= ini && hora < fin) : (hora >= ini || hora < fin);
            if (!dentro)
            {
                return false;
            }
        }

        return true;
    }
}

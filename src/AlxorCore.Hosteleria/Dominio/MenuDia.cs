using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>
/// Un plato del menú del día, dentro de una sección (Primeros, Segundos, Postres…). Pertenece al
/// agregado <see cref="MenuDia"/>.
/// </summary>
public sealed class PlatoMenu : EntidadBase<Guid>
{
    public const int LongitudMaximaSeccion = 60;
    public const int LongitudMaximaNombre = 160;

    private PlatoMenu(Guid id)
        : base(id)
    {
        Seccion = null!;
        Nombre = null!;
    }

    internal PlatoMenu(Guid empresaId, Guid menuDiaId, string seccion, string nombre, int orden)
        : base(Guid.NewGuid())
    {
        EmpresaId = empresaId;
        MenuDiaId = menuDiaId;
        Seccion = Recortar(seccion, LongitudMaximaSeccion);
        Nombre = Recortar(nombre, LongitudMaximaNombre);
        Orden = orden;
    }

    /// <summary>Empresa (para el aislamiento multiempresa de la tabla de platos).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Menú del día al que pertenece el plato.</summary>
    public Guid MenuDiaId { get; private set; }

    /// <summary>Sección del plato (p. ej. «Primeros», «Segundos», «Postres»).</summary>
    public string Seccion { get; private set; }

    /// <summary>Nombre del plato (p. ej. «Ensalada mixta»).</summary>
    public string Nombre { get; private set; }

    /// <summary>Orden de aparición (para conservar el orden que puso el bar).</summary>
    public int Orden { get; private set; }

    private static string Recortar(string texto, int max)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length > max ? t[..max] : t;
    }
}

/// <summary>
/// Menú del día de un local (uno por empresa): precio cerrado, qué incluye (pan, bebida, postre…) y
/// los platos agrupados por secciones. El bar lo actualiza a diario y, si está <see cref="Activo"/>,
/// se muestra en la carta pública (QR) para que el cliente lo consulte desde la mesa.
/// </summary>
public sealed class MenuDia : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaIncluye = 200;
    public const decimal PrecioMaximo = 1000m;

    private readonly List<PlatoMenu> _platos = [];

    private MenuDia(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private MenuDia(Guid id, Guid empresaId, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Activo = false;
        Precio = 0m;
        ActualizadaEn = ahora;
    }

    /// <summary>Precio cerrado del menú (con IVA incluido), lo que paga el cliente.</summary>
    public decimal Precio { get; private set; }

    /// <summary>Si el menú del día se muestra en la carta pública.</summary>
    public bool Activo { get; private set; }

    /// <summary>Texto libre de lo que incluye («Pan, bebida y postre o café»). Opcional.</summary>
    public string? Incluye { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    /// <summary>Platos del menú, en el orden en que los puso el bar.</summary>
    public IReadOnlyList<PlatoMenu> Platos => _platos.AsReadOnly();

    public static MenuDia Crear(Guid empresaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new MenuDia(Guid.NewGuid(), empresaId, reloj.AhoraUtc);
    }

    /// <summary>
    /// Fija el contenido del menú del día. <paramref name="platos"/> llega ya ordenado (sección, nombre);
    /// los nombres vacíos se descartan. Falla si el precio es negativo o desorbitado.
    /// </summary>
    public Resultado Fijar(decimal precio, bool activo, string? incluye, IEnumerable<(string Seccion, string Nombre)> platos, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(platos);
        ArgumentNullException.ThrowIfNull(reloj);

        if (precio < 0m)
        {
            return Resultado.Fallo(Error.Validacion("menu_dia.precio_negativo", "El precio del menú no puede ser negativo."));
        }

        if (precio > PrecioMaximo)
        {
            return Resultado.Fallo(Error.Validacion("menu_dia.precio_excesivo", $"El precio del menú no puede superar {PrecioMaximo:0} €."));
        }

        Precio = precio;
        Activo = activo;
        var inc = (incluye ?? string.Empty).Trim();
        Incluye = inc.Length == 0 ? null : (inc.Length > LongitudMaximaIncluye ? inc[..LongitudMaximaIncluye] : inc);

        _platos.Clear();
        var orden = 0;
        foreach (var (seccion, nombre) in platos)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            _platos.Add(new PlatoMenu(EmpresaId, Id, string.IsNullOrWhiteSpace(seccion) ? "Menú" : seccion, nombre, orden++));
        }

        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }
}

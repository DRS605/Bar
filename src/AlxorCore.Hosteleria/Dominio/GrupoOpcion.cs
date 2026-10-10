using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Cómo se elige dentro de un grupo de opciones.</summary>
public enum SeleccionOpcion
{
    /// <summary>Se elige una sola (formato/tamaño: media/ración, caña/doble).</summary>
    Unica = 1,

    /// <summary>Se eligen varias o ninguna (extras: queso, bacon…).</summary>
    Multiple = 2,
}

/// <summary>Una opción concreta dentro de un grupo, con su suplemento (o descuento) sobre el precio base.</summary>
public sealed class OpcionProducto : EntidadBase<Guid>
{
    public const int LongitudMaximaNombre = 80;

    private OpcionProducto(Guid id)
        : base(id)
    {
        Nombre = null!;
    }

    internal OpcionProducto(Guid empresaId, Guid grupoOpcionId, string nombre, decimal precioDelta, int orden)
        : base(Guid.NewGuid())
    {
        EmpresaId = empresaId;
        GrupoOpcionId = grupoOpcionId;
        Nombre = Recortar(nombre);
        PrecioDelta = precioDelta;
        Orden = orden;
    }

    public Guid EmpresaId { get; private set; }

    public Guid GrupoOpcionId { get; private set; }

    public string Nombre { get; private set; }

    /// <summary>Suplemento (positivo) o descuento (negativo) sobre el precio base del producto.</summary>
    public decimal PrecioDelta { get; private set; }

    public int Orden { get; private set; }

    private static string Recortar(string? t)
    {
        var s = (t ?? string.Empty).Trim();
        return s.Length > LongitudMaximaNombre ? s[..LongitudMaximaNombre] : s;
    }
}

/// <summary>
/// Grupo de opciones de un producto (p. ej. «Tamaño» con media/ración, o «Extras» con queso/bacon).
/// Un producto puede tener varios grupos. Vive en Hostelería (no toca el catálogo compartido).
/// </summary>
public sealed class GrupoOpcion : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 80;

    private readonly List<OpcionProducto> _opciones = [];

    private GrupoOpcion(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private GrupoOpcion(Guid id, Guid empresaId, Guid productoId, string nombre, SeleccionOpcion seleccion, bool obligatorio, int orden)
        : base(id, empresaId)
    {
        ProductoId = productoId;
        Nombre = Recortar(nombre);
        Seleccion = seleccion;
        Obligatorio = obligatorio;
        Orden = orden;
    }

    public Guid ProductoId { get; private set; }

    public string Nombre { get; private set; }

    public SeleccionOpcion Seleccion { get; private set; }

    /// <summary>Si es obligatorio elegir una opción (solo tiene sentido en <see cref="SeleccionOpcion.Unica"/>).</summary>
    public bool Obligatorio { get; private set; }

    public int Orden { get; private set; }

    public IReadOnlyList<OpcionProducto> Opciones => _opciones.AsReadOnly();

    public static GrupoOpcion Crear(Guid empresaId, Guid productoId, string nombre, SeleccionOpcion seleccion, bool obligatorio, int orden)
    {
        var grupo = new GrupoOpcion(Guid.NewGuid(), empresaId, productoId, nombre, seleccion, obligatorio, orden);
        return grupo;
    }

    /// <summary>Añade una opción al grupo (los nombres vacíos se descartan).</summary>
    public void AgregarOpcion(string nombre, decimal precioDelta, int orden)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        _opciones.Add(new OpcionProducto(EmpresaId, Id, nombre, precioDelta, orden));
    }

    private static string Recortar(string? t)
    {
        var s = (t ?? string.Empty).Trim();
        return s.Length > LongitudMaximaNombre ? s[..LongitudMaximaNombre] : s;
    }
}

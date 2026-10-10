using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Zona de preparación de un producto: dónde se prepara/entrega.</summary>
public enum ZonaPreparacion
{
    /// <summary>Cocina (platos). Es la zona por defecto.</summary>
    Cocina = 1,

    /// <summary>Barra (bebidas).</summary>
    Barra = 2,
}

/// <summary>Utilidades de zona de preparación.</summary>
public static class ZonasPreparacion
{
    public const ZonaPreparacion PorDefecto = ZonaPreparacion.Cocina;

    public static ZonaPreparacion Normalizar(string? zona) =>
        Enum.TryParse<ZonaPreparacion>((zona ?? string.Empty).Trim(), ignoreCase: true, out var z) && Enum.IsDefined(z) ? z : PorDefecto;
}

/// <summary>
/// Asignación de la zona de preparación de un producto (Cocina/Barra). Determina a qué parte va la
/// comanda y en qué columna de la pantalla de cocina aparece. Si no hay asignación, es Cocina.
/// </summary>
public sealed class ZonaProducto : RaizAgregadoEmpresa<Guid>
{
    private ZonaProducto(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private ZonaProducto(Guid id, Guid empresaId, Guid productoId, ZonaPreparacion zona, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ProductoId = productoId;
        Zona = zona;
        ActualizadaEn = ahora;
    }

    public Guid ProductoId { get; private set; }

    public ZonaPreparacion Zona { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public static ZonaProducto Crear(Guid empresaId, Guid productoId, ZonaPreparacion zona, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        return new ZonaProducto(Guid.NewGuid(), empresaId, productoId, zona, reloj.AhoraUtc);
    }

    public void Fijar(ZonaPreparacion zona, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Zona = zona;
        ActualizadaEn = reloj.AhoraUtc;
    }
}

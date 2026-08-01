using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Catalogo.Aplicacion;

/// <summary>Vista de un producto (incluye el porcentaje de IVA resuelto del catálogo).</summary>
public sealed record ProductoDto(
    Guid Id,
    string? Referencia,
    string Nombre,
    TipoProducto Tipo,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    string Unidad,
    bool Activo)
{
    public static ProductoDto Desde(Producto p)
    {
        var porcentaje = Impuesto.PorCodigoImpuesto(p.CodigoIva).Valor.Porcentaje;
        return new ProductoDto(p.Id, p.Referencia, p.Nombre, p.Tipo, p.PrecioUnitario, p.CodigoIva, porcentaje, p.Unidad, p.Activo);
    }
}

/// <summary>Vista de un tipo de impuesto del catálogo.</summary>
public sealed record ImpuestoDto(string Codigo, string Nombre, TipoImpuesto Tipo, decimal Porcentaje)
{
    public static ImpuestoDto Desde(Impuesto i) => new(i.Codigo, i.Nombre, i.Tipo, i.Porcentaje);
}

/// <summary>Repositorio de productos (escritura).</summary>
public interface IRepositorioProductos
{
    Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Producto producto);
}

/// <summary>Consultas de lectura de productos (las usan la API y Facturación).</summary>
public interface IConsultaProductos
{
    Task<ProductoDto?> ObtenerAsync(Guid productoId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Catálogo.</summary>
public interface IUnidadDeTrabajoCatalogo : IUnidadDeTrabajo;

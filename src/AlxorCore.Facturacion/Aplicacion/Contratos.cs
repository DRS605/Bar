using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Vista de una línea de factura.</summary>
public sealed record LineaFacturaDto(
    string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal PorcentajeDescuento,
    string CodigoIva, decimal PorcentajeIva, decimal Base, decimal CuotaIva);

/// <summary>Vista de una factura.</summary>
public sealed record FacturaDto(
    Guid Id,
    string NumeroCompleto,
    DateOnly FechaEmision,
    DateOnly FechaOperacion,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteNif,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal PorcentajeIrpf,
    decimal RetencionIrpf,
    decimal Total,
    string Estado,
    IReadOnlyList<LineaFacturaDto> Lineas)
{
    public static FacturaDto Desde(Factura f) => new(
        f.Id, f.NumeroCompleto, f.FechaEmision, f.FechaOperacion, f.ClienteId, f.ClienteNombre, f.ClienteNif,
        f.BaseImponible, f.CuotaIva, f.PorcentajeIrpf, f.RetencionIrpf, f.Total, f.Estado.ToString(),
        f.Lineas.Select(l => new LineaFacturaDto(
            l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeDescuento, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva)).ToList());
}

/// <summary>Resumen de factura para listados y libros de IVA.</summary>
public sealed record FacturaResumen(
    Guid Id, string NumeroCompleto, DateOnly FechaEmision, string ClienteNombre,
    string? ClienteNif, decimal BaseImponible, decimal CuotaIva, decimal Total, string Estado);

/// <summary>Repositorio de facturas (escritura).</summary>
public interface IRepositorioFacturas
{
    Task<Factura?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Factura factura);
}

/// <summary>Consultas de lectura de facturas (las usan la API, Tesorería e Informes).</summary>
public interface IConsultaFacturas
{
    Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default);

    Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Facturación.</summary>
public interface IUnidadDeTrabajoFacturacion : IUnidadDeTrabajo;

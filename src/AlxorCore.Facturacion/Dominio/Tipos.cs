using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Facturacion.Dominio;

/// <summary>Estado (fiscal) de una factura. El estado de cobro lo gestiona el módulo Tesorería.</summary>
public enum EstadoFactura
{
    /// <summary>Emitida: inmutable, con número asignado.</summary>
    Emitida = 1,

    /// <summary>Anulada mediante una factura rectificativa total.</summary>
    Anulada = 2,
}

/// <summary>Tipo de factura.</summary>
public enum TipoFactura
{
    Ordinaria = 1,
    Rectificativa = 2,
}

/// <summary>Datos del cliente que se "congelan" en la factura al emitirla (invariante fiscal F4).</summary>
public sealed record ClienteFacturado(
    Guid ClienteId,
    string Nombre,
    string? Nif,
    string Calle,
    string CodigoPostal,
    string Poblacion,
    string Provincia,
    string Pais);

/// <summary>Datos de una línea nueva al emitir (entrada del caso de uso).</summary>
public sealed record NuevaLinea(
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null);

/// <summary>Se ha emitido una factura.</summary>
public sealed record FacturaEmitida(
    Guid FacturaId,
    Guid EmpresaId,
    string NumeroCompleto,
    decimal Total,
    DateTimeOffset OcurridoEn) : IEventoDominio;

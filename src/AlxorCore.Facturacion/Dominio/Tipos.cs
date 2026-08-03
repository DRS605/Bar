using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Facturacion.Dominio;

/// <summary>Estado (fiscal) de una factura. El estado de cobro lo gestiona el módulo Tesorería.</summary>
public enum EstadoFactura
{
    /// <summary>Emitida: inmutable, con número asignado.</summary>
    Emitida = 1,

    /// <summary>Anulada mediante una factura rectificativa total.</summary>
    Anulada = 2,

    /// <summary>Corregida por una factura rectificativa (por sustitución).</summary>
    Rectificada = 3,
}

/// <summary>Tipo de factura.</summary>
public enum TipoFactura
{
    Ordinaria = 1,
    Rectificativa = 2,

    /// <summary>Factura simplificada (ticket): sin datos completos del destinatario y con tope de importe.</summary>
    Simplificada = 3,
}

/// <summary>
/// Datos del cliente que se "congelan" en la factura al emitirla (invariante fiscal F4). En una
/// factura simplificada (ticket) el destinatario puede no estar identificado; en ese caso
/// <see cref="ClienteId"/> es nulo y el nombre es genérico ("Cliente de contado").
/// </summary>
public sealed record ClienteFacturado(
    Guid? ClienteId,
    string Nombre,
    string? Nif,
    string Calle,
    string CodigoPostal,
    string Poblacion,
    string Provincia,
    string Pais)
{
    /// <summary>Destinatario genérico para tickets sin cliente identificado.</summary>
    public static ClienteFacturado Contado { get; } =
        new(null, "Cliente de contado", null, string.Empty, string.Empty, string.Empty, string.Empty, "ES");
}

/// <summary>Datos de una línea nueva al emitir (entrada del caso de uso).</summary>
public sealed record NuevaLinea(
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null,
    decimal CosteUnitario = 0m,
    decimal PorcentajeRecargo = 0m);

/// <summary>Se ha emitido una factura.</summary>
public sealed record FacturaEmitida(
    Guid FacturaId,
    Guid EmpresaId,
    string NumeroCompleto,
    decimal Total,
    DateTimeOffset OcurridoEn) : IEventoDominio;

namespace AlxorCore.Organizacion.Dominio;

/// <summary>Régimen de IVA de la empresa (simplificado para el MVP).</summary>
public enum RegimenIva
{
    /// <summary>Régimen general del IVA.</summary>
    General = 1,

    /// <summary>Recargo de equivalencia.</summary>
    RecargoEquivalencia = 2,
}

/// <summary>Tipo de documento numerado por una serie.</summary>
public enum TipoDocumento
{
    /// <summary>Factura emitida.</summary>
    Factura = 1,
}

/// <summary>Estado de una membresía (usuario dentro de una empresa).</summary>
public enum EstadoMembresia
{
    /// <summary>Membresía activa.</summary>
    Activa = 1,

    /// <summary>Membresía revocada.</summary>
    Revocada = 2,
}

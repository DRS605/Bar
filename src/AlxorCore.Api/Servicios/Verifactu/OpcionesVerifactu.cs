namespace AlxorCore.Api.Servicios.Verifactu;

/// <summary>
/// Ajustes de la <b>remisión de registros a la AEAT</b> (VeriFactu). El envío usa autenticación mutua
/// con el <b>certificado</b> del obligado a emitir. Sin certificado (o con <see cref="Activo"/> a
/// falso) no se remite: los registros quedan generados localmente y la remisión responde
/// «no configurada». Los endpoints por defecto deben confirmarse contra la documentación vigente de la
/// AEAT antes de producción.
/// </summary>
public sealed class OpcionesVerifactu
{
    public const string Seccion = "VeriFactu";

    /// <summary>URL del servicio SOAP de preproducción (pruebas).</summary>
    public const string UrlPreproduccion = "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";

    /// <summary>URL del servicio SOAP de producción.</summary>
    public const string UrlProduccion = "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";

    /// <summary>¿Se remite a la AEAT? Si es falso, los registros solo se generan localmente.</summary>
    public bool Activo { get; set; }

    /// <summary>«Preproduccion» (pruebas) o «Produccion».</summary>
    public string Entorno { get; set; } = "Preproduccion";

    /// <summary>Ruta al certificado del obligado (PFX/PKCS#12).</summary>
    public string? CertificadoRuta { get; set; }

    /// <summary>Contraseña del certificado.</summary>
    public string? CertificadoClave { get; set; }

    /// <summary>URL del servicio (opcional; si se deja vacía se usa la de preproducción/producción).</summary>
    public string? EndpointUrl { get; set; }

    public bool EsProduccion => string.Equals(Entorno, "Produccion", StringComparison.OrdinalIgnoreCase);

    /// <summary>¿Está lista la remisión? (activa y con certificado).</summary>
    public bool Configurado => Activo && !string.IsNullOrWhiteSpace(CertificadoRuta);

    /// <summary>URL efectiva del servicio SOAP.</summary>
    public string UrlEfectiva => !string.IsNullOrWhiteSpace(EndpointUrl)
        ? EndpointUrl!
        : (EsProduccion ? UrlProduccion : UrlPreproduccion);
}

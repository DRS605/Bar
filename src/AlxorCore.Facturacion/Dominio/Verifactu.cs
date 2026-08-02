using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AlxorCore.Facturacion.Dominio;

/// <summary>
/// Utilidades del sistema <b>VeriFactu</b> (RD 1007/2023 y Orden HAC/1177/2024): cálculo de la
/// <b>huella</b> (hash SHA-256) del registro de alta de una factura y su <b>encadenamiento</b> con el
/// registro anterior, más la URL de cotejo para el código QR. La huella se calcula sobre una cadena
/// canónica de campos en un orden fijo, tal como establece la especificación técnica de la AEAT.
/// </summary>
/// <remarks>
/// Este núcleo activa la generación de la huella y el encadenamiento (los campos ya estaban
/// reservados en <see cref="Factura"/>). El <b>envío en vivo del registro a la AEAT</b> (servicio web
/// con certificado) es un paso posterior; aquí el registro queda generado y almacenado localmente.
/// </remarks>
public static class Verifactu
{
    /// <summary>URL base del servicio de cotejo de la AEAT que se codifica en el QR.</summary>
    public const string BaseUrlCotejo = "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR";

    /// <summary>Código de tipo de factura VeriFactu (F1 ordinaria, F2 simplificada/ticket, R1 rectificativa).</summary>
    public static string TipoCodigo(TipoFactura tipo) => tipo switch
    {
        TipoFactura.Simplificada => "F2",
        TipoFactura.Rectificativa => "R1",
        _ => "F1",
    };

    /// <summary>
    /// Calcula la huella (SHA-256, hex en mayúsculas) del registro de alta a partir de la cadena
    /// canónica <c>Clave=valor&amp;…</c> en el orden fijado por la AEAT, incluyendo la huella anterior
    /// (encadenamiento). Importes con 2 decimales y punto; fecha <c>dd-MM-yyyy</c>; instante ISO-8601 con huso.
    /// </summary>
    public static string CalcularHuella(
        string nifEmisor, string numSerie, DateOnly fechaExpedicion, string tipoFactura,
        decimal cuotaTotal, decimal importeTotal, string? huellaAnterior, DateTimeOffset generadoEn)
    {
        var cadena = string.Join("&",
            $"IDEmisorFactura={nifEmisor}",
            $"NumSerieFactura={numSerie}",
            $"FechaExpedicionFactura={fechaExpedicion.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}",
            $"TipoFactura={tipoFactura}",
            $"CuotaTotal={cuotaTotal.ToString("F2", CultureInfo.InvariantCulture)}",
            $"ImporteTotal={importeTotal.ToString("F2", CultureInfo.InvariantCulture)}",
            $"Huella={huellaAnterior ?? string.Empty}",
            $"FechaHoraHusoGenRegistro={generadoEn.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)}");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cadena)));
    }

    /// <summary>URL de cotejo de la AEAT que codifica el QR de una factura/ticket VeriFactu.</summary>
    public static string UrlCotejo(string nifEmisor, string numSerie, DateOnly fecha, decimal importeTotal) =>
        $"{BaseUrlCotejo}?nif={Uri.EscapeDataString(nifEmisor)}" +
        $"&numserie={Uri.EscapeDataString(numSerie)}" +
        $"&fecha={fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}" +
        $"&importe={importeTotal.ToString("F2", CultureInfo.InvariantCulture)}";
}

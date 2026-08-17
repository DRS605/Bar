using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using AlxorCore.Nucleo.Resultados;
using Microsoft.Extensions.Options;

namespace AlxorCore.Api.Servicios.Verifactu;

/// <summary>Resultado de remitir un registro a la AEAT: estado, CSV y, si falla, el error.</summary>
public sealed record ResultadoRemisionVerifactu(string Estado, string? Csv, string? CodigoError, string? DescripcionError);

/// <summary>Puerto de remisión de un registro de facturación VeriFactu al servicio web de la AEAT.</summary>
public interface IRemisorVerifactu
{
    /// <summary>¿Está configurada la remisión (activa y con certificado)?</summary>
    bool Configurado { get; }

    /// <summary>Remite el XML del registro de alta a la AEAT.</summary>
    Task<Resultado<ResultadoRemisionVerifactu>> RemitirAsync(string registroXml, CancellationToken ct = default);
}

/// <summary>
/// Compone el <b>sobre SOAP</b> alrededor del registro y <b>lee la respuesta</b> de la AEAT. Se aísla
/// aquí, sin red, para poder probarlo con tests unitarios.
/// </summary>
public static class SobreSoapVerifactu
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>Envuelve el registro (sin su declaración XML) en un sobre SOAP 1.1.</summary>
    public static string Construir(string registroXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(registroXml);
        var cuerpo = SinDeclaracion(registroXml);
        return $"<soapenv:Envelope xmlns:soapenv=\"{SoapNs}\"><soapenv:Header/><soapenv:Body>{cuerpo}</soapenv:Body></soapenv:Envelope>";
    }

    /// <summary>Extrae el estado del envío, el CSV y el primer error de la respuesta SOAP de la AEAT.</summary>
    public static ResultadoRemisionVerifactu LeerRespuesta(string soapXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(soapXml);
        var doc = XDocument.Parse(soapXml);
        string? Local(params string[] nombres) =>
            doc.Descendants().FirstOrDefault(e => nombres.Contains(e.Name.LocalName))?.Value;

        var estado = Local("EstadoEnvio", "EstadoRegistro") ?? "Desconocido";
        return new ResultadoRemisionVerifactu(
            estado,
            Local("CSV"),
            Local("CodigoErrorRegistro", "CodigoError"),
            Local("DescripcionErrorRegistro", "DescripcionError"));
    }

    private static string SinDeclaracion(string xml)
    {
        var fin = xml.IndexOf("?>", StringComparison.Ordinal);
        return fin >= 0 ? xml[(fin + 2)..].TrimStart() : xml;
    }
}

/// <summary>Remisión no configurada: no hay certificado, así que informa y no envía.</summary>
internal sealed class RemisorVerifactuNulo : IRemisorVerifactu
{
    public bool Configurado => false;

    public Task<Resultado<ResultadoRemisionVerifactu>> RemitirAsync(string registroXml, CancellationToken ct = default) =>
        Task.FromResult(Resultado.Fallo<ResultadoRemisionVerifactu>(Error.Validacion(
            "verifactu.no_configurado",
            "La remisión a la AEAT no está configurada. Rellena la sección «VeriFactu» con tu certificado, o descarga el registro en XML.")));
}

/// <summary>
/// Remite el registro al servicio SOAP de la AEAT con <b>autenticación mutua</b> (el certificado del
/// obligado). Se registra cuando la sección «VeriFactu» está activa y tiene certificado; si no, se usa
/// <see cref="RemisorVerifactuNulo"/>. El puerto <see cref="IRemisorVerifactu"/> no cambia.
/// </summary>
internal sealed class RemisorVerifactuAeat : IRemisorVerifactu
{
    private readonly OpcionesVerifactu _opciones;
    private readonly ILogger<RemisorVerifactuAeat> _log;

    public RemisorVerifactuAeat(IOptions<OpcionesVerifactu> opciones, ILogger<RemisorVerifactuAeat> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public bool Configurado => _opciones.Configurado;

    public async Task<Resultado<ResultadoRemisionVerifactu>> RemitirAsync(string registroXml, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(registroXml);

        try
        {
            using var certificado = new X509Certificate2(_opciones.CertificadoRuta!, _opciones.CertificadoClave);
            using var manejador = new HttpClientHandler();
            manejador.ClientCertificates.Add(certificado);

            using var cliente = new HttpClient(manejador);
            using var contenido = new StringContent(SobreSoapVerifactu.Construir(registroXml), Encoding.UTF8);
            contenido.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
            contenido.Headers.Add("SOAPAction", "\"\"");

            using var respuesta = await cliente.PostAsync(_opciones.UrlEfectiva, contenido, ct).ConfigureAwait(false);
            var texto = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!respuesta.IsSuccessStatusCode)
            {
                _log.LogWarning("La AEAT respondió {Codigo} al remitir el registro VeriFactu.", (int)respuesta.StatusCode);
                return Resultado.Fallo<ResultadoRemisionVerifactu>(Error.Conflicto(
                    "verifactu.rechazado", $"La AEAT respondió {(int)respuesta.StatusCode}."));
            }

            var resultado = SobreSoapVerifactu.LeerRespuesta(texto);
            _log.LogInformation("Registro VeriFactu remitido. Estado {Estado}, CSV {Csv}.", resultado.Estado, resultado.Csv);
            return Resultado.Ok(resultado);
        }
#pragma warning disable CA1031 // Cualquier fallo de E/S o del certificado se traduce a un error de negocio legible.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogError(ex, "No se pudo remitir el registro VeriFactu a la AEAT.");
            return Resultado.Fallo<ResultadoRemisionVerifactu>(Error.Conflicto(
                "verifactu.error_remision", $"No se pudo remitir a la AEAT: {ex.Message}"));
        }
    }
}

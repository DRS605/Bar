using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using AlxorCore.Documentos.Aplicacion;

namespace AlxorCore.Documentos.Infraestructura.Correo;

/// <summary>
/// Traduce un <see cref="MensajeCorreo"/> del dominio a un <see cref="MailMessage"/> de
/// <c>System.Net.Mail</c>: remitente, destinatario, asunto, cuerpo <b>HTML</b> y, si lo hay, el
/// adjunto con el tipo de contenido deducido de su extensión (<c>.ics</c>, <c>.pdf</c>…). Se aísla
/// aquí, sin dependencias de red, para poder probarlo con tests unitarios.
/// </summary>
internal static class ConstructorMensajeSmtp
{
    public static MailMessage Construir(MensajeCorreo mensaje, string remitente, string remitenteNombre)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var correo = new MailMessage
        {
            From = new MailAddress(remitente, remitenteNombre),
            Subject = mensaje.Asunto,
            Body = mensaje.Cuerpo,
            IsBodyHtml = true,
        };
        correo.To.Add(mensaje.Para);

        if (mensaje.Adjunto is { Length: > 0 } && !string.IsNullOrWhiteSpace(mensaje.NombreAdjunto))
        {
            // El MemoryStream lo posee el Attachment, que a su vez lo libera al desecharse el MailMessage.
            var flujo = new MemoryStream(mensaje.Adjunto, writable: false);
            var adjunto = new Attachment(flujo, mensaje.NombreAdjunto, TipoContenido(mensaje.NombreAdjunto));
            correo.Attachments.Add(adjunto);
        }

        return correo;
    }

    /// <summary>Tipo MIME según la extensión del adjunto; genérico si no se reconoce.</summary>
    public static string TipoContenido(string nombreAdjunto)
    {
        var extension = Path.GetExtension(nombreAdjunto).ToLowerInvariant();
        return extension switch
        {
            ".ics" => "text/calendar",
            ".pdf" => MediaTypeNames.Application.Pdf,
            ".html" or ".htm" => MediaTypeNames.Text.Html,
            ".txt" => MediaTypeNames.Text.Plain,
            _ => MediaTypeNames.Application.Octet,
        };
    }
}

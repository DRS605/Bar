using System.Net;
using System.Net.Mail;
using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlxorCore.Documentos.Infraestructura.Correo;

/// <summary>
/// Envío real de correo de negocio (facturas, presupuestos y avisos de reservas) por <b>SMTP</b>
/// (<c>System.Net.Mail</c>). Se registra solo cuando hay un servidor configurado en la sección
/// <c>Correo</c> (ver <see cref="OpcionesCorreo"/>); si no, se usa el <see cref="ServicioCorreoStub"/>.
/// </summary>
internal sealed class ServicioCorreoSmtp : IServicioCorreo
{
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<ServicioCorreoSmtp> _log;

    public ServicioCorreoSmtp(IOptions<OpcionesCorreo> opciones, ILogger<ServicioCorreoSmtp> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        using var correo = ConstructorMensajeSmtp.Construir(mensaje, _opciones.Remitente, _opciones.RemitenteNombre);
        using var cliente = new SmtpClient(_opciones.Host, _opciones.Puerto) { EnableSsl = _opciones.UsarStartTls };
        if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
        {
            cliente.Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Clave);
        }

        await cliente.SendMailAsync(correo, ct).ConfigureAwait(false);
        _log.LogInformation("Correo «{Asunto}» enviado a {Para} ({Bytes} bytes adjuntos).",
            mensaje.Asunto, mensaje.Para, mensaje.Adjunto.Length);
    }
}

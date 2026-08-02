using System.Net;
using System.Net.Mail;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlxorCore.Identidad.Infraestructura.Correo;

/// <summary>
/// Envío de correos de cuenta por <b>SMTP</b> (MailKit): verificación de correo, restablecimiento de
/// contraseña e invitación. Construye el enlace con la URL base configurada y envía un correo HTML.
/// Se registra solo cuando hay un servidor SMTP configurado (ver <see cref="OpcionesCorreo"/>).
/// </summary>
internal sealed class ServicioCorreoSmtp : IServicioVerificacionEmail
{
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<ServicioCorreoSmtp> _log;

    public ServicioCorreoSmtp(IOptions<OpcionesCorreo> opciones, ILogger<ServicioCorreoSmtp> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public Task EnviarVerificacionAsync(Usuario usuario, string token, CancellationToken ct = default)
    {
        var enlace = $"{_opciones.BaseUrl.TrimEnd('/')}/?verificar={Uri.EscapeDataString(token)}";
        var html = Cuerpo(
            $"Hola {usuario.Nombre}, confirma tu correo",
            "Gracias por crear tu cuenta en ALXOR Core. Confirma tu correo para activarla:",
            "Verificar correo", enlace);
        return EnviarAsync(usuario.Email.Valor, "Confirma tu correo · ALXOR Core", html, ct);
    }

    public Task EnviarRestablecimientoAsync(Usuario usuario, string token, CancellationToken ct = default)
    {
        var enlace = $"{_opciones.BaseUrl.TrimEnd('/')}/?restablecer={Uri.EscapeDataString(token)}";
        var html = Cuerpo(
            $"Hola {usuario.Nombre}",
            "Has solicitado (o te han invitado a) establecer tu contraseña. Pulsa el botón para hacerlo:",
            "Establecer contraseña", enlace);
        return EnviarAsync(usuario.Email.Valor, "Tu contraseña de ALXOR Core", html, ct);
    }

    private async Task EnviarAsync(string destino, string asunto, string html, CancellationToken ct)
    {
        using var mensaje = new MailMessage
        {
            From = new MailAddress(_opciones.Remitente, _opciones.RemitenteNombre),
            Subject = asunto,
            Body = html,
            IsBodyHtml = true,
        };
        mensaje.To.Add(destino);

        using var cliente = new SmtpClient(_opciones.Host, _opciones.Puerto) { EnableSsl = _opciones.UsarStartTls };
        if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
        {
            cliente.Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Clave);
        }

        await cliente.SendMailAsync(mensaje, ct).ConfigureAwait(false);
        _log.LogInformation("Correo «{Asunto}» enviado a {Destino}.", asunto, destino);
    }

    private static string Cuerpo(string titulo, string texto, string cta, string enlace) =>
        $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:auto;color:#0b2a4a">
          <h2 style="color:#0b6ea4">ALXOR Core</h2>
          <h3>{titulo}</h3>
          <p>{texto}</p>
          <p style="margin:24px 0"><a href="{enlace}" style="background:#0d94ba;color:#fff;padding:12px 20px;border-radius:10px;text-decoration:none;font-weight:bold">{cta}</a></p>
          <p style="font-size:12px;color:#5c7186">Si no esperabas este correo, puedes ignorarlo. Enlace: {enlace}</p>
        </div>
        """;
}

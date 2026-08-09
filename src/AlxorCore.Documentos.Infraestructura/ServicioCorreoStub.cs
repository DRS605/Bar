using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>
/// Implementación de reserva (stub) del envío de correo: registra el envío en el log. Se usa cuando no
/// hay servidor SMTP configurado (sección «Correo» vacía); en cuanto se configura, la composición
/// elige <see cref="Correo.ServicioCorreoSmtp"/> sin cambiar el puerto <see cref="IServicioCorreo"/>.
/// </summary>
internal sealed class ServicioCorreoStub : IServicioCorreo
{
    private readonly ILogger<ServicioCorreoStub> _log;

    public ServicioCorreoStub(ILogger<ServicioCorreoStub> log) => _log = log;

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        _log.LogInformation("Correo (stub) a {Para}: {Asunto} ({Bytes} bytes adjuntos).", mensaje.Para, mensaje.Asunto, mensaje.Adjunto.Length);
        return Task.CompletedTask;
    }
}

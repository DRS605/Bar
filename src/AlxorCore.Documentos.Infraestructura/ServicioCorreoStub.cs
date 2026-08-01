using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>
/// Implementación provisional (stub) del envío de correo: registra el envío en el log. El proveedor
/// real (SMTP/servicio) se añadirá sin cambiar el puerto <see cref="IServicioCorreo"/>.
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

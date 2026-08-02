using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Identidad.Infraestructura.Correo;

/// <summary>
/// Implementación provisional (stub) del envío del correo de verificación: registra la intención
/// en el log. El proveedor real (SMTP/servicio) llegará con el módulo Documentos sin cambiar el
/// puerto <see cref="IServicioVerificacionEmail"/>.
/// </summary>
internal sealed class ServicioVerificacionEmailStub : IServicioVerificacionEmail
{
    private readonly ILogger<ServicioVerificacionEmailStub> _log;

    public ServicioVerificacionEmailStub(ILogger<ServicioVerificacionEmailStub> log) => _log = log;

    public Task EnviarVerificacionAsync(Usuario usuario, string token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        _log.LogInformation("Correo de verificación para {Email} (stub). Enlace: /?verificar={Token}", usuario.Email.Valor, token);
        return Task.CompletedTask;
    }

    public Task EnviarRestablecimientoAsync(Usuario usuario, string token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        _log.LogInformation("Correo de restablecimiento para {Email} (stub). Enlace: /?restablecer={Token}", usuario.Email.Valor, token);
        return Task.CompletedTask;
    }
}

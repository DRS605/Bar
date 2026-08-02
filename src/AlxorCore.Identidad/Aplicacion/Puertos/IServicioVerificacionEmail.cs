using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>
/// Puerto para enviar el correo de verificación tras el registro. En el MVP la implementación
/// es un stub (registra la intención); el proveedor real (SMTP/servicio) llegará con el módulo
/// Documentos, sin cambiar este contrato.
/// </summary>
public interface IServicioVerificacionEmail
{
    /// <summary>Envía (o simula) el correo de verificación con el token del enlace.</summary>
    Task EnviarVerificacionAsync(Usuario usuario, string token, CancellationToken ct = default);

    /// <summary>Envía (o simula) el correo de restablecimiento de contraseña con el token del enlace.</summary>
    Task EnviarRestablecimientoAsync(Usuario usuario, string token, CancellationToken ct = default);
}

using AlxorCore.Identidad.Dominio;

namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>
/// Puerto para enviar el correo de verificación tras el registro. En el MVP la implementación
/// es un stub (registra la intención); el proveedor real (SMTP/servicio) llegará con el módulo
/// Documentos, sin cambiar este contrato.
/// </summary>
public interface IServicioVerificacionEmail
{
    /// <summary>Envía (o simula) el correo de verificación para el usuario recién registrado.</summary>
    Task EnviarVerificacionAsync(Usuario usuario, CancellationToken ct = default);
}

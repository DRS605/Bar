namespace AlxorCore.Documentos.Infraestructura.Correo;

/// <summary>
/// Ajustes del envío de correo de negocio (facturas, presupuestos y avisos de reservas). Comparte la
/// misma sección de configuración <c>Correo</c> que el correo de cuenta del módulo Identidad, de modo
/// que el servidor SMTP se configura una sola vez. Si <see cref="Host"/> está vacío se usa el
/// <b>stub</b> (registra el envío en el log); en cuanto hay servidor, los correos salen de verdad.
/// </summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>Servidor SMTP. Vacío = modo stub (no se envían correos reales).</summary>
    public string? Host { get; set; }

    public int Puerto { get; set; } = 587;

    public bool UsarStartTls { get; set; } = true;

    public string? Usuario { get; set; }

    public string? Clave { get; set; }

    /// <summary>Dirección del remitente (p. ej. no-responder@tudominio.com).</summary>
    public string Remitente { get; set; } = "no-responder@alxor.local";

    public string RemitenteNombre { get; set; } = "Bar Query";

    /// <summary>¿Hay un servidor SMTP configurado?</summary>
    public bool Configurado => !string.IsNullOrWhiteSpace(Host);
}

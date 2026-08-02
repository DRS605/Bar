namespace AlxorCore.Identidad.Infraestructura.Correo;

/// <summary>
/// Ajustes del envío de correo. Si <see cref="Host"/> está vacío se usa el <b>stub</b> (registra el
/// enlace en el log); en cuanto se configura un servidor SMTP, los correos se envían de verdad.
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

    public string RemitenteNombre { get; set; } = "ALXOR Core";

    /// <summary>URL base de la aplicación para construir los enlaces de los correos.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>¿Hay un servidor SMTP configurado?</summary>
    public bool Configurado => !string.IsNullOrWhiteSpace(Host);
}

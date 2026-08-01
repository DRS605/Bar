using System.ComponentModel.DataAnnotations;

namespace AlxorCore.Identidad.Infraestructura.Seguridad;

/// <summary>Opciones de configuración para la emisión y validación de tokens JWT.</summary>
public sealed class OpcionesJwt
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Jwt";

    /// <summary>Emisor del token.</summary>
    [Required]
    public string Emisor { get; set; } = "alxor-core";

    /// <summary>Audiencia del token.</summary>
    [Required]
    public string Audiencia { get; set; } = "alxor-core";

    /// <summary>Clave secreta simétrica para firmar (mínimo 32 caracteres). Debe venir de configuración segura.</summary>
    [Required]
    [MinLength(32)]
    public string ClaveSecreta { get; set; } = string.Empty;

    /// <summary>Minutos de validez del token de acceso.</summary>
    [Range(1, 1440)]
    public int MinutosExpiracion { get; set; } = 60;
}

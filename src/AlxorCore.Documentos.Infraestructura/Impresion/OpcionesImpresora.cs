namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Ajustes de la impresora de tickets. Si <see cref="Host"/> está vacío no hay impresora (los intentos
/// de imprimir devuelven un error legible y se puede descargar el ticket). La mayoría de impresoras
/// térmicas de barra aceptan trabajos por red en el puerto <b>9100</b> (RAW/JetDirect).
/// </summary>
public sealed class OpcionesImpresora
{
    public const string Seccion = "Impresora";

    /// <summary>IP o nombre de red de la impresora. Vacío = sin impresora.</summary>
    public string? Host { get; set; }

    /// <summary>Puerto RAW de la impresora (9100 por defecto).</summary>
    public int Puerto { get; set; } = 9100;

    /// <summary>Tiempo máximo de conexión/envío en milisegundos.</summary>
    public int TiempoEsperaMs { get; set; } = 5000;

    /// <summary>¿Hay una impresora configurada?</summary>
    public bool Configurada => !string.IsNullOrWhiteSpace(Host);
}

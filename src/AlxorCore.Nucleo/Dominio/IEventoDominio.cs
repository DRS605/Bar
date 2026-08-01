namespace AlxorCore.Nucleo.Dominio;

/// <summary>
/// Marca un evento de dominio: algo relevante que ha ocurrido dentro de un agregado
/// (por ejemplo, "usuario registrado" o "factura emitida"). Los eventos se publican tras
/// persistir y sirven de base para la auditoría y las integraciones desacopladas.
/// </summary>
public interface IEventoDominio
{
    /// <summary>Momento en el que ocurrió el evento.</summary>
    DateTimeOffset OcurridoEn { get; }
}

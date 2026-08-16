using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Impresora de tickets de reserva: no hay impresora configurada. Informa de que no está configurada
/// (para que la operación devuelva un error legible) y registra los intentos en el log.
/// </summary>
internal sealed class ImpresoraTicketsNula : IImpresoraTickets
{
    private readonly ILogger<ImpresoraTicketsNula> _log;

    public ImpresoraTicketsNula(ILogger<ImpresoraTicketsNula> log) => _log = log;

    public bool Configurada => false;

    public Task ImprimirAsync(byte[] datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        _log.LogInformation("Impresora no configurada; se omite la impresión del ticket ({Bytes} bytes).", datos.Length);
        return Task.CompletedTask;
    }
}

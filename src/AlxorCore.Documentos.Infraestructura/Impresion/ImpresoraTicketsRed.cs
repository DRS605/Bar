using System.Net.Sockets;
using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Impresora de tickets por <b>red</b> (RAW/JetDirect, normalmente puerto 9100): abre un socket TCP con
/// la impresora y le envía los bytes ESC/POS. Se registra cuando hay un host configurado (ver
/// <see cref="OpcionesImpresora"/>); si no, se usa <see cref="ImpresoraTicketsNula"/>.
/// </summary>
internal sealed class ImpresoraTicketsRed : IImpresoraTickets
{
    private readonly OpcionesImpresora _opciones;
    private readonly ILogger<ImpresoraTicketsRed> _log;

    public ImpresoraTicketsRed(IOptions<OpcionesImpresora> opciones, ILogger<ImpresoraTicketsRed> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public bool Configurada => _opciones.Configurada;

    public async Task ImprimirAsync(byte[] datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        using var tiempo = new CancellationTokenSource(TimeSpan.FromMilliseconds(_opciones.TiempoEsperaMs));
        using var combinado = CancellationTokenSource.CreateLinkedTokenSource(ct, tiempo.Token);

        using var cliente = new TcpClient();
        await cliente.ConnectAsync(_opciones.Host!, _opciones.Puerto, combinado.Token).ConfigureAwait(false);
        await using var flujo = cliente.GetStream();
        await flujo.WriteAsync(datos, combinado.Token).ConfigureAwait(false);
        await flujo.FlushAsync(combinado.Token).ConfigureAwait(false);

        _log.LogInformation("Ticket enviado a la impresora {Host}:{Puerto} ({Bytes} bytes).",
            _opciones.Host, _opciones.Puerto, datos.Length);
    }
}

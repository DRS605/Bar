using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Reservas.Aplicacion;
using Microsoft.Extensions.Logging;

namespace AlxorCore.Reservas.Infraestructura;

/// <summary>
/// Envía el correo de una reserva al cliente. Compone el mensaje (asunto + cuerpo HTML con
/// <see cref="GeneradorCorreoReserva"/> y, salvo la cancelación, el adjunto iCalendar con
/// <see cref="GeneradorICal"/>) y lo entrega por el puerto <see cref="IServicioCorreo"/>. Es
/// <b>tolerante a fallos</b>: nunca lanza, para no interrumpir el alta o la cancelación de la reserva.
/// </summary>
internal sealed class NotificadorReservas : INotificadorReservas
{
    private readonly IServicioCorreo _correo;
    private readonly IConsultaEmpresas _empresas;
    private readonly IConsultaMesas _mesas;
    private readonly IReloj _reloj;
    private readonly ILogger<NotificadorReservas> _log;

    public NotificadorReservas(IServicioCorreo correo, IConsultaEmpresas empresas, IConsultaMesas mesas, IReloj reloj, ILogger<NotificadorReservas> log)
    {
        _correo = correo;
        _empresas = empresas;
        _mesas = mesas;
        _reloj = reloj;
        _log = log;
    }

    public async Task EnviarAsync(TipoCorreoReserva tipo, Guid empresaId, ReservaDto reserva, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reserva);

        if (string.IsNullOrWhiteSpace(reserva.Email))
        {
            return; // No hay destinatario.
        }

        try
        {
            var empresa = await _empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
            var nombreLocal = string.IsNullOrWhiteSpace(empresa?.RazonSocial) ? "Tu local" : empresa!.RazonSocial;

            string? mesaNombre = null;
            if (reserva.MesaId is not null)
            {
                var mesa = await _mesas.ObtenerAsync(reserva.MesaId.Value, ct).ConfigureAwait(false);
                mesaNombre = mesa?.Nombre;
            }

            var datos = new DatosCorreoReserva(nombreLocal, reserva.NombreCliente, reserva.FechaHora, reserva.DuracionMinutos, reserva.Comensales, mesaNombre, reserva.Notas);
            var (asunto, html) = GeneradorCorreoReserva.Generar(tipo, datos);

            byte[] adjunto = Array.Empty<byte>();
            var nombreAdjunto = string.Empty;
            if (tipo != TipoCorreoReserva.Cancelacion)
            {
                var ical = GeneradorICal.Generar(new[] { reserva }, "Reserva", _reloj.AhoraUtc);
                adjunto = Encoding.UTF8.GetBytes(ical);
                nombreAdjunto = "reserva.ics";
            }

            await _correo.EnviarAsync(new MensajeCorreo(reserva.Email!, asunto, html, adjunto, nombreAdjunto), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Un fallo de correo no debe interrumpir la operación de negocio.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogError(ex, "No se pudo enviar el correo ({Tipo}) de la reserva {ReservaId}.", tipo, reserva.Id);
        }
    }
}

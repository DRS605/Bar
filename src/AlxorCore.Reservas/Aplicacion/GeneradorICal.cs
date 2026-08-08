using System.Globalization;
using System.Text;

namespace AlxorCore.Reservas.Aplicacion;

/// <summary>
/// Genera un calendario en formato <b>iCalendar</b> (RFC 5545) a partir de reservas. Es el formato
/// estándar que entienden Google Calendar, Apple Calendar y Outlook, tanto para importar una reserva
/// suelta como para <b>suscribirse</b> a la agenda del local. Es una función pura (sin dependencias),
/// así que se puede probar de forma aislada.
/// </summary>
public static class GeneradorICal
{
    private const string Crlf = "\r\n";

    /// <summary>Construye un documento VCALENDAR con un evento por cada reserva.</summary>
    public static string Generar(IEnumerable<ReservaDto> reservas, string nombreCalendario, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(reservas);

        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR").Append(Crlf);
        sb.Append("VERSION:2.0").Append(Crlf);
        sb.Append("PRODID:-//ALXOR Core//Reservas//ES").Append(Crlf);
        sb.Append("CALSCALE:GREGORIAN").Append(Crlf);
        sb.Append("METHOD:PUBLISH").Append(Crlf);
        sb.Append("X-WR-CALNAME:").Append(Escapar(nombreCalendario)).Append(Crlf);

        var sello = Fecha(ahora);
        foreach (var r in reservas)
        {
            sb.Append("BEGIN:VEVENT").Append(Crlf);
            sb.Append("UID:").Append(r.Id.ToString("N")).Append("@alxor-core").Append(Crlf);
            sb.Append("DTSTAMP:").Append(sello).Append(Crlf);
            sb.Append("DTSTART:").Append(Fecha(r.FechaHora)).Append(Crlf);
            sb.Append("DTEND:").Append(Fecha(r.FechaHoraFin)).Append(Crlf);
            sb.Append("SUMMARY:").Append(Escapar($"Reserva {r.NombreCliente} ({r.Comensales} pax)")).Append(Crlf);

            var descripcion = Descripcion(r);
            if (descripcion.Length > 0)
            {
                sb.Append("DESCRIPTION:").Append(Escapar(descripcion)).Append(Crlf);
            }

            sb.Append("STATUS:").Append(Estado(r.Estado)).Append(Crlf);
            sb.Append("END:VEVENT").Append(Crlf);
        }

        sb.Append("END:VCALENDAR").Append(Crlf);
        return sb.ToString();
    }

    private static string Descripcion(ReservaDto r)
    {
        var partes = new List<string> { $"{r.Comensales} comensales" };
        if (!string.IsNullOrWhiteSpace(r.Telefono))
        {
            partes.Add($"Tel: {r.Telefono}");
        }

        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            partes.Add(r.Email!);
        }

        if (!string.IsNullOrWhiteSpace(r.Notas))
        {
            partes.Add(r.Notas!);
        }

        partes.Add($"Estado: {r.Estado}");
        return string.Join(" · ", partes);
    }

    // Las reservas canceladas o no presentadas se marcan CANCELLED; el resto, CONFIRMED.
    private static string Estado(string estado) =>
        estado is "Cancelada" or "NoShow" ? "CANCELLED" : "CONFIRMED";

    private static string Fecha(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    // Escapado de texto de RFC 5545: barra invertida, comas, puntos y coma y saltos de línea.
    private static string Escapar(string texto) => texto
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
}

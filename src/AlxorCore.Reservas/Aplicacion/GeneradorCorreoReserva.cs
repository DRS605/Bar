using System.Globalization;

namespace AlxorCore.Reservas.Aplicacion;

/// <summary>Datos necesarios para redactar el correo de una reserva.</summary>
public sealed record DatosCorreoReserva(
    string NombreLocal,
    string NombreCliente,
    DateTimeOffset FechaHora,
    int DuracionMinutos,
    int Comensales,
    string? MesaNombre,
    string? Notas);

/// <summary>
/// Compone el <b>asunto</b> y el <b>cuerpo HTML</b> del correo de una reserva (confirmación,
/// recordatorio o cancelación). Es una función pura (sin dependencias ni fechas del sistema), de modo
/// que se puede probar de forma aislada. El HTML es sobrio y con estilos en línea para que se vea bien
/// en cualquier cliente de correo.
/// </summary>
public static class GeneradorCorreoReserva
{
    private static readonly string[] Dias = { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
    private static readonly string[] Meses = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private const string Verde = "#14563e";
    private const string Cobre = "#b8692b";
    private const string Tinta = "#17251e";
    private const string Suave = "#5f7168";

    public static (string Asunto, string Html) Generar(TipoCorreoReserva tipo, DatosCorreoReserva d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var f = d.FechaHora;
        var diaTxt = $"{Dias[(int)f.DayOfWeek]} {f.Day} de {Meses[f.Month - 1]}";
        var horaTxt = f.ToString("HH:mm", CultureInfo.InvariantCulture);
        var nombre = Escapar(d.NombreCliente);
        var local = Escapar(d.NombreLocal);

        string keb, titulo, saludo, cuerpo, colorCabecera = Verde;
        string asunto;
        switch (tipo)
        {
            case TipoCorreoReserva.Recordatorio:
                colorCabecera = Cobre;
                keb = "Recordatorio";
                titulo = "Te esperamos";
                asunto = $"Te esperamos {DiaRelativo(f)} a las {horaTxt} · {d.NombreLocal}";
                saludo = $"Hola {nombre}, te recordamos tu reserva en <strong>{local}</strong>. ¡Te esperamos!";
                cuerpo = "Si al final no puedes venir, avísanos respondiendo a este correo para liberar la mesa. ¡Gracias!";
                break;
            case TipoCorreoReserva.Cancelacion:
                colorCabecera = "#8a3b30";
                keb = local;
                titulo = "Reserva cancelada";
                asunto = $"Reserva cancelada · {d.NombreLocal}";
                saludo = $"Hola {nombre}, hemos cancelado tu reserva del <strong>{diaTxt} a las {horaTxt}</strong>.";
                cuerpo = "Cuando quieras, vuelve a reservar. ¡Esperamos verte pronto!";
                break;
            default: // Confirmacion
                keb = local;
                titulo = "Reserva confirmada";
                asunto = $"Tu reserva en {d.NombreLocal} · {diaTxt}, {horaTxt}";
                saludo = $"Hola {nombre}, tu mesa está reservada. Aquí tienes los detalles y un archivo para añadir la reserva a tu calendario.";
                cuerpo = "¿Alguna alergia o petición especial? Responde a este correo y lo tenemos en cuenta.";
                break;
        }

        var filas = new System.Text.StringBuilder();
        Fila(filas, "Cuándo", $"{Mayus(diaTxt)} · {horaTxt}");
        Fila(filas, "Personas", d.Comensales.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(d.MesaNombre))
        {
            Fila(filas, "Mesa", Escapar(d.MesaNombre!));
        }

        if (!string.IsNullOrWhiteSpace(d.Notas))
        {
            Fila(filas, "Nota", Escapar(d.Notas!));
        }

        var html = $"""
            <div style="margin:0;padding:24px;background:#f4f6f4;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Tinta}">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#fff;border-radius:14px;overflow:hidden;border:1px solid #e6ede9">
                <tr><td style="background:{colorCabecera};padding:26px 30px;color:#fff">
                  <div style="font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;opacity:.85">{Escapar(keb)}</div>
                  <div style="font-size:24px;font-weight:700;margin-top:6px">{titulo}</div>
                </td></tr>
                <tr><td style="padding:24px 30px 6px">
                  <p style="margin:0 0 18px;font-size:15px;line-height:1.5">{saludo}</p>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border:1px solid #e6ede9;border-radius:10px">
                    {filas}
                  </table>
                  <p style="margin:18px 0 0;font-size:13px;color:{Suave}">{cuerpo}</p>
                </td></tr>
                <tr><td style="padding:20px 30px 26px;border-top:1px solid #e6ede9;color:{Suave};font-size:12px">
                  <strong style="color:{Tinta}">{local}</strong><br>
                  Recibes este correo por tu reserva.
                  <span style="color:#9fb0be">· Enviado con Comandia</span>
                </td></tr>
              </table>
            </div>
            """;

        return (asunto, html);
    }

    private static void Fila(System.Text.StringBuilder sb, string clave, string valor) =>
        sb.Append($"""<tr><td style="padding:11px 14px;color:{Suave};font-size:13px;font-weight:600;width:90px;border-bottom:1px solid #eef3f0">{clave}</td><td style="padding:11px 14px;font-size:13px;border-bottom:1px solid #eef3f0">{valor}</td></tr>""");

    private static string DiaRelativo(DateTimeOffset f) => $"el {Dias[(int)f.DayOfWeek]}";

    private static string Mayus(string s) => s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string Escapar(string s) => s
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}

using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura.Correo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Documentos.Tests;

public class ConstructorMensajeSmtpTests
{
    private static MensajeCorreo ConAdjunto(string nombreAdjunto) => new(
        "cliente@ejemplo.com", "Tu reserva", "<p>Hola</p>",
        Encoding.UTF8.GetBytes("BEGIN:VCALENDAR"), nombreAdjunto);

    [Fact]
    public void Construye_remitente_destinatario_asunto_y_cuerpo_html()
    {
        using var correo = ConstructorMensajeSmtp.Construir(ConAdjunto("reserva.ics"), "no-responder@barquery.local", "Bar Query");

        correo.From!.Address.Should().Be("no-responder@barquery.local");
        correo.From.DisplayName.Should().Be("Bar Query");
        correo.To.Should().ContainSingle(d => d.Address == "cliente@ejemplo.com");
        correo.Subject.Should().Be("Tu reserva");
        correo.Body.Should().Be("<p>Hola</p>");
        correo.IsBodyHtml.Should().BeTrue();
    }

    [Fact]
    public void El_ics_se_adjunta_como_text_calendar()
    {
        using var correo = ConstructorMensajeSmtp.Construir(ConAdjunto("reserva.ics"), "x@y.z", "Bar Query");

        correo.Attachments.Should().ContainSingle();
        correo.Attachments[0].Name.Should().Be("reserva.ics");
        correo.Attachments[0].ContentType.MediaType.Should().Be("text/calendar");
    }

    [Fact]
    public void Sin_adjunto_no_se_adjunta_nada()
    {
        var mensaje = new MensajeCorreo("c@e.com", "Reserva cancelada", "<p>Adiós</p>", System.Array.Empty<byte>(), string.Empty);
        using var correo = ConstructorMensajeSmtp.Construir(mensaje, "x@y.z", "Bar Query");

        correo.Attachments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("factura.pdf", "application/pdf")]
    [InlineData("reserva.ics", "text/calendar")]
    [InlineData("nota.txt", "text/plain")]
    [InlineData("algo.desconocido", "application/octet-stream")]
    public void Resuelve_el_tipo_de_contenido_por_extension(string nombre, string tipoEsperado)
    {
        ConstructorMensajeSmtp.TipoContenido(nombre).Should().Be(tipoEsperado);
    }
}

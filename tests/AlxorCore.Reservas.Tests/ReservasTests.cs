using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Reservas.Aplicacion;
using AlxorCore.Reservas.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Reservas.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class ReservaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DateTimeOffset Cuando = new(2026, 2, 14, 21, 0, 0, TimeSpan.Zero);

    private static Reserva Nueva(Guid? mesa = null) =>
        Reserva.Crear(Empresa, "  Ana  ", "600111222", "ana@ej.com", Cuando, 90, 4, mesa, "cumpleaños", Reloj).Valor;

    [Fact]
    public void Crear_valida_y_normaliza()
    {
        var r = Reserva.Crear(Empresa, "Ana", null, null, Cuando, 90, 4, null, null, Reloj);
        r.EsCorrecto.Should().BeTrue();
        r.Valor.Estado.Should().Be(EstadoReserva.Pendiente);
        r.Valor.FechaHoraFin.Should().Be(Cuando.AddMinutes(90));
        r.Valor.EventosDominio.Should().ContainSingle(e => e is ReservaCreada);
    }

    [Theory]
    [InlineData("", 4)]
    [InlineData("Ana", 0)]
    [InlineData("Ana", -3)]
    public void Crear_rechaza_datos_invalidos(string nombre, int comensales)
    {
        Reserva.Crear(Empresa, nombre, null, null, Cuando, 90, comensales, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Email_sin_arroba_falla()
    {
        Reserva.Crear(Empresa, "Ana", null, "correo-malo", Cuando, 90, 2, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Duracion_no_positiva_se_normaliza_a_120()
    {
        Reserva.Crear(Empresa, "Ana", null, null, Cuando, 0, 2, null, null, Reloj).Valor.DuracionMinutos.Should().Be(120);
    }

    [Fact]
    public void Confirmar_y_sentar_flujo()
    {
        var r = Nueva();
        r.Confirmar(Reloj).EsCorrecto.Should().BeTrue();
        r.Estado.Should().Be(EstadoReserva.Confirmada);

        r.Sentar(Guid.NewGuid(), Reloj).EsCorrecto.Should().BeTrue();
        r.Estado.Should().Be(EstadoReserva.Sentada);
        r.ComandaId.Should().NotBeNull();
    }

    [Fact]
    public void No_se_puede_confirmar_dos_veces()
    {
        var r = Nueva();
        r.Confirmar(Reloj);
        r.Confirmar(Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Cancelada_no_es_modificable_ni_sentable()
    {
        var r = Nueva();
        r.Cancelar(Reloj).EsCorrecto.Should().BeTrue();
        r.EsModificable.Should().BeFalse();
        r.Sentar(null, Reloj).EsFallo.Should().BeTrue();
        r.Actualizar("Otro", null, null, Cuando, 60, 2, null, null, Reloj).EsFallo.Should().BeTrue();
    }
}

public class GeneradorICalTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static ReservaDto Dto(string estado = "Confirmada") => new(
        Guid.NewGuid(), "Ana, S.L.", "600111222", "ana@ej.com",
        new DateTimeOffset(2026, 2, 14, 21, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 2, 14, 23, 0, 0, TimeSpan.Zero),
        120, 4, null, "mesa junto a la ventana", estado, null, Ahora);

    [Fact]
    public void Genera_un_vcalendar_con_un_vevent_por_reserva()
    {
        var ical = GeneradorICal.Generar(new[] { Dto(), Dto() }, "Reservas", Ahora);
        ical.Should().StartWith("BEGIN:VCALENDAR");
        ical.TrimEnd().Should().EndWith("END:VCALENDAR");
        System.Text.RegularExpressions.Regex.Matches(ical, "BEGIN:VEVENT").Count.Should().Be(2);
        ical.Should().Contain("VERSION:2.0");
        ical.Should().Contain("DTSTART:20260214T210000Z");
        ical.Should().Contain("DTEND:20260214T230000Z");
        // Las comas del nombre se escapan según RFC 5545.
        ical.Should().Contain("SUMMARY:Reserva Ana\\, S.L. (4 pax)");
        ical.Should().Contain("\r\n");
    }

    [Fact]
    public void Reserva_cancelada_se_marca_CANCELLED()
    {
        GeneradorICal.Generar(new[] { Dto("Cancelada") }, "Reservas", Ahora).Should().Contain("STATUS:CANCELLED");
    }
}

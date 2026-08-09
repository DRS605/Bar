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

public class TurnoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    private static Turno Cena() =>
        Turno.Crear(Empresa, "Cena", DiasSemana.Viernes | DiasSemana.Sabado, new TimeOnly(20, 0), new TimeOnly(23, 30), 20, Reloj).Valor;

    [Fact]
    public void Aplica_dentro_del_dia_y_hora()
    {
        // 2026-02-14 es sábado.
        Cena().Aplica(new DateTimeOffset(2026, 2, 14, 21, 0, 0, TimeSpan.Zero)).Should().BeTrue();
    }

    [Fact]
    public void No_aplica_fuera_de_hora()
    {
        Cena().Aplica(new DateTimeOffset(2026, 2, 14, 18, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void No_aplica_en_dia_no_incluido()
    {
        // 2026-02-16 es lunes (no está en Viernes|Sábado).
        Cena().Aplica(new DateTimeOffset(2026, 2, 16, 21, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Turno_que_cruza_medianoche()
    {
        var t = Turno.Crear(Empresa, "Copas", DiasSemana.Todos, new TimeOnly(22, 0), new TimeOnly(2, 0), 0, Reloj).Valor;
        t.Aplica(new DateTimeOffset(2026, 2, 14, 23, 30, 0, TimeSpan.Zero)).Should().BeTrue();
        t.Aplica(new DateTimeOffset(2026, 2, 14, 1, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        t.Aplica(new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Crear_sin_dias_o_con_horas_iguales_falla()
    {
        Turno.Crear(Empresa, "X", DiasSemana.Ninguno, new TimeOnly(20, 0), new TimeOnly(23, 0), 0, Reloj).EsFallo.Should().BeTrue();
        Turno.Crear(Empresa, "X", DiasSemana.Todos, new TimeOnly(20, 0), new TimeOnly(20, 0), 0, Reloj).EsFallo.Should().BeTrue();
    }
}

public class DisponibilidadTurnosTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DateTimeOffset Sabado21 = new(2026, 2, 14, 21, 0, 0, TimeSpan.Zero);

    private static List<Turno> Turnos(int aforo) =>
        new() { Turno.Crear(Empresa, "Cena", DiasSemana.Todos, new TimeOnly(20, 0), new TimeOnly(23, 30), aforo, Reloj).Valor };

    private static ReservaDto Reserva(int pax, DateTimeOffset cuando, string estado = "Confirmada") =>
        new(Guid.NewGuid(), "X", null, null, cuando, cuando.AddHours(2), 120, pax, null, null, estado, null, cuando);

    [Fact]
    public void Sin_turnos_no_restringe()
    {
        DisponibilidadTurnos.Validar(new List<Turno>(), new List<ReservaDto>(), Sabado21, 10).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Fuera_de_horario_falla()
    {
        var r = DisponibilidadTurnos.Validar(Turnos(0), new List<ReservaDto>(), new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero), 2);
        r.EsFallo.Should().BeTrue();
        r.Error.Codigo.Should().Be("reserva.fuera_de_horario");
    }

    [Fact]
    public void Respeta_el_aforo_del_turno()
    {
        var existentes = new List<ReservaDto> { Reserva(3, Sabado21) };
        DisponibilidadTurnos.Validar(Turnos(4), existentes, Sabado21.AddMinutes(30), 1).EsCorrecto.Should().BeTrue();
        var lleno = DisponibilidadTurnos.Validar(Turnos(4), existentes, Sabado21.AddMinutes(30), 2);
        lleno.EsFallo.Should().BeTrue();
        lleno.Error.Codigo.Should().Be("reserva.aforo_completo");
    }

    [Fact]
    public void Las_canceladas_no_cuentan_para_el_aforo()
    {
        var existentes = new List<ReservaDto> { Reserva(4, Sabado21, "Cancelada") };
        DisponibilidadTurnos.Validar(Turnos(4), existentes, Sabado21.AddMinutes(30), 4).EsCorrecto.Should().BeTrue();
    }

    [Fact]
    public void Al_editar_no_se_cuenta_a_si_misma()
    {
        var propia = Reserva(4, Sabado21);
        DisponibilidadTurnos.Validar(Turnos(4), new List<ReservaDto> { propia }, Sabado21, 4, propia.Id).EsCorrecto.Should().BeTrue();
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

public class GeneradorCorreoReservaTests
{
    private static DatosCorreoReserva Datos() => new(
        "Sol de Levante", "Ana", new DateTimeOffset(2026, 2, 14, 21, 0, 0, TimeSpan.Zero), 90, 4, "Terraza 1", "Cumpleaños");

    [Fact]
    public void Confirmacion_incluye_local_fecha_y_datos()
    {
        var (asunto, html) = GeneradorCorreoReserva.Generar(TipoCorreoReserva.Confirmacion, Datos());
        asunto.Should().Contain("Sol de Levante").And.Contain("21:00");
        html.Should().Contain("Reserva confirmada").And.Contain("Ana").And.Contain("Terraza 1").And.Contain("Cumpleaños");
        html.Should().Contain("14 de febrero");
        asunto.Should().Contain("sábado 14 de febrero");
    }

    [Fact]
    public void Recordatorio_y_cancelacion_tienen_su_tono()
    {
        GeneradorCorreoReserva.Generar(TipoCorreoReserva.Recordatorio, Datos()).Html.Should().Contain("Te esperamos");
        GeneradorCorreoReserva.Generar(TipoCorreoReserva.Cancelacion, Datos()).Asunto.Should().Contain("cancelada");
    }

    [Fact]
    public void Escapa_el_html_del_nombre()
    {
        var d = Datos() with { NombreCliente = "<b>Ana</b>" };
        GeneradorCorreoReserva.Generar(TipoCorreoReserva.Confirmacion, d).Html.Should().Contain("&lt;b&gt;Ana&lt;/b&gt;");
    }
}

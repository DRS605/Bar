using System.Net;
using System.Net.Http.Json;
using AlxorCore.Documentos.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Reservas: agenda, estados, sentar y calendario iCal.</summary>
public sealed class ReservasEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ReservasEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ReservaResp(Guid Id, string NombreCliente, DateTimeOffset FechaHora, int Comensales, Guid? MesaId, string Estado, Guid? ComandaId);
    private sealed record MesaResp(Guid Id, string Nombre, bool Ocupada, Guid? ComandaAbiertaId);
    private sealed record AgendaResp(string Token, string Ruta, string Url);

    private static readonly DateTimeOffset Cuando = new(2026, 2, 14, 21, 0, 0, TimeSpan.Zero);

    private static object NuevaReserva(Guid? mesaId = null) => new
    {
        NombreCliente = "Ana",
        FechaHora = Cuando,
        Comensales = 4,
        Telefono = "600111222",
        Email = "ana@ej.com",
        DuracionMinutos = 90,
        MesaId = mesaId,
        Notas = "cumpleaños",
    };

    private static async Task<ReservaResp> CrearAsync(HttpClient cliente, Guid? mesaId = null)
    {
        var resp = await cliente.PostAsJsonAsync("/reservas", NuevaReserva(mesaId));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<ReservaResp>())!;
    }

    [Fact]
    public async Task Crea_una_reserva_y_aparece_en_la_agenda()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var reserva = await CrearAsync(cliente);
        reserva.Estado.Should().Be("Pendiente");

        var lista = await cliente.GetFromJsonAsync<List<ReservaResp>>("/reservas");
        lista.Should().ContainSingle(r => r.Id == reserva.Id);
    }

    [Fact]
    public async Task Transiciones_confirmar_y_cancelar()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var reserva = await CrearAsync(cliente);

        var conf = await cliente.PostAsync(new Uri($"/reservas/{reserva.Id}/confirmar", UriKind.Relative), null);
        conf.StatusCode.Should().Be(HttpStatusCode.OK);
        (await conf.Content.ReadFromJsonAsync<ReservaResp>())!.Estado.Should().Be("Confirmada");

        var canc = await cliente.PostAsync(new Uri($"/reservas/{reserva.Id}/cancelar", UriKind.Relative), null);
        (await canc.Content.ReadFromJsonAsync<ReservaResp>())!.Estado.Should().Be("Cancelada");

        // Ya no se puede confirmar una cancelada.
        var reconf = await cliente.PostAsync(new Uri($"/reservas/{reserva.Id}/confirmar", UriKind.Relative), null);
        reconf.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sentar_con_mesa_abre_la_comanda_y_ocupa_la_mesa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Capacidad = 4 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var reserva = await CrearAsync(cliente, mesa.Id);

        var sentar = await cliente.PostAsync(new Uri($"/reservas/{reserva.Id}/sentar", UriKind.Relative), null);
        sentar.StatusCode.Should().Be(HttpStatusCode.OK);
        var sentada = (await sentar.Content.ReadFromJsonAsync<ReservaResp>())!;
        sentada.Estado.Should().Be("Sentada");
        sentada.ComandaId.Should().NotBeNull();

        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        var m = mesas!.Single(x => x.Id == mesa.Id);
        m.Ocupada.Should().BeTrue();
        m.ComandaAbiertaId.Should().Be(sentada.ComandaId);
    }

    [Fact]
    public async Task Descarga_el_ical_de_una_reserva()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var reserva = await CrearAsync(cliente);

        var resp = await cliente.GetAsync(new Uri($"/reservas/{reserva.Id}/ical", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
        var texto = await resp.Content.ReadAsStringAsync();
        texto.Should().Contain("BEGIN:VCALENDAR").And.Contain("BEGIN:VEVENT").And.Contain("Reserva Ana");
    }

    [Fact]
    public async Task El_feed_de_agenda_es_suscribible_sin_sesion()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await CrearAsync(cliente);

        var agenda = await cliente.GetFromJsonAsync<AgendaResp>("/reservas/agenda");
        agenda!.Token.Should().NotBeNullOrWhiteSpace();
        agenda.Ruta.Should().Be($"/agenda/{agenda.Token}.ics");

        // Un cliente SIN token de sesión puede leer el calendario mediante el enlace secreto.
        var anonimo = _fabrica.CreateClient();
        var feed = await anonimo.GetAsync(new Uri(agenda.Ruta, UriKind.Relative));
        feed.StatusCode.Should().Be(HttpStatusCode.OK);
        feed.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
        (await feed.Content.ReadAsStringAsync()).Should().Contain("BEGIN:VEVENT").And.Contain("Reserva Ana");

        // Un token inexistente devuelve 404.
        var malo = await anonimo.GetAsync(new Uri("/agenda/token-que-no-existe.ics", UriKind.Relative));
        malo.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Regenerar_el_enlace_invalida_el_anterior()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var antes = await cliente.GetFromJsonAsync<AgendaResp>("/reservas/agenda");
        var despues = (await (await cliente.PostAsync(new Uri("/reservas/agenda/regenerar", UriKind.Relative), null)).Content.ReadFromJsonAsync<AgendaResp>())!;
        despues.Token.Should().NotBe(antes!.Token);

        var anonimo = _fabrica.CreateClient();
        (await anonimo.GetAsync(new Uri(antes.Ruta, UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonimo.GetAsync(new Uri(despues.Ruta, UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sin_empresa_seleccionada_no_se_pueden_listar_reservas()
    {
        var cliente = await Ayudas.AutenticadoAsync(_fabrica);
        (await cliente.GetAsync(new Uri("/reservas", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record TurnoResp(Guid Id, string Nombre, int Dias, string HoraInicio, string HoraFin, int AforoComensales, bool Activo);
    private sealed record DispResp(Guid TurnoId, string Nombre, int Aforo, int Reservado, int Libre);

    private static object Reserva(DateTimeOffset cuando, int pax) => new
    {
        NombreCliente = "Ana",
        FechaHora = cuando,
        Comensales = pax,
    };

    [Fact]
    public async Task Los_turnos_definen_el_horario_y_el_aforo_de_las_reservas()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        // Cena todos los días 20:00–23:30 con aforo 4.
        var crear = await cliente.PostAsJsonAsync("/turnos", new { Nombre = "Cena", Dias = 127, HoraInicio = "20:00", HoraFin = "23:30", AforoComensales = 4 });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var turno = (await crear.Content.ReadFromJsonAsync<TurnoResp>())!;
        turno.HoraInicio.Should().Be("20:00");

        var lista = await cliente.GetFromJsonAsync<List<TurnoResp>>("/turnos");
        lista.Should().ContainSingle(t => t.Id == turno.Id && t.Activo);

        var dia = new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero);

        // Fuera de horario (18:00) → 400.
        var fuera = await cliente.PostAsJsonAsync("/reservas", Reserva(dia.AddHours(18), 2));
        fuera.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Dentro de horario (21:00), 3 pax → OK.
        var dentro = await cliente.PostAsJsonAsync("/reservas", Reserva(dia.AddHours(21), 3));
        dentro.StatusCode.Should().Be(HttpStatusCode.Created);

        // Otra de 2 pax (22:00) supera el aforo de 4 → 409.
        var lleno = await cliente.PostAsJsonAsync("/reservas", Reserva(dia.AddHours(22), 2));
        lleno.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Disponibilidad del día: 3 reservados, 1 libre.
        var disp = await cliente.GetFromJsonAsync<List<DispResp>>("/reservas/disponibilidad?dia=2026-02-14");
        var d = disp!.Single(x => x.TurnoId == turno.Id);
        d.Aforo.Should().Be(4);
        d.Reservado.Should().Be(3);
        d.Libre.Should().Be(1);
    }

    private sealed record EnviadosResp(int Enviados);

    private static string EmailUnico() => $"cli-{Guid.NewGuid():N}@ej.com";

    [Fact]
    public async Task Al_crear_con_email_se_envia_confirmacion_con_ics()
    {
        _fabrica.Correos.Limpiar();
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = EmailUnico();

        var crear = await cliente.PostAsJsonAsync("/reservas", new { NombreCliente = "Ana", FechaHora = Cuando, Comensales = 4, Email = email });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);

        var msgs = _fabrica.Correos.Para(email);
        msgs.Should().ContainSingle();
        msgs[0].Asunto.Should().Contain("reserva");
        msgs[0].Cuerpo.Should().Contain("Reserva confirmada").And.Contain("Empresa de Pruebas SL");
        msgs[0].Adjunto.Length.Should().BeGreaterThan(0);
        msgs[0].NombreAdjunto.Should().Be("reserva.ics");
    }

    [Fact]
    public async Task Sin_email_no_se_envia_nada()
    {
        _fabrica.Correos.Limpiar();
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/reservas", new { NombreCliente = "Ana", FechaHora = Cuando, Comensales = 2 });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        _fabrica.Correos.Total.Should().Be(0);
    }

    [Fact]
    public async Task Al_cancelar_con_email_se_envia_aviso_sin_adjunto()
    {
        _fabrica.Correos.Limpiar();
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = EmailUnico();
        var reserva = (await (await cliente.PostAsJsonAsync("/reservas", new { NombreCliente = "Ana", FechaHora = Cuando, Comensales = 2, Email = email })).Content.ReadFromJsonAsync<ReservaResp>())!;

        await cliente.PostAsync(new Uri($"/reservas/{reserva.Id}/cancelar", UriKind.Relative), null);

        var msgs = _fabrica.Correos.Para(email);
        msgs.Should().HaveCount(2);
        msgs[^1].Asunto.Should().Contain("cancelada");
        msgs[^1].NombreAdjunto.Should().BeEmpty();
    }

    [Fact]
    public async Task El_recordatorio_se_envia_una_sola_vez_para_reservas_proximas()
    {
        _fabrica.Correos.Limpiar();
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = EmailUnico();
        var cuando = DateTimeOffset.UtcNow.AddHours(3);
        await cliente.PostAsJsonAsync("/reservas", new { NombreCliente = "Ana", FechaHora = cuando, Comensales = 2, Email = email });
        _fabrica.Correos.Limpiar(); // descartamos la confirmación del alta

        var r1 = await cliente.PostAsync(new Uri("/reservas/recordatorios/procesar", UriKind.Relative), null);
        (await r1.Content.ReadFromJsonAsync<EnviadosResp>())!.Enviados.Should().Be(1);
        var msgs = _fabrica.Correos.Para(email);
        msgs.Should().ContainSingle();
        msgs[0].Asunto.Should().Contain("Te esperamos");

        // Segunda pasada: ya no se reenvía.
        var r2 = await cliente.PostAsync(new Uri("/reservas/recordatorios/procesar", UriKind.Relative), null);
        (await r2.Content.ReadFromJsonAsync<EnviadosResp>())!.Enviados.Should().Be(0);
        _fabrica.Correos.Para(email).Should().ContainSingle();
    }

    [Fact]
    public async Task Un_turno_desactivado_deja_de_restringir()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var turno = (await (await cliente.PostAsJsonAsync("/turnos", new { Nombre = "Comida", Dias = 127, HoraInicio = "13:00", HoraFin = "16:00", AforoComensales = 2 })).Content.ReadFromJsonAsync<TurnoResp>())!;

        var dia = new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero);
        // A las 21:00 no hay turno → 400.
        (await cliente.PostAsJsonAsync("/reservas", Reserva(dia.AddHours(21), 2))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Al retirar el único turno, se abre la reserva libre.
        (await cliente.DeleteAsync(new Uri($"/turnos/{turno.Id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.PostAsJsonAsync("/reservas", Reserva(dia.AddHours(21), 2))).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

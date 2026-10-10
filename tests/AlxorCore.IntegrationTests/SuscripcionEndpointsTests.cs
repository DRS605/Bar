using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Pruebas del plan contratado (Essential vs Pro) y de que las funciones Pro (autopedido/carta QR,
/// comandas a cocina y reservas) quedan capadas cuando el local está en el plan Essential.
/// </summary>
public sealed class SuscripcionEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public SuscripcionEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record SuscripcionResp(string Plan);

    private static async Task PonerPlanAsync(HttpClient cliente, string plan)
    {
        var resp = await cliente.PutAsJsonAsync("/suscripcion", new { Plan = plan });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task El_plan_por_defecto_es_Pro()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var sus = (await cliente.GetFromJsonAsync<SuscripcionResp>("/suscripcion"))!;

        sus.Plan.Should().Be("Pro");
    }

    [Fact]
    public async Task Cambiar_a_Essential_y_volver_a_Pro_persiste()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        await PonerPlanAsync(cliente, "Essential");
        (await cliente.GetFromJsonAsync<SuscripcionResp>("/suscripcion"))!.Plan.Should().Be("Essential");

        await PonerPlanAsync(cliente, "Pro");
        (await cliente.GetFromJsonAsync<SuscripcionResp>("/suscripcion"))!.Plan.Should().Be("Pro");
    }

    [Fact]
    public async Task En_Essential_las_funciones_Pro_devuelven_403()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await PonerPlanAsync(cliente, "Essential");

        (await cliente.GetAsync(new Uri("/pedidos-web", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cliente.GetAsync(new Uri("/avisos", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cliente.GetAsync(new Uri("/carta/configuracion", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cliente.GetAsync(new Uri("/carta/fichas", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cliente.GetAsync(new Uri("/reservas", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task En_Essential_la_carta_publica_no_existe()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        await PonerPlanAsync(cliente, "Essential");

        var anon = _fabrica.CreateClient();
        var resp = await anon.GetAsync(new Uri($"/carta/{empresaId}/datos?idioma=es", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task En_Pro_las_funciones_Pro_y_la_carta_publica_responden()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        // Por defecto es Pro: las funciones Pro no deben estar capadas.
        (await cliente.GetAsync(new Uri("/pedidos-web", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync(new Uri("/reservas", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

        var anon = _fabrica.CreateClient();
        var carta = await anon.GetAsync(new Uri($"/carta/{empresaId}/datos?idioma=es", UriKind.Relative));
        carta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la carta pública (menú con QR): acceso anónimo y solo lectura.</summary>
public sealed class CartaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CartaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ItemResp(string Nombre, decimal Precio);
    private sealed record CatResp(string Nombre, List<ItemResp> Items);
    private sealed record CartaResp(string Local, List<CatResp> Categorias);

    [Fact]
    public async Task Carta_publica_lista_categorias_y_precios_sin_autenticacion()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/productos", new { Nombre = "Caña", PrecioUnitario = 1.50m, CodigoIva = "IVA10", Categoria = "Cervezas" });
        await cliente.PostAsJsonAsync("/productos", new { Nombre = "Tortilla", PrecioUnitario = 4.50m, CodigoIva = "IVA10", Categoria = "Tapas" });

        // Un cliente sin token (como el móvil de un comensal) puede ver la carta.
        var publico = _fabrica.CreateClient();
        var resp = await publico.GetAsync(new Uri($"/carta/{empresaId}/datos", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var carta = (await resp.Content.ReadFromJsonAsync<CartaResp>())!;
        carta.Local.Should().Be("Empresa de Pruebas SL");
        carta.Categorias.Should().Contain(c => c.Nombre == "Cervezas" && c.Items.Any(i => i.Nombre == "Caña" && i.Precio == 1.50m));
        carta.Categorias.Should().Contain(c => c.Nombre == "Tapas" && c.Items.Any(i => i.Nombre == "Tortilla"));
    }

    [Fact]
    public async Task El_qr_de_la_carta_se_sirve_como_svg()
    {
        var (_, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var publico = _fabrica.CreateClient();

        var qr = await publico.GetAsync(new Uri($"/carta/{empresaId}/qr.svg", UriKind.Relative));
        qr.StatusCode.Should().Be(HttpStatusCode.OK);
        qr.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        (await qr.Content.ReadAsStringAsync()).Should().Contain("<svg");
    }

    [Fact]
    public async Task Carta_de_un_local_inexistente_da_404()
    {
        var publico = _fabrica.CreateClient();
        var resp = await publico.GetAsync(new Uri($"/carta/{Guid.NewGuid()}/datos", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

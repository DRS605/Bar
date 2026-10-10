using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de pedir el menú del día desde la carta QR (autopedido).</summary>
public sealed class MenuDiaPedidoEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public MenuDiaPedidoEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record MesaResp(Guid Id);
    private sealed record CartaLinkResp(string Url, Guid Token);
    private sealed record LineaPedidoResp(Guid ProductoId, string Descripcion, decimal Cantidad, string? Nota);
    private sealed record PedidoResp(Guid Id, List<LineaPedidoResp> Lineas);
    private sealed record LineaComandaResp(string Descripcion, decimal PrecioUnitario);
    private sealed record ComandaResp(Guid Id, List<LineaComandaResp> Lineas);

    [Fact]
    public async Task El_cliente_pide_el_menu_del_dia_y_el_camarero_lo_acepta()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);

        await cliente.PutAsJsonAsync("/carta/menu-dia", new
        {
            Precio = 12.95m, Activo = true, Incluye = "Pan y bebida",
            Secciones = new[]
            {
                new { Titulo = "Primeros", Platos = new[] { "Sopa", "Ensalada" } },
                new { Titulo = "Segundos", Platos = new[] { "Pollo asado" } },
            },
        });

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var link = (await cliente.GetFromJsonAsync<CartaLinkResp>($"/mesas/{mesa.Id}/carta-link"))!;

        var anon = _fabrica.CreateClient();
        var pedir = await anon.PostAsJsonAsync($"/carta/{empresaId}/mesa/{mesa.Id}/pedido", new
        {
            Token = link.Token, Idioma = "es", Items = Array.Empty<object>(),
            Menus = new[] { new { Platos = new[] { "Sopa", "Pollo asado" }, Cantidad = 1m } },
        });
        pedir.StatusCode.Should().Be(HttpStatusCode.OK);

        var pendientes = (await cliente.GetFromJsonAsync<List<PedidoResp>>("/pedidos-web"))!;
        pendientes.Should().ContainSingle();
        var linea = pendientes[0].Lineas.Single();
        linea.Descripcion.Should().Be("Menú del día");
        linea.Nota.Should().Contain("Sopa").And.Contain("Pollo asado");

        var comanda = (await (await cliente.PostAsync(new Uri($"/pedidos-web/{pendientes[0].Id}/aceptar", UriKind.Relative), content: null))
            .Content.ReadFromJsonAsync<ComandaResp>())!;
        comanda.Lineas.Should().ContainSingle(l => l.Descripcion == "Menú del día" && l.PrecioUnitario == 12.95m);
    }
}

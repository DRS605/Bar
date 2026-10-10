using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de la atribución de comandas al camarero y del arqueo por camarero.</summary>
public sealed class CamareroEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CamareroEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record MesaResp(Guid Id);
    private sealed record ComandaResp(Guid Id, string? UsuarioNombre);
    private sealed record VentasCamareroResp(string Camarero, int Comandas, decimal Total);

    [Fact]
    public async Task La_comanda_guarda_el_camarero_que_la_abre()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 }))
            .Content.ReadFromJsonAsync<MesaResp>())!;

        var abrir = await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id });
        abrir.StatusCode.Should().Be(HttpStatusCode.Created);
        var comanda = (await abrir.Content.ReadFromJsonAsync<ComandaResp>())!;

        // El usuario de las pruebas se registra con nombre "Ana".
        comanda.UsuarioNombre.Should().Be("Ana");
    }

    [Fact]
    public async Task El_arqueo_por_camarero_responde()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var resp = await cliente.GetAsync(new Uri("/comandas/ventas-por-camarero", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<List<VentasCamareroResp>>()).Should().NotBeNull();
    }
}

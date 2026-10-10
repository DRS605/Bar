using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de la reserva online por el cliente desde la carta pública.</summary>
public sealed class CartaReservaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CartaReservaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ReservaResp(Guid Id, string NombreCliente, int Comensales, string Estado);

    [Fact]
    public async Task El_cliente_solicita_una_reserva_y_el_local_la_ve()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var anon = _fabrica.CreateClient();

        var resp = await anon.PostAsJsonAsync($"/carta/{empresaId}/reserva", new
        {
            Nombre = "Dani", FechaHora = DateTimeOffset.UtcNow.AddDays(1), Comensales = 4, Telefono = "600111222", Notas = "junto a la ventana",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var reservas = (await cliente.GetFromJsonAsync<List<ReservaResp>>("/reservas"))!;
        reservas.Should().Contain(r => r.NombreCliente == "Dani" && r.Comensales == 4 && r.Estado == "Pendiente");
    }

    [Fact]
    public async Task En_Essential_la_reserva_online_no_existe()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PutAsJsonAsync("/suscripcion", new { Plan = "Essential" })).EnsureSuccessStatusCode();

        var anon = _fabrica.CreateClient();
        var resp = await anon.PostAsJsonAsync($"/carta/{empresaId}/reserva", new { Nombre = "Dani", FechaHora = DateTimeOffset.UtcNow.AddDays(1), Comensales = 2 });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

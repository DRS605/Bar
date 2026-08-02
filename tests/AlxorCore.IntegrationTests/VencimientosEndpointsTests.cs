using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del plazo de vencimiento de las facturas.</summary>
public sealed class VencimientosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VencimientosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, DateOnly FechaEmision, DateOnly FechaVencimiento);

    [Fact]
    public async Task Los_dias_de_vencimiento_fijan_la_fecha_de_vencimiento()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Venc" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;

        var f = await (await cliente.PostAsJsonAsync("/facturas", new
        {
            ClienteId = clienteId,
            DiasVencimiento = 30,
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        })).Content.ReadFromJsonAsync<FacturaResp>();

        f!.FechaVencimiento.Should().Be(f.FechaEmision.AddDays(30));
    }

    [Fact]
    public async Task Sin_dias_el_vencimiento_es_la_emision_contado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Contado" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;

        var f = await (await cliente.PostAsJsonAsync("/facturas", new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        })).Content.ReadFromJsonAsync<FacturaResp>();

        f!.FechaVencimiento.Should().Be(f.FechaEmision);
    }
}

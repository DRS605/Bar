using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Gastos.</summary>
public sealed class GastosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public GastosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record GastoDto(Guid Id, string Concepto, decimal BaseImponible, decimal CuotaIva, decimal RetencionIrpf, decimal Total, string Estado);

    [Fact]
    public async Task Registrar_gasto_calcula_iva_y_total()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var registrar = await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Material", BaseImponible = 200m, CodigoIva = "IVA21", ProveedorTexto = "Papelería SL" });
        registrar.StatusCode.Should().Be(HttpStatusCode.Created);
        var gasto = await registrar.Content.ReadFromJsonAsync<GastoDto>();

        gasto!.CuotaIva.Should().Be(42m);
        gasto.Total.Should().Be(242m);
    }

    [Fact]
    public async Task Listar_y_obtener_gasto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var creado = await (await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Luz", BaseImponible = 50m, CodigoIva = "IVA21" })).Content.ReadFromJsonAsync<GastoDto>();

        var lista = await cliente.GetFromJsonAsync<List<GastoDto>>("/gastos");
        lista.Should().ContainSingle(g => g.Id == creado!.Id);

        var obtenido = await cliente.GetFromJsonAsync<GastoDto>($"/gastos/{creado!.Id}");
        obtenido!.Concepto.Should().Be("Luz");
    }

    [Fact]
    public async Task Registrar_gasto_sin_concepto_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var registrar = await cliente.PostAsJsonAsync("/gastos", new { Concepto = "", BaseImponible = 10m });
        registrar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Los_gastos_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/gastos", new { Concepto = "Gasto de A", BaseImponible = 10m });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<GastoDto>>("/gastos");

        listaB.Should().BeEmpty();
    }
}

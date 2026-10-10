using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de los movimientos de caja y el arqueo del día.</summary>
public sealed class CajaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CajaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record MovResp(Guid Id, string Tipo, decimal Importe, string? Concepto);
    private sealed record ArqueoResp(decimal Fondo, decimal CobrosEfectivo, decimal Entradas, decimal Salidas, decimal EfectivoTeorico, List<MovResp> Movimientos);

    [Fact]
    public async Task Registrar_movimientos_y_calcular_el_arqueo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        (await cliente.PostAsJsonAsync("/caja/movimientos", new { Tipo = "FondoInicial", Importe = 100m, Concepto = "cambio" })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await cliente.PostAsJsonAsync("/caja/movimientos", new { Tipo = "Entrada", Importe = 50m })).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync("/caja/movimientos", new { Tipo = "Salida", Importe = 30m, Concepto = "proveedor" })).EnsureSuccessStatusCode();

        var arqueo = (await cliente.GetFromJsonAsync<ArqueoResp>("/caja/arqueo"))!;
        arqueo.Fondo.Should().Be(100m);
        arqueo.Entradas.Should().Be(50m);
        arqueo.Salidas.Should().Be(30m);
        arqueo.CobrosEfectivo.Should().Be(0m);
        arqueo.EfectivoTeorico.Should().Be(120m);
        arqueo.Movimientos.Should().HaveCount(3);
    }

    [Fact]
    public async Task Eliminar_un_movimiento()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var creado = await cliente.PostAsJsonAsync("/caja/movimientos", new { Tipo = "Entrada", Importe = 10m });
        creado.EnsureSuccessStatusCode();
        var mov = (await creado.Content.ReadFromJsonAsync<MovResp>())!;

        (await cliente.DeleteAsync(new Uri($"/caja/movimientos/{mov.Id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var lista = (await cliente.GetFromJsonAsync<List<MovResp>>("/caja/movimientos"))!;
        lista.Should().BeEmpty();
    }

    [Fact]
    public async Task Importe_invalido_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PostAsJsonAsync("/caja/movimientos", new { Tipo = "Entrada", Importe = 0m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de propina y pago mixto (efectivo + tarjeta) al cobrar una comanda.</summary>
public sealed class PropinaPagoMixtoTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public PropinaPagoMixtoTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id);
    private sealed record MesaResp(Guid Id);
    private sealed record ComandaResp(Guid Id);
    private sealed record MetodoResp(string Metodo, decimal Importe, int Numero);
    private sealed record CierreResp(decimal TotalCobrado, List<MetodoResp> CobrosPorMetodo);
    private sealed record ArqueoResp(decimal CobrosEfectivo, decimal Entradas, decimal EfectivoTeorico);

    [Fact]
    public async Task Pago_mixto_y_propina_cuadran_en_caja()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var prod = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Combo", PrecioUnitario = 10m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false })).Content.ReadFromJsonAsync<ProductoResp>())!;
        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = prod.Id, Cantidad = 1m });

        // Total 10 €: 6 en efectivo + 4 con tarjeta, y 2 € de propina (efectivo).
        var cobro = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar", new { Metodo = "Mixto", ImporteEfectivo = 6m, Propina = 2m });
        cobro.StatusCode.Should().Be(HttpStatusCode.OK);

        var hoy = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cierre = (await cliente.GetFromJsonAsync<CierreResp>($"/informes/cierre-caja?dia={hoy}"))!;
        cierre.CobrosPorMetodo.Should().Contain(m => m.Metodo == "Efectivo" && m.Importe == 6m);
        cierre.CobrosPorMetodo.Should().Contain(m => m.Metodo == "Tarjeta" && m.Importe == 4m);

        var arqueo = (await cliente.GetFromJsonAsync<ArqueoResp>($"/caja/arqueo?dia={hoy}"))!;
        arqueo.CobrosEfectivo.Should().Be(6m);
        arqueo.Entradas.Should().Be(2m);          // la propina entra en la caja
        arqueo.EfectivoTeorico.Should().Be(8m);   // 6 cobrado + 2 propina
    }
}

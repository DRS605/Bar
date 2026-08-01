using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Tesorería (cobros/pagos, saldo y sobrepago).</summary>
public sealed class TesoreriaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public TesoreriaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, decimal Total);
    private sealed record GastoResp(Guid Id, decimal Total);
    private sealed record SaldoResp(decimal Total, decimal Liquidado, decimal Pendiente, string Estado);

    private static async Task<FacturaResp> EmitirFacturaAsync(HttpClient cliente, decimal precio)
    {
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente SL", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = precio, CodigoIva = "IVA0" } } };
        return (await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>())!;
    }

    [Fact]
    public async Task Cobro_parcial_y_total_actualizan_el_saldo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var factura = await EmitirFacturaAsync(cliente, 100m); // total 100 (IVA 0)

        var parcial = await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 40m });
        parcial.StatusCode.Should().Be(HttpStatusCode.OK);
        var saldo1 = await parcial.Content.ReadFromJsonAsync<SaldoResp>();
        saldo1!.Liquidado.Should().Be(40m);
        saldo1.Pendiente.Should().Be(60m);
        saldo1.Estado.Should().Be("Parcial");

        var resto = await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 60m });
        var saldo2 = await resto.Content.ReadFromJsonAsync<SaldoResp>();
        saldo2!.Pendiente.Should().Be(0m);
        saldo2.Estado.Should().Be("Liquidado");
    }

    [Fact]
    public async Task No_se_permite_sobrepago()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var factura = await EmitirFacturaAsync(cliente, 100m);

        var sobrepago = await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 150m });
        sobrepago.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Consultar_saldo_de_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var factura = await EmitirFacturaAsync(cliente, 200m);
        await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 50m });

        var saldo = await cliente.GetFromJsonAsync<SaldoResp>($"/facturas/{factura.Id}/saldo");
        saldo!.Total.Should().Be(200m);
        saldo.Liquidado.Should().Be(50m);
        saldo.Pendiente.Should().Be(150m);
    }

    [Fact]
    public async Task Pago_de_un_gasto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var gasto = (await (await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Servicio", BaseImponible = 100m, CodigoIva = "IVA0" })).Content.ReadFromJsonAsync<GastoResp>())!;

        var pago = await cliente.PostAsJsonAsync("/pagos", new { GastoId = gasto.Id, Importe = 100m });
        pago.StatusCode.Should().Be(HttpStatusCode.OK);
        var saldo = await pago.Content.ReadFromJsonAsync<SaldoResp>();
        saldo!.Estado.Should().Be("Liquidado");
    }
}

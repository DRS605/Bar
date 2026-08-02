using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de VeriFactu: huella y encadenamiento entre facturas.</summary>
public sealed class VerifactuEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VerifactuEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, string Tipo, string? Huella, string? HuellaAnterior);

    private static async Task<Guid> ClienteAsync(HttpClient c) =>
        (await (await c.PostAsJsonAsync("/clientes", new { Nombre = "Cliente VF", NifFiscal = "12345678Z" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;

    private static object Linea(decimal p) => new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = p, CodigoIva = "IVA21" };

    [Fact]
    public async Task Cada_factura_tiene_huella_y_se_encadena_con_la_anterior()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await ClienteAsync(cliente);

        var f1 = await (await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { Linea(100m) } }))
            .Content.ReadFromJsonAsync<FacturaResp>();
        var f2 = await (await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { Linea(200m) } }))
            .Content.ReadFromJsonAsync<FacturaResp>();

        f1!.Huella.Should().NotBeNullOrEmpty();
        f1.HuellaAnterior.Should().BeNull(); // primera de la cadena
        f2!.HuellaAnterior.Should().Be(f1.Huella); // encadenada
        f2.Huella.Should().NotBe(f1.Huella);
    }

    [Fact]
    public async Task Un_ticket_tambien_lleva_huella_y_entra_en_la_cadena()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await ClienteAsync(cliente);

        var factura = await (await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { Linea(100m) } }))
            .Content.ReadFromJsonAsync<FacturaResp>();
        var ticket = await (await cliente.PostAsJsonAsync("/tickets", new { Lineas = new[] { Linea(10m) } }))
            .Content.ReadFromJsonAsync<FacturaResp>();

        ticket!.Tipo.Should().Be("Simplificada");
        ticket.Huella.Should().NotBeNullOrEmpty();
        ticket.HuellaAnterior.Should().Be(factura!.Huella);
    }
}

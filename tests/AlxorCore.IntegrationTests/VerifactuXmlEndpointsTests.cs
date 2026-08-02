using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del registro de alta VeriFactu en XML.</summary>
public sealed class VerifactuXmlEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VerifactuXmlEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, string? Huella);

    [Fact]
    public async Task El_xml_de_alta_contiene_los_campos_clave_y_la_huella()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente XML", NifFiscal = "12345678Z" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var factura = await (await cliente.PostAsJsonAsync("/facturas", new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        })).Content.ReadFromJsonAsync<FacturaResp>();

        var resp = await cliente.GetAsync(new Uri($"/facturas/{factura!.Id}/verifactu.xml", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        var xml = await resp.Content.ReadAsStringAsync();
        xml.Should().Contain("<RegistroAlta>");
        xml.Should().Contain("<NumSerieFactura>" + factura.NumeroCompleto + "</NumSerieFactura>");
        xml.Should().Contain("<TipoFactura>F1</TipoFactura>");
        xml.Should().Contain("<TipoImpositivo>21.00</TipoImpositivo>");
        xml.Should().Contain("<ImporteTotal>121.00</ImporteTotal>");
        xml.Should().Contain("<PrimerRegistro>S</PrimerRegistro>"); // primera factura de la cadena
        xml.Should().Contain("<Huella>" + factura.Huella + "</Huella>");
    }

    [Fact]
    public async Task El_ticket_se_marca_como_F2_en_el_xml()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var ticket = await (await cliente.PostAsJsonAsync("/tickets", new
        {
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Café", PrecioUnitario = 2m, CodigoIva = "IVA10" } },
        })).Content.ReadFromJsonAsync<FacturaResp>();

        var xml = await cliente.GetStringAsync($"/facturas/{ticket!.Id}/verifactu.xml");
        xml.Should().Contain("<TipoFactura>F2</TipoFactura>");
    }
}

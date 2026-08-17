using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using AlxorCore.Api.Comun;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del registro de alta VeriFactu en XML (documento remisible a la AEAT).</summary>
public sealed class VerifactuXmlEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VerifactuXmlEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, string? Huella);

    private static XName Info(string nombre) => XName.Get(nombre, GeneradorXmlVerifactu.NsInfo);
    private static XName Lr(string nombre) => XName.Get(nombre, GeneradorXmlVerifactu.NsLR);

    private static string? Valor(XDocument doc, string nombreLocal) =>
        doc.Descendants(Info(nombreLocal)).FirstOrDefault()?.Value;

    [Fact]
    public async Task El_xml_es_un_registro_remisible_con_cabecera_desglose_y_huella()
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

        var doc = XDocument.Parse(await resp.Content.ReadAsStringAsync());

        // Sobre y cabecera con los espacios de nombres oficiales.
        doc.Root!.Name.Should().Be(Lr("RegFactuSistemaFacturacion"));
        doc.Descendants(Lr("Cabecera")).Should().ContainSingle();
        doc.Descendants(Info("ObligadoEmision")).Descendants(Info("NIF")).First().Value.Should().NotBeNullOrEmpty();
        doc.Descendants(Info("RegistroAlta")).Should().ContainSingle();

        // Contenido del registro.
        Valor(doc, "NumSerieFactura").Should().Be(factura.NumeroCompleto);
        Valor(doc, "TipoFactura").Should().Be("F1");
        Valor(doc, "TipoImpositivo").Should().Be("21.00");
        Valor(doc, "ImporteTotal").Should().Be("121.00");
        Valor(doc, "PrimerRegistro").Should().Be("S"); // primera factura de la cadena
        Valor(doc, "Huella").Should().Be(factura.Huella);
    }

    [Fact]
    public async Task El_ticket_se_marca_como_F2_en_el_xml()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var ticket = await (await cliente.PostAsJsonAsync("/tickets", new
        {
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Café", PrecioUnitario = 2m, CodigoIva = "IVA10" } },
        })).Content.ReadFromJsonAsync<FacturaResp>();

        var doc = XDocument.Parse(await cliente.GetStringAsync($"/facturas/{ticket!.Id}/verifactu.xml"));
        Valor(doc, "TipoFactura").Should().Be("F2");
    }
}

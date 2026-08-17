using System.Net;
using System.Net.Http.Json;
using AlxorCore.Api.Servicios.Verifactu;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de la remisión VeriFactu: sobre SOAP (unitario) y el endpoint sin certificado.</summary>
public sealed class VerifactuRemisionTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public VerifactuRemisionTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id);

    [Fact]
    public void El_sobre_soap_envuelve_el_registro_sin_su_declaracion_xml()
    {
        var registro = "<?xml version=\"1.0\" encoding=\"utf-8\"?><sum:RegFactuSistemaFacturacion>x</sum:RegFactuSistemaFacturacion>";

        var sobre = SobreSoapVerifactu.Construir(registro);

        sobre.Should().StartWith("<soapenv:Envelope");
        sobre.Should().Contain("<soapenv:Body>").And.Contain("</soapenv:Body>");
        sobre.Should().Contain("<sum:RegFactuSistemaFacturacion>x</sum:RegFactuSistemaFacturacion>");
        sobre.Should().NotContain("<?xml"); // la declaración del registro se quita al incrustarlo
    }

    [Fact]
    public void Lee_estado_csv_y_error_de_la_respuesta_de_la_aeat()
    {
        var ok = "<env:Envelope xmlns:env=\"http://schemas.xmlsoap.org/soap/envelope/\"><env:Body>" +
                 "<Respuesta><EstadoEnvio>Correcto</EstadoEnvio><CSV>ABC123XYZ</CSV></Respuesta></env:Body></env:Envelope>";
        var r = SobreSoapVerifactu.LeerRespuesta(ok);
        r.Estado.Should().Be("Correcto");
        r.Csv.Should().Be("ABC123XYZ");

        var err = "<Respuesta><EstadoEnvio>Incorrecto</EstadoEnvio>" +
                  "<CodigoErrorRegistro>4102</CodigoErrorRegistro><DescripcionErrorRegistro>NIF no identificado</DescripcionErrorRegistro></Respuesta>";
        var e = SobreSoapVerifactu.LeerRespuesta(err);
        e.Estado.Should().Be("Incorrecto");
        e.CodigoError.Should().Be("4102");
        e.DescripcionError.Should().Be("NIF no identificado");
    }

    [Fact]
    public async Task Remitir_sin_certificado_configurado_devuelve_error_legible()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente VF", NifFiscal = "12345678Z" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var factura = (await (await cliente.PostAsJsonAsync("/facturas", new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        })).Content.ReadFromJsonAsync<FacturaResp>())!;

        var resp = await cliente.PostAsync(new Uri($"/facturas/{factura.Id}/verifactu/remitir", UriKind.Relative), content: null);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("verifactu.no_configurado");
    }
}

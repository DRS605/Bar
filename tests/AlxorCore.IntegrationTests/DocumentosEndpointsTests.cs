using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Documentos (PDF y envío de facturas).</summary>
public sealed class DocumentosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public DocumentosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id);

    private static async Task<Guid> EmitirFacturaAsync(HttpClient cliente)
    {
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente PDF SL", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 2m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        return (await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>())!.Id;
    }

    [Fact]
    public async Task Descargar_pdf_de_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var facturaId = await EmitirFacturaAsync(cliente);

        var respuesta = await cliente.GetAsync(new Uri($"/facturas/{facturaId}/pdf", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Enviar_factura_por_correo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var facturaId = await EmitirFacturaAsync(cliente);

        var enviar = await cliente.PostAsJsonAsync($"/facturas/{facturaId}/enviar", new { Email = "cliente@ejemplo.com" });
        enviar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

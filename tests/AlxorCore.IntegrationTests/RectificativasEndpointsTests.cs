using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de las facturas rectificativas.</summary>
public sealed class RectificativasEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public RectificativasEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, string Estado, string Tipo, Guid? RectificaFacturaId, string? MotivoRectificacion, string? Huella, string? HuellaAnterior);

    private static async Task<Guid> ClienteAsync(HttpClient c) =>
        (await (await c.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Rect" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;

    private static object Linea(decimal p) => new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = p, CodigoIva = "IVA21" };

    private static async Task<FacturaResp> EmitirFacturaAsync(HttpClient c, Guid clienteId) =>
        (await (await c.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { Linea(100m) } })).Content.ReadFromJsonAsync<FacturaResp>())!;

    [Fact]
    public async Task Rectificar_emite_una_R1_encadenada_y_marca_la_original_rectificada()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await ClienteAsync(cliente);
        var original = await EmitirFacturaAsync(cliente, clienteId);
        var anio = DateTime.UtcNow.Year;

        var resp = await cliente.PostAsJsonAsync($"/facturas/{original.Id}/rectificar",
            new { Motivo = "Descuento no aplicado", Lineas = new[] { Linea(80m) } });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var rect = await resp.Content.ReadFromJsonAsync<FacturaResp>();
        rect!.Tipo.Should().Be("Rectificativa");
        rect.NumeroCompleto.Should().Be($"R{anio}/000001");
        rect.RectificaFacturaId.Should().Be(original.Id);
        rect.MotivoRectificacion.Should().Be("Descuento no aplicado");
        rect.HuellaAnterior.Should().Be(original.Huella); // encadenada en VeriFactu

        var originalTrasRect = await cliente.GetFromJsonAsync<FacturaResp>($"/facturas/{original.Id}");
        originalTrasRect!.Estado.Should().Be("Rectificada");
    }

    [Fact]
    public async Task No_se_puede_rectificar_dos_veces_la_misma_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await ClienteAsync(cliente);
        var original = await EmitirFacturaAsync(cliente, clienteId);

        await cliente.PostAsJsonAsync($"/facturas/{original.Id}/rectificar", new { Motivo = "Primera", Lineas = new[] { Linea(80m) } });
        var segunda = await cliente.PostAsJsonAsync($"/facturas/{original.Id}/rectificar", new { Motivo = "Segunda", Lineas = new[] { Linea(70m) } });

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rectificar_sin_motivo_se_rechaza()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await ClienteAsync(cliente);
        var original = await EmitirFacturaAsync(cliente, clienteId);

        var resp = await cliente.PostAsJsonAsync($"/facturas/{original.Id}/rectificar", new { Motivo = "", Lineas = new[] { Linea(80m) } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rectificar_una_factura_inexistente_devuelve_404()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var resp = await cliente.PostAsJsonAsync($"/facturas/{Guid.NewGuid()}/rectificar", new { Motivo = "X", Lineas = new[] { Linea(80m) } });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

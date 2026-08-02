using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de las series de numeración y su uso al emitir facturas.</summary>
public sealed class SeriesEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public SeriesEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record SerieDto(Guid Id, string TipoDocumento, int Ejercicio, string Prefijo, long SiguienteNumero);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente)
    {
        var resp = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Serie" });
        return (await resp.Content.ReadFromJsonAsync<ClienteResp>())!.Id;
    }

    private static object Factura(Guid clienteId, string? serie = null) => new
    {
        ClienteId = clienteId,
        Serie = serie,
        Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
    };

    [Fact]
    public async Task Crear_y_listar_series()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var anio = DateTime.UtcNow.Year;

        var crear = await cliente.PostAsJsonAsync("/series", new { TipoDocumento = "Factura", Ejercicio = anio, Prefijo = "R" });
        crear.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await cliente.GetFromJsonAsync<List<SerieDto>>("/series");
        lista.Should().Contain(s => s.Prefijo == "R" && s.Ejercicio == anio);
    }

    [Fact]
    public async Task Emitir_en_una_serie_usa_su_prefijo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var anio = DateTime.UtcNow.Year;

        var resp = await cliente.PostAsJsonAsync("/facturas", Factura(clienteId, "R"));
        var factura = await resp.Content.ReadFromJsonAsync<FacturaResp>();

        factura!.NumeroCompleto.Should().Be($"R{anio}/000001");
    }

    [Fact]
    public async Task Cada_serie_lleva_su_propia_numeracion_correlativa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var anio = DateTime.UtcNow.Year;

        // Serie por defecto (FA) y serie propia (T) numeran de forma independiente.
        var fa1 = await (await cliente.PostAsJsonAsync("/facturas", Factura(clienteId))).Content.ReadFromJsonAsync<FacturaResp>();
        var t1 = await (await cliente.PostAsJsonAsync("/facturas", Factura(clienteId, "T"))).Content.ReadFromJsonAsync<FacturaResp>();
        var fa2 = await (await cliente.PostAsJsonAsync("/facturas", Factura(clienteId))).Content.ReadFromJsonAsync<FacturaResp>();
        var t2 = await (await cliente.PostAsJsonAsync("/facturas", Factura(clienteId, "T"))).Content.ReadFromJsonAsync<FacturaResp>();

        fa1!.NumeroCompleto.Should().Be($"FA{anio}/000001");
        fa2!.NumeroCompleto.Should().Be($"FA{anio}/000002");
        t1!.NumeroCompleto.Should().Be($"T{anio}/000001");
        t2!.NumeroCompleto.Should().Be($"T{anio}/000002");
    }
}

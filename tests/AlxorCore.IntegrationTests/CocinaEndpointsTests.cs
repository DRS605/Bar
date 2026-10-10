using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de las zonas de preparación y la pantalla de cocina (KDS).</summary>
public sealed class CocinaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CocinaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id);
    private sealed record MesaResp(Guid Id);
    private sealed record ComandaResp(Guid Id);
    private sealed record ZonaResp(Guid ProductoId, string Zona);
    private sealed record ItemResp(Guid ComandaId, Guid LineaId, string Mesa, string Descripcion, decimal Cantidad, string Zona, string? Nota);

    private async Task<Guid> CrearProducto(HttpClient c, string nombre) =>
        (await (await c.PostAsJsonAsync("/productos", new { Nombre = nombre, PrecioUnitario = 2m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false })).Content.ReadFromJsonAsync<ProductoResp>())!.Id;

    [Fact]
    public async Task Fijar_zona_y_leerla()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prod = await CrearProducto(cliente, "Cerveza");

        (await cliente.PutAsJsonAsync($"/carta/zonas/{prod}", new { Zona = "Barra" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var zonas = (await cliente.GetFromJsonAsync<List<ZonaResp>>("/carta/zonas"))!;
        zonas.Should().ContainSingle(z => z.ProductoId == prod && z.Zona == "Barra");
    }

    [Fact]
    public async Task Flujo_cocina_enviar_ver_y_servir()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prod = await CrearProducto(cliente, "Tortilla");
        await cliente.PutAsJsonAsync($"/carta/zonas/{prod}", new { Zona = "Cocina" });

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = prod, Cantidad = 2m });

        // Antes de enviar a cocina no hay nada pendiente de servir.
        (await cliente.GetFromJsonAsync<List<ItemResp>>("/cocina/pendientes"))!.Should().BeEmpty();

        (await cliente.PostAsync(new Uri($"/comandas/{comanda.Id}/cocina", UriKind.Relative), content: null)).EnsureSuccessStatusCode();

        var pendientes = (await cliente.GetFromJsonAsync<List<ItemResp>>("/cocina/pendientes"))!;
        pendientes.Should().ContainSingle();
        pendientes[0].Zona.Should().Be("Cocina");
        pendientes[0].Cantidad.Should().Be(2m);

        // Filtro por zona: en Barra no aparece.
        (await cliente.GetFromJsonAsync<List<ItemResp>>("/cocina/pendientes?zona=Barra"))!.Should().BeEmpty();

        // Servir y comprobar que desaparece.
        (await cliente.PostAsJsonAsync("/cocina/servir", new { pendientes[0].ComandaId, pendientes[0].LineaId })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<List<ItemResp>>("/cocina/pendientes"))!.Should().BeEmpty();
    }
}

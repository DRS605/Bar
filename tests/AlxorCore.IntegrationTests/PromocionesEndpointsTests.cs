using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de las promociones: alta y aplicación automática del descuento en la comanda.</summary>
public sealed class PromocionesEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public PromocionesEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id);
    private sealed record MesaResp(Guid Id);
    private sealed record PromoResp(Guid Id, string Nombre, decimal Porcentaje, bool Activa);
    private sealed record LineaResp(string Descripcion, decimal PrecioUnitario);
    private sealed record ComandaResp(Guid Id, List<LineaResp> Lineas);

    [Fact]
    public async Task La_promocion_de_categoria_se_aplica_al_anadir_a_la_comanda()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var prod = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Caña", PrecioUnitario = 2m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false, Categoria = "Cervezas" })).Content.ReadFromJsonAsync<ProductoResp>())!;

        (await cliente.PostAsJsonAsync("/promociones", new { Nombre = "-50% cervezas", Porcentaje = 50m, Ambito = "Categoria", Categoria = "Cervezas" })).StatusCode.Should().Be(HttpStatusCode.Created);

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;

        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = prod.Id, Cantidad = 1m })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var linea = conLinea.Lineas.Single();
        linea.PrecioUnitario.Should().Be(1m); // 2 € − 50 %
        linea.Descripcion.Should().Contain("-50");
    }

    [Fact]
    public async Task Pausar_una_promocion_evita_que_se_aplique()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prod = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Vino", PrecioUnitario = 2m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false, Categoria = "Vinos" })).Content.ReadFromJsonAsync<ProductoResp>())!;
        var promo = (await (await cliente.PostAsJsonAsync("/promociones", new { Nombre = "-10% vinos", Porcentaje = 10m, Ambito = "Categoria", Categoria = "Vinos" })).Content.ReadFromJsonAsync<PromoResp>())!;

        (await cliente.PutAsJsonAsync($"/promociones/{promo.Id}/activa", new { Activa = false })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "M", Zona = "S", Capacidad = 2 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = prod.Id, Cantidad = 1m })).Content.ReadFromJsonAsync<ComandaResp>())!;

        conLinea.Lineas.Single().PrecioUnitario.Should().Be(2m); // sin descuento
    }
}

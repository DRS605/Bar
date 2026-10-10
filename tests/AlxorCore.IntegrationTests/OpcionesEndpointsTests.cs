using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de los formatos/extras de producto: configuración y composición de precio en la comanda.</summary>
public sealed class OpcionesEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public OpcionesEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id);
    private sealed record MesaResp(Guid Id);
    private sealed record OpcionResp(Guid Id, string Nombre, decimal PrecioDelta);
    private sealed record GrupoResp(Guid Id, string Nombre, string Seleccion, bool Obligatorio, List<OpcionResp> Opciones);
    private sealed record LineaResp(Guid ProductoId, string Descripcion, decimal PrecioUnitario);
    private sealed record ComandaResp(Guid Id, List<LineaResp> Lineas);

    [Fact]
    public async Task Configurar_opciones_y_componer_precio_en_la_comanda()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var prod = (await (await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = "Croquetas", PrecioUnitario = 10m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false,
        })).Content.ReadFromJsonAsync<ProductoResp>())!;

        var put = await cliente.PutAsJsonAsync($"/carta/opciones/{prod.Id}", new
        {
            Grupos = new object[]
            {
                new { Nombre = "Tamaño", Seleccion = "Unica", Obligatorio = true, Opciones = new[] { new { Nombre = "Media ración", PrecioDelta = -3m }, new { Nombre = "Ración", PrecioDelta = 0m } } },
                new { Nombre = "Extras", Seleccion = "Multiple", Obligatorio = false, Opciones = new[] { new { Nombre = "Queso", PrecioDelta = 1m } } },
            },
        });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var grupos = (await cliente.GetFromJsonAsync<List<GrupoResp>>($"/carta/opciones/{prod.Id}"))!;
        grupos.Should().HaveCount(2);
        var media = grupos.First(g => g.Nombre == "Tamaño").Opciones.First(o => o.Nombre == "Media ración");
        var queso = grupos.First(g => g.Nombre == "Extras").Opciones.First();

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Mesa 1", Zona = "Salón", Capacidad = 4 }))
            .Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id }))
            .Content.ReadFromJsonAsync<ComandaResp>())!;

        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new
        {
            ProductoId = prod.Id, Cantidad = 1m, OpcionIds = new[] { media.Id, queso.Id },
        })).Content.ReadFromJsonAsync<ComandaResp>())!;

        var linea = conLinea.Lineas.Single();
        linea.PrecioUnitario.Should().Be(8m); // 10 − 3 + 1
        linea.Descripcion.Should().Contain("Media ración").And.Contain("Queso");
    }

    [Fact]
    public async Task Falta_la_opcion_obligatoria_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prod = (await (await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = "Menú", PrecioUnitario = 12m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false,
        })).Content.ReadFromJsonAsync<ProductoResp>())!;

        await cliente.PutAsJsonAsync($"/carta/opciones/{prod.Id}", new
        {
            Grupos = new object[] { new { Nombre = "Primero", Seleccion = "Unica", Obligatorio = true, Opciones = new[] { new { Nombre = "Sopa", PrecioDelta = 0m } } } },
        });

        var mesa = (await (await cliente.PostAsJsonAsync("/mesas", new { Nombre = "M", Zona = "S", Capacidad = 2 })).Content.ReadFromJsonAsync<MesaResp>())!;
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;

        var resp = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = prod.Id, Cantidad = 1m, OpcionIds = new[] { Guid.NewGuid() } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

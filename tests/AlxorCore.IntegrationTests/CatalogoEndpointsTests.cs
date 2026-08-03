using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Catálogo (productos e impuestos).</summary>
public sealed class CatalogoEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public CatalogoEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoDto(Guid Id, string Nombre, decimal PrecioUnitario, string CodigoIva, decimal PorcentajeIva, bool Activo, decimal PrecioCompra, Guid? ProveedorHabitualId);
    private sealed record ProveedorResp(Guid Id);

    private sealed record ImpuestoDto(string Codigo, string Nombre, string Tipo, decimal Porcentaje);

    private sealed record HistoricoPrecioResp(DateTimeOffset RegistradoEn, decimal PrecioVenta, decimal PrecioCompra);

    [Fact]
    public async Task Listar_impuestos_devuelve_los_tipos_de_iva()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var impuestos = await cliente.GetFromJsonAsync<List<ImpuestoDto>>("/impuestos");

        impuestos.Should().Contain(i => i.Codigo == "IVA21" && i.Porcentaje == 21m);
        impuestos.Should().Contain(i => i.Codigo == "IVA4");
    }

    [Fact]
    public async Task Crear_listar_y_obtener_producto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/productos", new { Nombre = "Consultoría", PrecioUnitario = 90m, Tipo = "Servicio", CodigoIva = "IVA21" });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<ProductoDto>();
        creado!.PorcentajeIva.Should().Be(21m);

        var lista = await cliente.GetFromJsonAsync<List<ProductoDto>>("/productos");
        lista.Should().ContainSingle(p => p.Id == creado.Id);

        var obtenido = await cliente.GetFromJsonAsync<ProductoDto>($"/productos/{creado.Id}");
        obtenido!.Nombre.Should().Be("Consultoría");
    }

    [Fact]
    public async Task Un_articulo_puede_tener_proveedor_habitual()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prov = (await (await cliente.PostAsJsonAsync("/proveedores", new { Nombre = "Mayorista SL" })).Content.ReadFromJsonAsync<ProveedorResp>())!;

        var creado = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Artículo", PrecioUnitario = 10m, CodigoIva = "IVA21", ProveedorHabitualId = prov.Id }))
            .Content.ReadFromJsonAsync<ProductoDto>())!;
        creado.ProveedorHabitualId.Should().Be(prov.Id);

        var obtenido = await cliente.GetFromJsonAsync<ProductoDto>($"/productos/{creado.Id}");
        obtenido!.ProveedorHabitualId.Should().Be(prov.Id);
    }

    [Fact]
    public async Task El_historico_de_precios_registra_alta_y_cambios()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var creado = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Artículo", PrecioUnitario = 100m, PrecioCompra = 60m, CodigoIva = "IVA21" }))
            .Content.ReadFromJsonAsync<ProductoDto>())!;
        creado.PrecioCompra.Should().Be(60m);

        // Cambio de precios -> nueva fila de histórico.
        await cliente.PutAsJsonAsync($"/productos/{creado.Id}", new { Nombre = "Artículo", PrecioUnitario = 120m, PrecioCompra = 70m, CodigoIva = "IVA21" });

        var historico = await cliente.GetFromJsonAsync<List<HistoricoPrecioResp>>($"/productos/{creado.Id}/precios");
        historico.Should().HaveCount(2);
        historico![0].PrecioVenta.Should().Be(120m); // más reciente primero
        historico[0].PrecioCompra.Should().Be(70m);
        historico[1].PrecioVenta.Should().Be(100m);
    }

    [Fact]
    public async Task Crear_producto_con_iva_invalido_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/productos", new { Nombre = "X", PrecioUnitario = 10m, CodigoIva = "IVA99" });
        crear.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Los_productos_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/productos", new { Nombre = "Producto de A", PrecioUnitario = 5m });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<ProductoDto>>("/productos");

        listaB.Should().BeEmpty();
    }
}

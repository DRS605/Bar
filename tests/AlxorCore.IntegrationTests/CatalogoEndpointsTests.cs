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

    private sealed record ProductoDto(Guid Id, string Nombre, decimal PrecioUnitario, string CodigoIva, decimal PorcentajeIva, bool Activo);

    private sealed record ImpuestoDto(string Codigo, string Nombre, string Tipo, decimal Porcentaje);

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

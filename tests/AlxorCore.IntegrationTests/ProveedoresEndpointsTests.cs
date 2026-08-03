using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de Proveedores y su enlace con Gastos.</summary>
public sealed class ProveedoresEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public ProveedoresEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProveedorDto(Guid Id, string Nombre, string? NifFiscal, bool Activo, string FormaPago);
    private sealed record GastoDto(Guid Id, Guid? ProveedorId, string? ProveedorTexto, string Concepto, decimal Total);

    [Fact]
    public async Task Crear_listar_y_obtener_proveedor()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/proveedores", new { Nombre = "Suministros Turia SL", NifFiscal = "B12345674" });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creado = await crear.Content.ReadFromJsonAsync<ProveedorDto>();

        var lista = await cliente.GetFromJsonAsync<List<ProveedorDto>>("/proveedores");
        lista.Should().ContainSingle(p => p.Id == creado!.Id);

        var obtenido = await cliente.GetFromJsonAsync<ProveedorDto>($"/proveedores/{creado!.Id}");
        obtenido!.Nombre.Should().Be("Suministros Turia SL");
    }

    [Fact]
    public async Task Guarda_la_forma_de_pago_del_proveedor()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var creado = await (await cliente.PostAsJsonAsync("/proveedores", new { Nombre = "Mayorista SL", FormaPago = "Domiciliacion" })).Content.ReadFromJsonAsync<ProveedorDto>();
        creado!.FormaPago.Should().Be("Domiciliacion");

        var obtenido = await cliente.GetFromJsonAsync<ProveedorDto>($"/proveedores/{creado.Id}");
        obtenido!.FormaPago.Should().Be("Domiciliacion");
    }

    [Fact]
    public async Task Un_gasto_con_proveedor_copia_su_nombre()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var prov = await (await cliente.PostAsJsonAsync("/proveedores", new { Nombre = "Gestoría Bellver" })).Content.ReadFromJsonAsync<ProveedorDto>();

        var crear = await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Asesoría trimestral", BaseImponible = 120m, CodigoIva = "IVA21", ProveedorId = prov!.Id });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var gasto = await crear.Content.ReadFromJsonAsync<GastoDto>();

        gasto!.ProveedorId.Should().Be(prov.Id);
        gasto.ProveedorTexto.Should().Be("Gestoría Bellver");
    }

    [Fact]
    public async Task Gasto_con_proveedor_inexistente_devuelve_404()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var crear = await cliente.PostAsJsonAsync("/gastos", new { Concepto = "X", BaseImponible = 10m, ProveedorId = Guid.NewGuid() });
        crear.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Los_proveedores_estan_aislados_por_empresa()
    {
        var (a, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await a.PostAsJsonAsync("/proveedores", new { Nombre = "Proveedor de A" });
        var (b, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await b.GetFromJsonAsync<List<ProveedorDto>>("/proveedores");
        listaB.Should().BeEmpty();
    }
}

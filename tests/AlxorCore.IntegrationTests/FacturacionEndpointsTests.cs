using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Facturación (flujo estrella y numeración correlativa).</summary>
public sealed class FacturacionEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public FacturacionEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record LineaResp(string Descripcion, decimal Base, decimal CuotaIva);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, decimal BaseImponible, decimal CuotaIva, decimal RetencionIrpf, decimal Total, string Estado, List<LineaResp> Lineas);
    private sealed record FacturaResumen(Guid Id, string NumeroCompleto, decimal Total);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente, decimal irpf = 0m)
    {
        var crear = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Facturación SL", NifFiscal = "B12345674", PorcentajeIrpfDefecto = irpf });
        var dto = await crear.Content.ReadFromJsonAsync<ClienteResp>();
        return dto!.Id;
    }

    [Fact]
    public async Task Emitir_factura_calcula_totales_y_asigna_numero()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        var comando = new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { Cantidad = 2m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        };

        var emitir = await cliente.PostAsJsonAsync("/facturas", comando);
        emitir.StatusCode.Should().Be(HttpStatusCode.Created);
        var factura = await emitir.Content.ReadFromJsonAsync<FacturaResp>();

        factura!.BaseImponible.Should().Be(200m);
        factura.CuotaIva.Should().Be(42m);
        factura.Total.Should().Be(242m);
        factura.NumeroCompleto.Should().EndWith("/000001");
        factura.Lineas.Should().ContainSingle();
    }

    [Fact]
    public async Task La_numeracion_es_correlativa_dentro_de_la_empresa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } };

        var primera = await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>();
        var segunda = await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>();

        primera!.NumeroCompleto.Should().EndWith("/000001");
        segunda!.NumeroCompleto.Should().EndWith("/000002");
    }

    [Fact]
    public async Task Emitir_con_irpf_del_cliente_aplica_retencion()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente, irpf: 15m);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Servicio", PrecioUnitario = 1000m, CodigoIva = "IVA21" } } };

        var factura = await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>();

        factura!.RetencionIrpf.Should().Be(150m);
        factura.Total.Should().Be(1060m);
    }

    [Fact]
    public async Task Listar_y_obtener_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } };
        var emitida = await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>();

        var lista = await cliente.GetFromJsonAsync<List<FacturaResumen>>("/facturas");
        lista.Should().ContainSingle(f => f.Id == emitida!.Id);

        var obtenida = await cliente.GetFromJsonAsync<FacturaResp>($"/facturas/{emitida!.Id}");
        obtenida!.Lineas.Should().ContainSingle();
    }

    [Fact]
    public async Task Emitir_con_cliente_inexistente_devuelve_404()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var comando = new { ClienteId = Guid.NewGuid(), Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } };

        var emitir = await cliente.PostAsJsonAsync("/facturas", comando);
        emitir.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Las_facturas_estan_aisladas_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA);
        await empresaA.PostAsJsonAsync("/facturas", new { ClienteId = clienteA, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<FacturaResumen>>("/facturas");

        listaB.Should().BeEmpty();
    }

    private sealed record FacturaRecargoResp(decimal BaseImponible, decimal CuotaIva, bool RecargoEquivalencia, decimal RecargoTotal, decimal Total);

    [Fact]
    public async Task Emitir_factura_con_recargo_de_equivalencia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        var comando = new
        {
            ClienteId = clienteId,
            RecargoEquivalencia = true,
            Lineas = new[] { new { Cantidad = 1m, Descripcion = "Género", PrecioUnitario = 1000m, CodigoIva = "IVA21" } },
        };

        var factura = await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaRecargoResp>();

        factura!.BaseImponible.Should().Be(1000m);
        factura.CuotaIva.Should().Be(210m);
        factura.RecargoEquivalencia.Should().BeTrue();
        factura.RecargoTotal.Should().Be(52m);   // 5,2 % de 1000
        factura.Total.Should().Be(1262m);         // 1000 + 210 + 52
    }
}

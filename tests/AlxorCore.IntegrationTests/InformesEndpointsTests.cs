using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Informes (dashboard, libro de IVA, exportación CSV).</summary>
public sealed class InformesEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public InformesEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record FacturaResp(Guid Id, decimal Total);
    private sealed record DashboardResp(int Anio, int Mes, decimal FacturadoMes, decimal GastadoMes, int NumeroFacturasMes, decimal PendienteCobro, decimal PendientePago);
    private sealed record AsientoResp(string Documento, string Tercero, decimal Base, decimal Cuota);
    private sealed record LibroResp(string Tipo, List<AsientoResp> Asientos, decimal TotalBase, decimal TotalCuota);
    private sealed record Modelo303Resp(decimal IvaDevengadoBase, decimal IvaDevengadoCuota, decimal IvaDeducibleBase, decimal IvaDeducibleCuota, decimal Resultado);
    private sealed record Modelo130Resp(decimal IngresosAcumulados, decimal GastosAcumulados, decimal RendimientoAcumulado, decimal PagoFraccionadoBruto, decimal RetencionesAcumuladas, decimal PagosAnteriores, decimal Resultado);
    private sealed record ResumenResp(Modelo303Resp Modelo303, Modelo130Resp Modelo130);
    private sealed record ProductoResp(Guid Id);
    private sealed record BeneficioProductoResp(string Descripcion, decimal Ingresos, decimal Coste, decimal Margen);
    private sealed record BeneficioResp(decimal Ingresos, decimal Coste, decimal MargenBruto, decimal Gastos, decimal BeneficioNeto, List<BeneficioProductoResp> PorProducto);

    private static async Task<FacturaResp> EmitirFacturaAsync(HttpClient cliente)
    {
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente SL", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 2m, Descripcion = "Servicio", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        return (await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>())!;
    }

    [Fact]
    public async Task Dashboard_refleja_facturado_gastado_y_pendientes()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var factura = await EmitirFacturaAsync(cliente);   // total 242
        await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Material", BaseImponible = 100m, CodigoIva = "IVA21" }); // total 121

        var dashboard = await cliente.GetFromJsonAsync<DashboardResp>("/informes/dashboard");

        dashboard!.FacturadoMes.Should().Be(242m);
        dashboard.GastadoMes.Should().Be(121m);
        dashboard.NumeroFacturasMes.Should().Be(1);
        dashboard.PendienteCobro.Should().Be(242m);
        dashboard.PendientePago.Should().Be(121m);

        // Tras cobrar la factura, el pendiente de cobro baja a 0.
        await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 242m });
        var dashboard2 = await cliente.GetFromJsonAsync<DashboardResp>("/informes/dashboard");
        dashboard2!.PendienteCobro.Should().Be(0m);
    }

    [Fact]
    public async Task Libro_iva_repercutido_lista_las_facturas()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await EmitirFacturaAsync(cliente);

        var libro = await cliente.GetFromJsonAsync<LibroResp>("/informes/libro-iva?tipo=Repercutido&desde=2026-01-01&hasta=2026-12-31");

        libro!.Asientos.Should().ContainSingle();
        libro.Asientos[0].Base.Should().Be(200m);
        libro.Asientos[0].Cuota.Should().Be(42m);
        libro.TotalCuota.Should().Be(42m);
    }

    [Fact]
    public async Task Exportar_libro_iva_csv()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await EmitirFacturaAsync(cliente);

        var respuesta = await cliente.GetAsync(new Uri("/informes/libro-iva/csv?tipo=Repercutido&desde=2026-01-01&hasta=2026-12-31", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await respuesta.Content.ReadAsStringAsync();
        csv.Should().Contain("Fecha;Documento;Tercero;NIF;Base;Cuota IVA");
        csv.Should().Contain("TOTALES;;;;200,00;42,00");
    }

    [Fact]
    public async Task Resumen_trimestral_calcula_303_y_130()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await EmitirFacturaAsync(cliente); // base 200, IVA 42
        await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Material", BaseImponible = 100m, CodigoIva = "IVA21" }); // base 100, IVA 21

        var hoy = DateTime.UtcNow;
        var trimestre = ((hoy.Month - 1) / 3) + 1;

        var resumen = await cliente.GetFromJsonAsync<ResumenResp>($"/informes/resumen-trimestral?anio={hoy.Year}&trimestre={trimestre}");

        resumen!.Modelo303.IvaDevengadoCuota.Should().Be(42m);
        resumen.Modelo303.IvaDeducibleCuota.Should().Be(21m);
        resumen.Modelo303.Resultado.Should().Be(21m); // 42 - 21

        resumen.Modelo130.IngresosAcumulados.Should().Be(200m);
        resumen.Modelo130.GastosAcumulados.Should().Be(100m);
        resumen.Modelo130.RendimientoAcumulado.Should().Be(100m);
        resumen.Modelo130.PagoFraccionadoBruto.Should().Be(20m); // 20% de 100
    }

    [Fact]
    public async Task Beneficio_calcula_margen_bruto_y_neto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;

        // Artículo con precio de venta 100 y compra 60 -> margen 40 por unidad.
        var prod = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Artículo", PrecioUnitario = 100m, PrecioCompra = 60m, CodigoIva = "IVA21" }))
            .Content.ReadFromJsonAsync<ProductoResp>())!;

        var hoy = DateTime.UtcNow;
        await cliente.PostAsJsonAsync("/facturas", new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { ProductoId = prod.Id, Cantidad = 3m } }, // ingreso 300, coste 180
        });
        await cliente.PostAsJsonAsync("/gastos", new { Concepto = "Luz", BaseImponible = 50m, CodigoIva = "IVA21" });

        var b = await cliente.GetFromJsonAsync<BeneficioResp>($"/informes/beneficio?desde={hoy.Year}-01-01&hasta={hoy.Year}-12-31");

        b!.Ingresos.Should().Be(300m);
        b.Coste.Should().Be(180m);
        b.MargenBruto.Should().Be(120m);   // 300 - 180
        b.Gastos.Should().Be(50m);
        b.BeneficioNeto.Should().Be(70m);  // 120 - 50
        b.PorProducto.Should().ContainSingle(p => p.Margen == 120m);
    }
}

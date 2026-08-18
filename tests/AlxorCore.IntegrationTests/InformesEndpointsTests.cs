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
    private sealed record CierreMetodoResp(string Metodo, decimal Importe, int Numero);
    private sealed record CierreResp(decimal TotalCobrado, decimal TotalPagado, decimal Neto, List<CierreMetodoResp> CobrosPorMetodo);
    private sealed record VentaProductoResp(string Descripcion, decimal Unidades, decimal Importe, decimal Margen);
    private sealed record VentaDiaResp(int DiaSemana, string Nombre, decimal Importe, int Tickets);
    private sealed record VentasResp(int Tickets, decimal VentaTotal, decimal TicketMedio, List<VentaProductoResp> TopProductos, List<VentaDiaResp> PorDiaSemana);

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

    private sealed record Linea347Resp(string Nombre, string? Nif, string Sentido, decimal ImporteAnual);
    private sealed record Modelo347Resp(decimal Umbral, List<Linea347Resp> Clientes, List<Linea347Resp> Proveedores);
    private sealed record Modelo390Resp(decimal IvaDevengadoCuota, decimal IvaDeducibleCuota, decimal Resultado);
    private sealed record DeclaracionAnualResp(Modelo390Resp Modelo390, Modelo347Resp Modelo347);

    [Fact]
    public async Task Declaracion_anual_calcula_390_y_347()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var hoy = DateTime.UtcNow;

        // Cliente por encima del umbral 347 (4.000 base → 4.840 con IVA > 3.005,06).
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Gran Cliente SL", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Proyecto", PrecioUnitario = 4000m, CodigoIva = "IVA21" } } });
        // Cliente pequeño, por debajo del umbral (no debe aparecer en el 347).
        var pequenoId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Pequeño", NifFiscal = "B00000000" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        await cliente.PostAsJsonAsync("/facturas", new { ClienteId = pequenoId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Consulta", PrecioUnitario = 100m, CodigoIva = "IVA21" } } });
        // Gasto de un proveedor por encima del umbral.
        var proveedorId = (await (await cliente.PostAsJsonAsync("/proveedores", new { Nombre = "Gran Proveedor SL", NifFiscal = "B99999999", FormaPago = "Transferencia" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        await cliente.PostAsJsonAsync("/gastos", new { ProveedorId = proveedorId, Concepto = "Mercancía", BaseImponible = 5000m, CodigoIva = "IVA21" });

        var dec = await cliente.GetFromJsonAsync<DeclaracionAnualResp>($"/informes/declaracion-anual?anio={hoy.Year}");

        // 390: IVA devengado 4100*0.21=861; deducible 5000*0.21=1050; resultado 861-1050 = -189.
        dec!.Modelo390.IvaDevengadoCuota.Should().Be(861m);
        dec.Modelo390.IvaDeducibleCuota.Should().Be(1050m);
        dec.Modelo390.Resultado.Should().Be(-189m);

        // 347: solo el gran cliente y el gran proveedor superan el umbral.
        dec.Modelo347.Umbral.Should().Be(3005.06m);
        dec.Modelo347.Clientes.Should().ContainSingle(c => c.Nombre == "Gran Cliente SL");
        dec.Modelo347.Clientes.Should().NotContain(c => c.Nombre == "Cliente Pequeño");
        dec.Modelo347.Clientes.Single().ImporteAnual.Should().Be(4840m); // 4000 + 21% IVA
        dec.Modelo347.Proveedores.Should().ContainSingle(p => p.Nombre == "Gran Proveedor SL" && p.Nif == "B99999999");
        dec.Modelo347.Proveedores.Single().ImporteAnual.Should().Be(6050m); // 5000 + 21% IVA
    }

    [Fact]
    public async Task Cierre_de_caja_agrupa_los_cobros_del_dia_por_metodo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var factura = await EmitirFacturaAsync(cliente); // total 242
        await cliente.PostAsJsonAsync("/cobros", new { FacturaId = factura.Id, Importe = 242m, Metodo = "Efectivo" });

        var hoy = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cierre = await cliente.GetFromJsonAsync<CierreResp>($"/informes/cierre-caja?dia={hoy}");

        cierre!.TotalCobrado.Should().Be(242m);
        cierre.Neto.Should().Be(242m);
        cierre.CobrosPorMetodo.Should().ContainSingle(m => m.Metodo == "Efectivo" && m.Importe == 242m && m.Numero == 1);
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

    [Fact]
    public async Task Informe_de_ventas_da_tickets_medio_top_por_unidades_y_por_dia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Contado", NifFiscal = "B12345674" })).Content.ReadFromJsonAsync<ClienteResp>())!.Id;
        var cana = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Caña", PrecioUnitario = 1.50m, PrecioCompra = 0.50m, CodigoIva = "IVA10" })).Content.ReadFromJsonAsync<ProductoResp>())!;
        var tapa = (await (await cliente.PostAsJsonAsync("/productos", new { Nombre = "Tapa", PrecioUnitario = 4.00m, PrecioCompra = 1.50m, CodigoIva = "IVA10" })).Content.ReadFromJsonAsync<ProductoResp>())!;

        // Dos tickets: uno con 10 cañas y otro con 2 cañas + 1 tapa.
        await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new[] { new { ProductoId = cana.Id, Cantidad = 10m } } });
        await cliente.PostAsJsonAsync("/facturas", new { ClienteId = clienteId, Lineas = new object[] { new { ProductoId = cana.Id, Cantidad = 2m }, new { ProductoId = tapa.Id, Cantidad = 1m } } });

        var hoy = DateTime.UtcNow;
        var v = await cliente.GetFromJsonAsync<VentasResp>($"/informes/ventas?desde={hoy.Year}-01-01&hasta={hoy.Year}-12-31");

        v!.Tickets.Should().Be(2);
        v.VentaTotal.Should().Be(24.20m);             // ticket1 16,50 (15,00+10%) + ticket2 7,70 (7,00+10%)
        v.TicketMedio.Should().Be(12.10m);            // 24,20 / 2
        v.TopProductos[0].Descripcion.Should().Be("Caña"); // 12 uds, por encima de la tapa (1)
        v.TopProductos[0].Unidades.Should().Be(12m);
        v.PorDiaSemana.Should().HaveCount(7);
        v.PorDiaSemana.Sum(d => d.Tickets).Should().Be(2);
        v.PorDiaSemana.Sum(d => d.Importe).Should().Be(24.20m);
    }
}

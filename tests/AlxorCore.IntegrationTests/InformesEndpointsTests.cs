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
}

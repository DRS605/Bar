using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la remesa de adeudos SEPA (Norma 19 / pain.008) y sus datos previos.</summary>
public sealed class RemesaSepaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public RemesaSepaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id, string? Iban, string? MandatoReferencia);
    private sealed record FacturaResp(Guid Id, decimal Total);
    private sealed record EmpresaResp(string? Iban, string? IdentificadorAcreedor);
    private sealed record RemesaResp(string FicheroXml, string NombreArchivo, int NumeroAdeudos, decimal Total, List<string> Omitidas);

    private static async Task<Guid> CrearFacturaAsync(HttpClient cliente, Guid clienteId, decimal precio)
    {
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Cuota", PrecioUnitario = precio, CodigoIva = "IVA0" } } };
        return (await (await cliente.PostAsJsonAsync("/facturas", comando)).Content.ReadFromJsonAsync<FacturaResp>())!.Id;
    }

    [Fact]
    public async Task Configurar_datos_de_cobro_de_la_empresa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var resp = await cliente.PutAsJsonAsync("/empresas/actual/cobro", new { Iban = "ES9121000418450200051332", IdentificadorAcreedor = "ES12345M1234567890" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var empresa = await resp.Content.ReadFromJsonAsync<EmpresaResp>();
        empresa!.Iban.Should().Be("ES9121000418450200051332");
        empresa.IdentificadorAcreedor.Should().Be("ES12345M1234567890");
    }

    [Fact]
    public async Task Generar_remesa_produce_pain008_con_los_adeudos_domiciliables()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PutAsJsonAsync("/empresas/actual/cobro", new { Iban = "ES9121000418450200051332", IdentificadorAcreedor = "ES12345M1234567890" });

        // Cliente con domiciliación completa.
        var conMandato = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Domiciliado SL", NifFiscal = "B12345674", Iban = "ES7620770024003102575766", MandatoReferencia = "MND-001", MandatoFecha = "2025-01-10" })).Content.ReadFromJsonAsync<ClienteResp>())!;
        conMandato.Iban.Should().Be("ES7620770024003102575766");
        // Cliente sin datos de domiciliación.
        var sinMandato = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Sin Mandato" })).Content.ReadFromJsonAsync<ClienteResp>())!;

        var facturaOk = await CrearFacturaAsync(cliente, conMandato.Id, 150m);
        var facturaSin = await CrearFacturaAsync(cliente, sinMandato.Id, 90m);

        var resp = await cliente.PostAsJsonAsync("/tesoreria/remesa", new { FacturaIds = new[] { facturaOk, facturaSin } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var remesa = await resp.Content.ReadFromJsonAsync<RemesaResp>();

        remesa!.NumeroAdeudos.Should().Be(1);
        remesa.Total.Should().Be(150m);
        remesa.Omitidas.Should().ContainSingle(o => o.Contains("mandato", StringComparison.OrdinalIgnoreCase));
        remesa.FicheroXml.Should().Contain("pain.008.001.02");
        remesa.FicheroXml.Should().Contain("ES7620770024003102575766");   // IBAN del deudor
        remesa.FicheroXml.Should().Contain("MND-001");                     // referencia del mandato
        remesa.FicheroXml.Should().Contain("150.00");                      // importe
        remesa.NombreArchivo.Should().EndWith(".xml");
    }

    [Fact]
    public async Task Remesa_sin_datos_de_cobro_de_la_empresa_devuelve_400()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var conMandato = (await (await cliente.PostAsJsonAsync("/clientes", new { Nombre = "X", Iban = "ES7620770024003102575766", MandatoReferencia = "M1", MandatoFecha = "2025-01-10" })).Content.ReadFromJsonAsync<ClienteResp>())!;
        var factura = await CrearFacturaAsync(cliente, conMandato.Id, 100m);

        var resp = await cliente.PostAsJsonAsync("/tesoreria/remesa", new { FacturaIds = new[] { factura } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

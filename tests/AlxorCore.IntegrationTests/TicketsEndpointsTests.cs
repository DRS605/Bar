using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del TPV: emisión de tickets (facturas simplificadas).</summary>
public sealed class TicketsEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public TicketsEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record FacturaResp(Guid Id, string NumeroCompleto, string ClienteNombre, decimal Total, string Tipo);

    private static object Ticket(object lineas, Guid? clienteId = null, string? serie = null) => new
    {
        ClienteId = clienteId,
        Serie = serie,
        Lineas = lineas,
    };

    [Fact]
    public async Task Emite_un_ticket_sin_cliente_en_la_serie_T()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var anio = DateTime.UtcNow.Year;

        var resp = await cliente.PostAsJsonAsync("/tickets", Ticket(
            new[] { new { Cantidad = 2m, Descripcion = "Café", PrecioUnitario = 1.50m, CodigoIva = "IVA10" } }));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var ticket = await resp.Content.ReadFromJsonAsync<FacturaResp>();
        ticket!.NumeroCompleto.Should().Be($"T{anio}/000001");
        ticket.Tipo.Should().Be("Simplificada");
        ticket.ClienteNombre.Should().Be("Cliente de contado");
        ticket.Total.Should().Be(3.30m); // 3,00 + 10% IVA
    }

    [Fact]
    public async Task El_ticket_aparece_en_el_listado_de_facturas()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await cliente.PostAsJsonAsync("/tickets", Ticket(
            new[] { new { Cantidad = 1m, Descripcion = "Menú", PrecioUnitario = 12m, CodigoIva = "IVA10" } }));

        var facturas = await cliente.GetFromJsonAsync<List<FacturaResp>>("/facturas");
        facturas.Should().ContainSingle(f => f.Tipo == "Simplificada");
    }

    [Fact]
    public async Task Un_ticket_por_encima_del_tope_se_rechaza()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var resp = await cliente.PostAsJsonAsync("/tickets", Ticket(
            new[] { new { Cantidad = 1m, Descripcion = "Equipo caro", PrecioUnitario = 5000m, CodigoIva = "IVA21" } }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_ticket_genera_un_pdf()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var ticket = await (await cliente.PostAsJsonAsync("/tickets", Ticket(
            new[] { new { Cantidad = 1m, Descripcion = "Refresco", PrecioUnitario = 2m, CodigoIva = "IVA10" } })))
            .Content.ReadFromJsonAsync<FacturaResp>();

        var pdf = await cliente.GetAsync(new Uri($"/facturas/{ticket!.Id}/pdf", UriKind.Relative));
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task El_ticket_se_puede_cobrar_y_queda_liquidado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var ticket = await (await cliente.PostAsJsonAsync("/tickets", Ticket(
            new[] { new { Cantidad = 2m, Descripcion = "Café", PrecioUnitario = 1.50m, CodigoIva = "IVA10" } })))
            .Content.ReadFromJsonAsync<FacturaResp>();

        var cobro = await cliente.PostAsJsonAsync("/cobros", new { FacturaId = ticket!.Id, Importe = ticket.Total, Metodo = "Efectivo" });
        cobro.StatusCode.Should().Be(HttpStatusCode.OK);

        var saldo = await cliente.GetFromJsonAsync<SaldoResp>($"/facturas/{ticket.Id}/saldo");
        saldo!.Estado.Should().Be("Liquidado");
        saldo.Pendiente.Should().Be(0m);
    }

    private sealed record SaldoResp(string Estado, decimal Liquidado, decimal Pendiente);
}

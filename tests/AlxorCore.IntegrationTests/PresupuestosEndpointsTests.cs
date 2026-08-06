using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de presupuestos: creación, edición, conversión a factura y aislamiento.</summary>
public sealed class PresupuestosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public PresupuestosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record LineaPresupuestoResp(string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal Base, decimal CuotaIva);
    private sealed record PresupuestoResp(Guid Id, string NumeroCompleto, Guid ClienteId, string ClienteNombre, string Estado, decimal BaseImponible, decimal CuotaIva, decimal Total, Guid? FacturaId, List<LineaPresupuestoResp> Lineas);
    private sealed record PresupuestoResumen(Guid Id, string NumeroCompleto, string ClienteNombre, decimal Total, string Estado, Guid? FacturaId);
    private sealed record FacturaResp(Guid Id, string NumeroCompleto, decimal BaseImponible, decimal CuotaIva, decimal Total, string Estado);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente)
    {
        var crear = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente Presupuesto SL", NifFiscal = "B12345674" });
        var dto = await crear.Content.ReadFromJsonAsync<ClienteResp>();
        return dto!.Id;
    }

    [Fact]
    public async Task Crear_presupuesto_calcula_totales_y_asigna_numero()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        var comando = new
        {
            ClienteId = clienteId,
            Lineas = new[] { new { Cantidad = 2m, Descripcion = "Instalación climatización", PrecioUnitario = 100m, CodigoIva = "IVA21" } },
        };

        var crear = await cliente.PostAsJsonAsync("/presupuestos", comando);
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var presupuesto = await crear.Content.ReadFromJsonAsync<PresupuestoResp>();

        presupuesto!.BaseImponible.Should().Be(200m);
        presupuesto.CuotaIva.Should().Be(42m);
        presupuesto.Total.Should().Be(242m);
        presupuesto.Estado.Should().Be("Borrador");
        presupuesto.FacturaId.Should().BeNull();
        presupuesto.NumeroCompleto.Should().StartWith("P");
        presupuesto.NumeroCompleto.Should().EndWith("/000001");
        presupuesto.Lineas.Should().ContainSingle();
    }

    [Fact]
    public async Task Aceptar_presupuesto_lo_convierte_en_factura()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Equipo A/A", PrecioUnitario = 500m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        var aceptar = await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto!.Id}/aceptar", new { });
        aceptar.StatusCode.Should().Be(HttpStatusCode.Created);
        var factura = await aceptar.Content.ReadFromJsonAsync<FacturaResp>();

        factura!.BaseImponible.Should().Be(500m);
        factura.CuotaIva.Should().Be(105m);
        factura.Total.Should().Be(605m);
        factura.NumeroCompleto.Should().EndWith("/000001");

        // El presupuesto queda aceptado y enlazado a la factura.
        var trasAceptar = await cliente.GetFromJsonAsync<PresupuestoResp>($"/presupuestos/{presupuesto.Id}");
        trasAceptar!.Estado.Should().Be("Aceptado");
        trasAceptar.FacturaId.Should().Be(factura.Id);
    }

    [Fact]
    public async Task No_se_puede_aceptar_dos_veces_un_presupuesto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        (await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto!.Id}/aceptar", new { })).StatusCode.Should().Be(HttpStatusCode.Created);

        var segundaVez = await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto.Id}/aceptar", new { });
        segundaVez.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rechazar_presupuesto_cambia_su_estado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        (await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto!.Id}/rechazar", new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        var tras = await cliente.GetFromJsonAsync<PresupuestoResp>($"/presupuestos/{presupuesto.Id}");
        tras!.Estado.Should().Be("Rechazado");

        // Un presupuesto rechazado ya no se puede aceptar.
        (await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto.Id}/aceptar", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Actualizar_presupuesto_en_borrador_recalcula_totales()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Original", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        var actualizar = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 3m, Descripcion = "Revisado", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        var resp = await cliente.PutAsJsonAsync($"/presupuestos/{presupuesto!.Id}", actualizar);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizado = await resp.Content.ReadFromJsonAsync<PresupuestoResp>();

        actualizado!.BaseImponible.Should().Be(300m);
        actualizado.Total.Should().Be(363m);
        actualizado.Lineas.Should().ContainSingle(l => l.Descripcion == "Revisado");
    }

    [Fact]
    public async Task Descargar_pdf_de_presupuesto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Oferta", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        var pdf = await cliente.GetAsync($"/presupuestos/{presupuesto!.Id}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
        // Un PDF válido empieza por "%PDF".
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Enviar_presupuesto_por_email()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);
        var comando = new { ClienteId = clienteId, Lineas = new[] { new { Cantidad = 1m, Descripcion = "Oferta", PrecioUnitario = 100m, CodigoIva = "IVA21" } } };
        var presupuesto = await (await cliente.PostAsJsonAsync("/presupuestos", comando)).Content.ReadFromJsonAsync<PresupuestoResp>();

        var envio = await cliente.PostAsJsonAsync($"/presupuestos/{presupuesto!.Id}/enviar", new { Email = "cliente@ejemplo.es" });
        envio.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Los_presupuestos_estan_aislados_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(empresaA);
        await empresaA.PostAsJsonAsync("/presupuestos", new { ClienteId = clienteA, Lineas = new[] { new { Cantidad = 1m, Descripcion = "X", PrecioUnitario = 10m, CodigoIva = "IVA21" } } });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await empresaB.GetFromJsonAsync<List<PresupuestoResumen>>("/presupuestos");

        listaB.Should().BeEmpty();
    }
}

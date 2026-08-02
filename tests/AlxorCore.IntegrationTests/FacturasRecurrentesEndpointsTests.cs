using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la facturación automática periódica.</summary>
public sealed class FacturasRecurrentesEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public FacturasRecurrentesEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ClienteResp(Guid Id);
    private sealed record RecurrenteResp(Guid Id, string Nombre, string Periodicidad, bool Activa, decimal Total);
    private sealed record FacturaResumen(Guid Id, string NumeroCompleto, decimal Total);
    private sealed record ProcesoResp(int Emitidas, IReadOnlyList<Guid> FacturasCreadas);

    private static async Task<Guid> CrearClienteAsync(HttpClient cliente)
    {
        var resp = await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cafetería La Central", NifFiscal = "12345678Z" });
        var creado = await resp.Content.ReadFromJsonAsync<ClienteResp>();
        return creado!.Id;
    }

    private static object DatosRecurrente(Guid clienteId, string primeraEmision, string periodicidad = "Mensual") => new
    {
        Nombre = "Cuota mantenimiento web",
        ClienteId = clienteId,
        Periodicidad = periodicidad,
        PrimeraEmision = primeraEmision,
        Lineas = new[] { new { Cantidad = 1m, Descripcion = "Mantenimiento web mensual", PrecioUnitario = 90m, CodigoIva = "IVA21" } },
    };

    [Fact]
    public async Task Crear_y_listar_factura_recurrente()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        var crear = await cliente.PostAsJsonAsync("/facturas-recurrentes", DatosRecurrente(clienteId, "2026-09-01"));
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var creada = await crear.Content.ReadFromJsonAsync<RecurrenteResp>();
        creada!.Periodicidad.Should().Be("Mensual");
        creada.Total.Should().Be(108.90m); // 90 + 21% IVA

        var lista = await cliente.GetFromJsonAsync<List<RecurrenteResp>>("/facturas-recurrentes");
        lista.Should().ContainSingle(r => r.Id == creada.Id);
    }

    [Fact]
    public async Task Procesar_emite_una_factura_de_la_recurrencia_vencida_y_avanza_la_fecha()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        // Primera emisión en el pasado → está vencida.
        await cliente.PostAsJsonAsync("/facturas-recurrentes", DatosRecurrente(clienteId, "2026-01-01"));

        var proceso = await cliente.PostAsync(new Uri("/facturas-recurrentes/procesar", UriKind.Relative), content: null);
        proceso.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await proceso.Content.ReadFromJsonAsync<ProcesoResp>();
        resultado!.Emitidas.Should().Be(1);

        var facturas = await cliente.GetFromJsonAsync<List<FacturaResumen>>("/facturas");
        facturas.Should().ContainSingle(f => f.Total == 108.90m);

        // Al procesar de nuevo el mismo día ya no hay nada vencido (la fecha avanzó un mes).
        var segundo = await cliente.PostAsync(new Uri("/facturas-recurrentes/procesar", UriKind.Relative), content: null);
        var resultado2 = await segundo.Content.ReadFromJsonAsync<ProcesoResp>();
        resultado2!.Emitidas.Should().Be(0);
    }

    [Fact]
    public async Task Una_recurrencia_pausada_no_emite()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteId = await CrearClienteAsync(cliente);

        var crear = await cliente.PostAsJsonAsync("/facturas-recurrentes", DatosRecurrente(clienteId, "2026-01-01"));
        var creada = await crear.Content.ReadFromJsonAsync<RecurrenteResp>();

        await cliente.PostAsJsonAsync($"/facturas-recurrentes/{creada!.Id}/estado", new { Activa = false });

        var proceso = await cliente.PostAsync(new Uri("/facturas-recurrentes/procesar", UriKind.Relative), content: null);
        var resultado = await proceso.Content.ReadFromJsonAsync<ProcesoResp>();
        resultado!.Emitidas.Should().Be(0);
    }

    [Fact]
    public async Task Las_recurrencias_estan_aisladas_por_empresa()
    {
        var (a, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var clienteA = await CrearClienteAsync(a);
        await a.PostAsJsonAsync("/facturas-recurrentes", DatosRecurrente(clienteA, "2026-09-01"));

        var (b, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var listaB = await b.GetFromJsonAsync<List<RecurrenteResp>>("/facturas-recurrentes");
        listaB.Should().BeEmpty();
    }
}

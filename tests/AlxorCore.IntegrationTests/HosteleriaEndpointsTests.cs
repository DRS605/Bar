using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración del módulo Hostelería: mesas, comandas y cobro (ticket + stock).</summary>
public sealed class HosteleriaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public HosteleriaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id, string Nombre, decimal PrecioUnitario, decimal Stock, bool ControlarStock);

    private sealed record MesaResp(Guid Id, string Nombre, string? Zona, int Capacidad, string Forma, double PosX, double PosY, bool Activa, bool Ocupada, Guid? ComandaAbiertaId, decimal TotalComandaAbierta);

    private sealed record LineaResp(Guid Id, Guid ProductoId, string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal Total);

    private sealed record ComandaResp(Guid Id, Guid MesaId, string Estado, decimal BaseImponible, decimal CuotaIva, decimal Total, string? MetodoCobro, Guid? FacturaId, string? NumeroTicket, List<LineaResp> Lineas);

    private sealed record ComandaResumenResp(Guid Id, Guid MesaId, string MesaNombre, string Estado, int NumeroLineas, decimal Total);

    private sealed record FacturaResp(Guid Id, string NumeroCompleto, decimal Total, string Tipo);

    private sealed record CierreMetodoResp(string Metodo, decimal Importe, int Numero);
    private sealed record CierreResp(decimal TotalCobrado, List<CierreMetodoResp> CobrosPorMetodo);

    private sealed record ArticuloCocinaResp(decimal Cantidad, string Descripcion);
    private sealed record CocinaResp(Guid MesaId, List<ArticuloCocinaResp> Articulos);

    private static async Task<ProductoResp> CrearCañaAsync(HttpClient cliente, decimal precio = 1.50m, bool stock = true, decimal stockInicial = 100m)
    {
        var resp = await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = "Caña",
            PrecioUnitario = precio,
            Tipo = "Bien",
            CodigoIva = "IVA10",
            Unidad = "ud",
            ControlarStock = stock,
            StockInicial = stockInicial,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<ProductoResp>())!;
    }

    private static async Task<MesaResp> CrearMesaAsync(HttpClient cliente, string nombre = "Mesa 1")
    {
        var resp = await cliente.PostAsJsonAsync("/mesas", new { Nombre = nombre, Zona = "Salón", Capacidad = 4 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<MesaResp>())!;
    }

    [Fact]
    public async Task Crea_mesa_y_aparece_libre_en_el_listado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = await CrearMesaAsync(cliente);

        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesas.Should().ContainSingle(m => m.Id == mesa.Id);
        mesas!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeFalse();
    }

    [Fact]
    public async Task Flujo_completo_abrir_pedir_cobrar_libera_mesa_y_descuenta_stock()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente);
        var mesa = await CrearMesaAsync(cliente);
        var anio = DateTime.UtcNow.Year;

        // Abrir comanda
        var abrir = await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id, Notas = "sin hielo" });
        abrir.StatusCode.Should().Be(HttpStatusCode.Created);
        var comanda = (await abrir.Content.ReadFromJsonAsync<ComandaResp>())!;
        comanda.Estado.Should().Be("Abierta");

        // Añadir 3 cañas
        var conLinea = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 3m });
        conLinea.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizada = (await conLinea.Content.ReadFromJsonAsync<ComandaResp>())!;
        actualizada.Lineas.Should().ContainSingle();
        actualizada.BaseImponible.Should().Be(4.50m);
        actualizada.CuotaIva.Should().Be(0.45m);
        actualizada.Total.Should().Be(4.95m);

        // La mesa figura ocupada con el total de la comanda
        var mesasOcupadas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        var mesaOcupada = mesasOcupadas!.Single(m => m.Id == mesa.Id);
        mesaOcupada.Ocupada.Should().BeTrue();
        mesaOcupada.ComandaAbiertaId.Should().Be(comanda.Id);
        mesaOcupada.TotalComandaAbierta.Should().Be(4.95m);

        // Cobrar
        var cobrar = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar", new { Metodo = "Tarjeta" });
        cobrar.StatusCode.Should().Be(HttpStatusCode.OK);
        var cobrada = (await cobrar.Content.ReadFromJsonAsync<ComandaResp>())!;
        cobrada.Estado.Should().Be("Cobrada");
        cobrada.MetodoCobro.Should().Be("Tarjeta");
        cobrada.NumeroTicket.Should().Be($"T{anio}/000001");
        cobrada.FacturaId.Should().NotBeNull();

        // La mesa vuelve a estar libre
        var mesasLibres = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesasLibres!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeFalse();

        // Se generó un ticket (factura simplificada)
        var facturas = await cliente.GetFromJsonAsync<List<FacturaResp>>("/facturas");
        facturas.Should().Contain(f => f.Id == cobrada.FacturaId && f.Tipo == "Simplificada" && f.Total == 4.95m);

        // El stock se descontó (100 - 3)
        var tras = await cliente.GetFromJsonAsync<ProductoResp>($"/productos/{producto.Id}");
        tras!.Stock.Should().Be(97m);
    }

    [Fact]
    public async Task Crea_barra_con_forma_y_recoloca_en_el_plano()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var crear = await cliente.PostAsJsonAsync("/mesas", new { Nombre = "Barra", Zona = "Barra", Capacidad = 8, Forma = "Rectangular", PosX = 120.0, PosY = 300.0 });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var barra = (await crear.Content.ReadFromJsonAsync<MesaResp>())!;
        barra.Forma.Should().Be("Rectangular");
        barra.PosX.Should().Be(120.0);
        barra.PosY.Should().Be(300.0);

        var mover = await cliente.PutAsJsonAsync($"/mesas/{barra.Id}/posicion", new { PosX = 640.5, PosY = 210.0 });
        mover.StatusCode.Should().Be(HttpStatusCode.OK);
        var movida = (await mover.Content.ReadFromJsonAsync<MesaResp>())!;
        movida.PosX.Should().Be(640.5);
        movida.PosY.Should().Be(210.0);

        // La nueva posición persiste en el listado.
        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        var guardada = mesas!.Single(m => m.Id == barra.Id);
        guardada.PosX.Should().Be(640.5);
        guardada.Forma.Should().Be("Rectangular");
    }

    [Fact]
    public async Task No_se_puede_abrir_una_segunda_comanda_en_la_misma_mesa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = await CrearMesaAsync(cliente);

        await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id });
        var segunda = await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id });

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task La_comanda_abierta_aparece_en_el_listado_de_abiertas()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente, "Barra");
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 2m });

        var abiertas = await cliente.GetFromJsonAsync<List<ComandaResumenResp>>("/comandas");
        var resumen = abiertas!.Single(c => c.Id == comanda.Id);
        resumen.MesaNombre.Should().Be("Barra");
        resumen.NumeroLineas.Should().Be(1);
        resumen.Total.Should().Be(3.30m);
    }

    [Fact]
    public async Task Quitar_linea_recalcula_el_total()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 2m })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var lineaId = conLinea.Lineas.Single().Id;

        var quitar = await cliente.DeleteAsync(new Uri($"/comandas/{comanda.Id}/lineas/{lineaId}", UriKind.Relative));
        quitar.StatusCode.Should().Be(HttpStatusCode.OK);
        var vacia = (await quitar.Content.ReadFromJsonAsync<ComandaResp>())!;
        vacia.Lineas.Should().BeEmpty();
        vacia.Total.Should().Be(0m);
    }

    [Fact]
    public async Task Pedir_el_mismo_producto_dos_veces_acumula_en_una_sola_linea()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;

        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 2m });
        var segunda = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 1m })).Content.ReadFromJsonAsync<ComandaResp>())!;

        segunda.Lineas.Should().ContainSingle();
        segunda.Lineas.Single().Cantidad.Should().Be(3m);
        segunda.Total.Should().Be(4.95m);
    }

    [Fact]
    public async Task Fijar_la_cantidad_de_una_linea_recalcula_el_total()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 1m })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var lineaId = conLinea.Lineas.Single().Id;

        var fijar = await cliente.PutAsJsonAsync($"/comandas/{comanda.Id}/lineas/{lineaId}", new { Cantidad = 5m });
        fijar.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualizada = (await fijar.Content.ReadFromJsonAsync<ComandaResp>())!;
        actualizada.Lineas.Single().Cantidad.Should().Be(5m);
        actualizada.Total.Should().Be(8.25m);

        // Cantidad cero se rechaza (para eliminar se usa el borrado de la línea).
        var cero = await cliente.PutAsJsonAsync($"/comandas/{comanda.Id}/lineas/{lineaId}", new { Cantidad = 0m });
        cero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cobrar_una_comanda_registra_el_cobro_en_el_cierre_de_caja()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false); // 1,50 € IVA10
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 2m }); // total 3,30 €

        var cobrar = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar", new { Metodo = "Tarjeta" });
        cobrar.StatusCode.Should().Be(HttpStatusCode.OK);

        // La venta del bar aparece en el cierre de caja del día, por su forma de pago.
        var hoy = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cierre = await cliente.GetFromJsonAsync<CierreResp>($"/informes/cierre-caja?dia={hoy}");
        cierre!.CobrosPorMetodo.Should().ContainSingle(m => m.Metodo == "Tarjeta" && m.Importe == 3.30m && m.Numero == 1);
        cierre.TotalCobrado.Should().Be(3.30m);
    }

    [Fact]
    public async Task Enviar_a_cocina_manda_los_articulos_nuevos_y_no_los_repite()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 3m });

        var envio1 = await (await cliente.PostAsync(new Uri($"/comandas/{comanda.Id}/cocina", UriKind.Relative), content: null)).Content.ReadFromJsonAsync<CocinaResp>();
        envio1!.Articulos.Should().ContainSingle(a => a.Descripcion == "Caña" && a.Cantidad == 3m);

        // Sin cambios, un segundo envío no manda nada de nuevo a cocina.
        var envio2 = await (await cliente.PostAsync(new Uri($"/comandas/{comanda.Id}/cocina", UriKind.Relative), content: null)).Content.ReadFromJsonAsync<CocinaResp>();
        envio2!.Articulos.Should().BeEmpty();
    }

    [Fact]
    public async Task No_se_puede_cobrar_una_comanda_vacia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;

        var cobrar = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar", new { Metodo = "Efectivo" });
        cobrar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anular_una_comanda_la_cierra_y_libera_la_mesa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = producto.Id, Cantidad = 1m });

        var anular = await cliente.PostAsync(new Uri($"/comandas/{comanda.Id}/anular", UriKind.Relative), content: null);
        anular.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesas!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_empresa_seleccionada_no_se_pueden_listar_mesas()
    {
        var cliente = await Ayudas.AutenticadoAsync(_fabrica);
        var resp = await cliente.GetAsync(new Uri("/mesas", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

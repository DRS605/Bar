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

    private sealed record LineaResp(Guid Id, Guid ProductoId, string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal Total, decimal CantidadCobrada, decimal CantidadPendienteCobro);

    private sealed record ComandaResp(Guid Id, Guid MesaId, string Estado, decimal BaseImponible, decimal CuotaIva, decimal Total, string? MetodoCobro, Guid? FacturaId, string? NumeroTicket, bool TieneCobroParcial, decimal TotalPendienteCobro, List<LineaResp> Lineas);

    private sealed record CobroParcialResp(Guid FacturaId, string NumeroTicket, decimal Total, bool Cerrada, ComandaResp Comanda);

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
    public async Task Reparto_por_articulos_emite_un_ticket_por_pago_y_cierra_al_pagar_lo_ultimo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false); // 1,50 € IVA10
        var tapaResp = await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = "Tapa", PrecioUnitario = 4.00m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false,
        });
        var tapa = (await tapaResp.Content.ReadFromJsonAsync<ProductoResp>())!;
        var mesa = await CrearMesaAsync(cliente);
        var anio = DateTime.UtcNow.Year;

        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 2m });
        var conTapa = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = tapa.Id, Cantidad = 1m })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var lineaCana = conTapa.Lineas.Single(l => l.ProductoId == cana.Id);
        var lineaTapa = conTapa.Lineas.Single(l => l.ProductoId == tapa.Id);

        // Primer comensal paga sus 2 cañas (3,30 €): se emite un ticket y la mesa sigue abierta con lo que falta.
        var pago1 = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar-parcial",
            new { Items = new[] { new { LineaId = lineaCana.Id, Cantidad = 2m } }, Metodo = "Efectivo" });
        pago1.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1 = (await pago1.Content.ReadFromJsonAsync<CobroParcialResp>())!;
        r1.Total.Should().Be(3.30m);
        r1.NumeroTicket.Should().Be($"T{anio}/000001");
        r1.Cerrada.Should().BeFalse();
        r1.Comanda.Estado.Should().Be("Abierta");
        r1.Comanda.TieneCobroParcial.Should().BeTrue();
        r1.Comanda.TotalPendienteCobro.Should().Be(4.40m); // la tapa: 4,00 + 10%
        r1.Comanda.Lineas.Single(l => l.ProductoId == cana.Id).CantidadPendienteCobro.Should().Be(0m);

        // La mesa sigue ocupada mientras quede algo por cobrar.
        var mesasMedias = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesasMedias!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeTrue();

        // Segundo comensal paga la tapa (4,40 €): el último pago cierra la comanda y libera la mesa.
        var pago2 = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar-parcial",
            new { Items = new[] { new { LineaId = lineaTapa.Id, Cantidad = 1m } }, Metodo = "Tarjeta" });
        pago2.StatusCode.Should().Be(HttpStatusCode.OK);
        var r2 = (await pago2.Content.ReadFromJsonAsync<CobroParcialResp>())!;
        r2.Total.Should().Be(4.40m);
        r2.NumeroTicket.Should().Be($"T{anio}/000002");
        r2.Cerrada.Should().BeTrue();
        r2.Comanda.Estado.Should().Be("Cobrada");

        var mesasLibres = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesasLibres!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeFalse();

        // Dos tickets (uno por comensal) y ambos cobros en el cierre de caja del día.
        var facturas = await cliente.GetFromJsonAsync<List<FacturaResp>>("/facturas");
        facturas!.Count(f => f.Tipo == "Simplificada").Should().BeGreaterThanOrEqualTo(2);
        var hoy = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cierre = await cliente.GetFromJsonAsync<CierreResp>($"/informes/cierre-caja?dia={hoy}");
        cierre!.TotalCobrado.Should().Be(7.70m);
        cierre.CobrosPorMetodo.Should().Contain(m => m.Metodo == "Efectivo" && m.Importe == 3.30m);
        cierre.CobrosPorMetodo.Should().Contain(m => m.Metodo == "Tarjeta" && m.Importe == 4.40m);
    }

    [Fact]
    public async Task Reparto_por_encima_de_lo_pendiente_devuelve_conflicto()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var conLinea = (await (await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 2m })).Content.ReadFromJsonAsync<ComandaResp>())!;
        var linea = conLinea.Lineas.Single();

        var exceso = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/cobrar-parcial",
            new { Items = new[] { new { LineaId = linea.Id, Cantidad = 3m } }, Metodo = "Efectivo" });
        exceso.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mover_una_comanda_libera_la_mesa_origen_y_ocupa_la_destino()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false);
        var mesa1 = await CrearMesaAsync(cliente, "Mesa 1");
        var mesa2 = await CrearMesaAsync(cliente, "Terraza 1");
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa1.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 2m });

        var mover = await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/mover", new { MesaId = mesa2.Id });
        mover.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mover.Content.ReadFromJsonAsync<ComandaResp>())!.MesaId.Should().Be(mesa2.Id);

        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesas!.Single(m => m.Id == mesa1.Id).Ocupada.Should().BeFalse();
        var destino = mesas!.Single(m => m.Id == mesa2.Id);
        destino.Ocupada.Should().BeTrue();
        destino.ComandaAbiertaId.Should().Be(comanda.Id);
    }

    [Fact]
    public async Task No_se_puede_mover_a_una_mesa_ocupada()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false);
        var mesa1 = await CrearMesaAsync(cliente, "Mesa 1");
        var mesa2 = await CrearMesaAsync(cliente, "Mesa 2");
        var comanda1 = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa1.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda1.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 1m });
        await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa2.Id }); // mesa2 ocupada

        var mover = await cliente.PostAsJsonAsync($"/comandas/{comanda1.Id}/mover", new { MesaId = mesa2.Id });
        mover.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Juntar_dos_comandas_funde_las_cuentas_y_libera_la_mesa_de_origen()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false); // 1,50 € IVA10
        var tapaResp = await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = "Tapa", PrecioUnitario = 4.00m, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false,
        });
        var tapa = (await tapaResp.Content.ReadFromJsonAsync<ProductoResp>())!;
        var mesa1 = await CrearMesaAsync(cliente, "Mesa 1");
        var mesa2 = await CrearMesaAsync(cliente, "Mesa 2");

        var destino = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa1.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{destino.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 2m });
        var origen = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa2.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{origen.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 1m });
        await cliente.PostAsJsonAsync($"/comandas/{origen.Id}/lineas", new { ProductoId = tapa.Id, Cantidad = 1m });

        var juntar = await cliente.PostAsJsonAsync($"/comandas/{destino.Id}/juntar", new { OrigenId = origen.Id });
        juntar.StatusCode.Should().Be(HttpStatusCode.OK);
        var fundida = (await juntar.Content.ReadFromJsonAsync<ComandaResp>())!;
        // 3 cañas (2+1 acumuladas) + 1 tapa = 8,50 + 10% IVA = 9,35 €
        fundida.Lineas.Single(l => l.ProductoId == cana.Id).Cantidad.Should().Be(3m);
        fundida.Total.Should().Be(9.35m);

        // La mesa de origen queda libre; solo queda una comanda abierta.
        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesas!.Single(m => m.Id == mesa2.Id).Ocupada.Should().BeFalse();
        mesas!.Single(m => m.Id == mesa1.Id).Ocupada.Should().BeTrue();
        var abiertas = await cliente.GetFromJsonAsync<List<ComandaResumenResp>>("/comandas");
        abiertas!.Count(c => c.Id == origen.Id).Should().Be(0);
        abiertas!.Should().ContainSingle(c => c.Id == destino.Id);
    }

    [Fact]
    public async Task La_cuenta_previa_se_descarga_en_escpos_y_no_es_fiscal()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 2m });

        var resp = await cliente.GetAsync(new Uri($"/comandas/{comanda.Id}/cuenta.escpos", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Take(2).Should().Equal(new byte[] { 0x1B, 0x40 });                 // ESC @ (ESC/POS)
        System.Text.Encoding.ASCII.GetString(bytes).Should().Contain("CUENTA");

        // Es una vista previa: no emite factura ni cierra la comanda.
        (await cliente.GetFromJsonAsync<List<FacturaResp>>("/facturas"))!.Should().BeEmpty();
        (await cliente.GetFromJsonAsync<ComandaResp>($"/comandas/{comanda.Id}"))!.Estado.Should().Be("Abierta");
    }

    [Fact]
    public async Task Imprimir_la_cuenta_sin_impresora_configurada_avisa()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var cana = await CrearCañaAsync(cliente, stock: false);
        var mesa = await CrearMesaAsync(cliente);
        var comanda = (await (await cliente.PostAsJsonAsync("/comandas", new { MesaId = mesa.Id })).Content.ReadFromJsonAsync<ComandaResp>())!;
        await cliente.PostAsJsonAsync($"/comandas/{comanda.Id}/lineas", new { ProductoId = cana.Id, Cantidad = 1m });

        var resp = await cliente.PostAsync(new Uri($"/comandas/{comanda.Id}/cuenta/imprimir", UriKind.Relative), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

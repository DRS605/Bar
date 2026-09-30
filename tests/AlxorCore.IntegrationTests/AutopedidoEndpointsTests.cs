using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>
/// Pruebas de integración del autopedido por QR (estilo Qamarero): carta multiidioma, pedido del
/// cliente desde la mesa (con token), aceptación por el camarero, avisos de mesa y traducciones.
/// </summary>
public sealed class AutopedidoEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public AutopedidoEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record ProductoResp(Guid Id, string Nombre, decimal PrecioUnitario);
    private sealed record MesaResp(Guid Id, string Nombre, bool Ocupada);
    private sealed record CartaLinkResp(string Url, Guid Token);
    private sealed record CartaItem(Guid Id, string Nombre, string? Descripcion, decimal Precio);
    private sealed record CartaCategoria(string Nombre, List<CartaItem> Items);
    private sealed record CartaResp(string Local, string Idioma, List<CartaCategoria> Categorias);
    private sealed record PedidoCreadoResp(Guid Id, int NumeroLineas);
    private sealed record LineaPedidoResp(Guid ProductoId, string Descripcion, decimal Cantidad, string? Nota);
    private sealed record PedidoPendienteResp(Guid Id, Guid MesaId, string MesaNombre, string Idioma, List<LineaPedidoResp> Lineas);
    private sealed record LineaComandaResp(Guid ProductoId, string Descripcion, decimal Cantidad, string? Nota);
    private sealed record ComandaResp(Guid Id, Guid MesaId, string Estado, decimal Total, List<LineaComandaResp> Lineas);
    private sealed record AvisoResp(Guid Id, Guid MesaId, string MesaNombre, string Tipo);

    private static async Task<ProductoResp> CrearProductoAsync(HttpClient cliente, string nombre, decimal precio, string? categoria = null)
    {
        var resp = await cliente.PostAsJsonAsync("/productos", new
        {
            Nombre = nombre, PrecioUnitario = precio, Tipo = "Bien", CodigoIva = "IVA10", Unidad = "ud", ControlarStock = false, Categoria = categoria,
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
    public async Task Flujo_autopedido_cliente_pide_y_camarero_acepta()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearProductoAsync(cliente, "Caña", 1.50m, "Cervezas");
        var mesa = await CrearMesaAsync(cliente);
        var anon = _fabrica.CreateClient();

        // El local obtiene el enlace/token de la mesa para su QR.
        var link = (await cliente.GetFromJsonAsync<CartaLinkResp>($"/mesas/{mesa.Id}/carta-link"))!;
        link.Token.Should().NotBe(Guid.Empty);
        link.Url.Should().Contain($"m={mesa.Id}");

        // El cliente (anónimo) ve la carta y hace un pedido con el token de la mesa.
        var carta = (await anon.GetFromJsonAsync<CartaResp>($"/carta/{empresaId}/datos?idioma=es"))!;
        carta.Categorias.SelectMany(c => c.Items).Should().Contain(i => i.Id == producto.Id && i.Precio == 1.50m);

        var pedir = await anon.PostAsJsonAsync($"/carta/{empresaId}/mesa/{mesa.Id}/pedido", new
        {
            Token = link.Token,
            Idioma = "es",
            Items = new[] { new { ProductoId = producto.Id, Cantidad = 2m, Nota = "muy fría" } },
        });
        pedir.StatusCode.Should().Be(HttpStatusCode.OK);
        var creado = (await pedir.Content.ReadFromJsonAsync<PedidoCreadoResp>())!;
        creado.NumeroLineas.Should().Be(1);

        // El camarero ve el pedido pendiente.
        var pendientes = await cliente.GetFromJsonAsync<List<PedidoPendienteResp>>("/pedidos-web");
        var pendiente = pendientes!.Single(p => p.Id == creado.Id);
        pendiente.MesaNombre.Should().Be("Mesa 1");
        pendiente.Lineas.Should().ContainSingle(l => l.Descripcion == "Caña" && l.Cantidad == 2m && l.Nota == "muy fría");

        // Lo acepta: se añade a la cuenta de la mesa (con la nota del cliente).
        var aceptar = await cliente.PostAsJsonAsync($"/pedidos-web/{creado.Id}/aceptar", new { });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK);
        var comanda = (await aceptar.Content.ReadFromJsonAsync<ComandaResp>())!;
        comanda.Total.Should().Be(3.00m); // 2 × 1,50 con IVA incluido
        comanda.Lineas.Should().ContainSingle(l => l.ProductoId == producto.Id && l.Cantidad == 2m && l.Nota == "muy fría");

        // Ya no está pendiente (aceptarlo otra vez es conflicto) y la mesa está ocupada.
        (await cliente.PostAsJsonAsync($"/pedidos-web/{creado.Id}/aceptar", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await cliente.GetFromJsonAsync<List<PedidoPendienteResp>>("/pedidos-web"))!.Should().NotContain(p => p.Id == creado.Id);
    }

    [Fact]
    public async Task Un_pedido_con_token_incorrecto_se_rechaza()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearProductoAsync(cliente, "Café", 1.30m);
        var mesa = await CrearMesaAsync(cliente, "Mesa 2");
        var anon = _fabrica.CreateClient();

        var resp = await anon.PostAsJsonAsync($"/carta/{empresaId}/mesa/{mesa.Id}/pedido", new
        {
            Token = Guid.NewGuid(), // token que no es el de la mesa
            Idioma = "es",
            Items = new[] { new { ProductoId = producto.Id, Cantidad = 1m } },
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rechazar_un_pedido_no_toca_la_cuenta()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearProductoAsync(cliente, "Tónica", 2.00m);
        var mesa = await CrearMesaAsync(cliente, "Mesa 3");
        var anon = _fabrica.CreateClient();
        var link = (await cliente.GetFromJsonAsync<CartaLinkResp>($"/mesas/{mesa.Id}/carta-link"))!;

        var creado = (await (await anon.PostAsJsonAsync($"/carta/{empresaId}/mesa/{mesa.Id}/pedido", new
        {
            Token = link.Token, Idioma = "es", Items = new[] { new { ProductoId = producto.Id, Cantidad = 1m } },
        })).Content.ReadFromJsonAsync<PedidoCreadoResp>())!;

        (await cliente.PostAsJsonAsync($"/pedidos-web/{creado.Id}/rechazar", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // La mesa sigue libre: rechazar no abre comanda.
        var mesas = await cliente.GetFromJsonAsync<List<MesaResp>>("/mesas");
        mesas!.Single(m => m.Id == mesa.Id).Ocupada.Should().BeFalse();
    }

    [Fact]
    public async Task Avisos_de_mesa_se_reciben_y_se_atienden()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var mesa = await CrearMesaAsync(cliente, "Mesa 4");
        var anon = _fabrica.CreateClient();
        var link = (await cliente.GetFromJsonAsync<CartaLinkResp>($"/mesas/{mesa.Id}/carta-link"))!;

        var aviso = await anon.PostAsJsonAsync($"/carta/{empresaId}/mesa/{mesa.Id}/aviso", new { Token = link.Token, Tipo = "LlamarCamarero" });
        aviso.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pendientes = await cliente.GetFromJsonAsync<List<AvisoResp>>("/avisos");
        var pend = pendientes!.Single(a => a.MesaId == mesa.Id);
        pend.Tipo.Should().Be("LlamarCamarero");
        pend.MesaNombre.Should().Be("Mesa 4");

        (await cliente.PostAsJsonAsync($"/avisos/{pend.Id}/atender", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<List<AvisoResp>>("/avisos"))!.Should().NotContain(a => a.Id == pend.Id);
    }

    [Fact]
    public async Task La_carta_se_muestra_traducida_al_idioma_pedido()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        var producto = await CrearProductoAsync(cliente, "Caña", 1.50m, "Cervezas");
        var anon = _fabrica.CreateClient();

        // El local traduce el nombre del artículo y de la categoría al inglés.
        (await cliente.PutAsJsonAsync("/carta/traducciones", new
        {
            Ambito = "Producto", Clave = producto.Id.ToString(), Idioma = "en", Nombre = "Draft beer", Descripcion = "Fresh from the tap",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.PutAsJsonAsync("/carta/traducciones", new
        {
            Ambito = "Categoria", Clave = "Cervezas", Idioma = "en", Nombre = "Beers", Descripcion = (string?)null,
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var en = (await anon.GetFromJsonAsync<CartaResp>($"/carta/{empresaId}/datos?idioma=en"))!;
        en.Idioma.Should().Be("en");
        var cat = en.Categorias.Single(c => c.Items.Any(i => i.Id == producto.Id));
        cat.Nombre.Should().Be("Beers");
        cat.Items.Single(i => i.Id == producto.Id).Nombre.Should().Be("Draft beer");
        cat.Items.Single(i => i.Id == producto.Id).Descripcion.Should().Be("Fresh from the tap");

        // En español (base) mantiene el nombre del catálogo.
        var es = (await anon.GetFromJsonAsync<CartaResp>($"/carta/{empresaId}/datos?idioma=es"))!;
        es.Categorias.SelectMany(c => c.Items).Single(i => i.Id == producto.Id).Nombre.Should().Be("Caña");
    }
}

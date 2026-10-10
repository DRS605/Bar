using AlxorCore.Api.Comun;
using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using QRCoder;

namespace AlxorCore.Api.Endpoints;

/// <summary>
/// Carta pública y <b>autopedido</b> de un local (estilo Qamarero). Son endpoints <b>anónimos</b>: el
/// cliente escanea el QR de su mesa, ve la carta en su idioma (español/inglés/francés), pide desde el
/// móvil y puede avisar al camarero. El pedido no toca la cuenta hasta que el camarero lo acepta, y no
/// hay pago online (se paga al final con el camarero). La lectura se acota al local de la URL.
/// </summary>
public static class EndpointsCarta
{
    public sealed record CartaItemDto(Guid Id, string Nombre, string? Descripcion, decimal Precio, IReadOnlyList<string> Alergenos, bool Recomendado, bool Picante, bool Agotado, string? Foto);
    public sealed record CartaCategoriaDto(string Nombre, IReadOnlyList<CartaItemDto> Items);
    public sealed record CartaPublicaDto(string Local, string Idioma, string Tema, IReadOnlyList<CartaCategoriaDto> Categorias);

    public static IEndpointRouteBuilder MapearCarta(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var carta = rutas.MapGroup("/carta").WithTags("Carta pública");

        carta.MapGet("/{empresaId:guid}/datos", DatosAsync)
            .WithSummary("Carta pública de un local en un idioma: categorías, artículos, precios y descripciones.")
            .AllowAnonymous();

        carta.MapGet("/{empresaId:guid}/qr.svg", Qr)
            .WithSummary("Código QR (SVG) que enlaza a la carta pública del local.")
            .AllowAnonymous();

        carta.MapPost("/{empresaId:guid}/mesa/{mesaId:guid}/pedido", PedirAsync)
            .WithSummary("El cliente envía un pedido desde la mesa (requiere el token del QR de la mesa).")
            .AllowAnonymous();

        carta.MapPost("/{empresaId:guid}/mesa/{mesaId:guid}/aviso", AvisarAsync)
            .WithSummary("El cliente avisa desde la mesa: llamar al camarero o pedir la cuenta.")
            .AllowAnonymous();

        carta.MapGet("/{empresaId:guid}/producto/{productoId:guid}/foto", FotoAsync)
            .WithSummary("Foto de un producto para la carta pública.")
            .AllowAnonymous();

        return rutas;
    }

    private static async Task<IResult> DatosAsync(
        Guid empresaId, string? idioma, IContextoEmpresaMutable contexto,
        IConsultaProductos productos, IConsultaEmpresas empresas, IConsultaTraducciones traducciones,
        IConsultaFichasCarta fichas, ObtenerConfiguracionCarta configuracion, IConsultaPlanBar planes, CancellationToken ct)
    {
        // Lectura pública acotada a este local (el filtro de empresa y la RLS usan la empresa fijada).
        contexto.Fijar(empresaId);

        // La carta QR con autopedido es una función del plan Pro: en Essential no existe carta pública.
        if (!PlanesBar.IncluyeFuncionesPro(await planes.ObtenerPlanAsync(empresaId, ct).ConfigureAwait(false)))
        {
            return Results.NotFound();
        }

        var empresa = await empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Results.NotFound();
        }

        var idi = Autopedido.IdiomaDe(idioma);
        var trads = await traducciones.ListarPorIdiomaAsync(empresaId, idi, ct).ConfigureAwait(false);
        var trProducto = trads.Where(t => t.Ambito == nameof(Hosteleria.Dominio.AmbitoTraduccion.Producto))
            .ToDictionary(t => t.Clave, StringComparer.OrdinalIgnoreCase);
        var trCategoria = trads.Where(t => t.Ambito == nameof(Hosteleria.Dominio.AmbitoTraduccion.Categoria))
            .ToDictionary(t => t.Clave, t => t.Nombre, StringComparer.OrdinalIgnoreCase);

        var fichasLista = await fichas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var fichaPorProducto = fichasLista.ToDictionary(f => f.ProductoId);

        var lista = await productos.ListarAsync(empresaId, incluirInactivos: false, ct).ConfigureAwait(false);
        var categorias = lista
            .Where(p => p.PrecioUnitario > 0)
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Categoria) ? "Otros" : p.Categoria!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CartaCategoriaDto(
                trCategoria.TryGetValue(g.Key, out var cat) ? cat : g.Key,
                g.OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
                    .Select(p =>
                    {
                        trProducto.TryGetValue(p.Id.ToString(), out var t);
                        fichaPorProducto.TryGetValue(p.Id, out var ficha);
                        var foto = ficha is { TieneFoto: true } ? $"/carta/{empresaId}/producto/{p.Id}/foto" : null;
                        return new CartaItemDto(p.Id, t?.Nombre ?? p.Nombre, t?.Descripcion, p.PrecioUnitario,
                            ficha?.Alergenos ?? Array.Empty<string>(), ficha?.Recomendado ?? false, ficha?.Picante ?? false, ficha?.Agotado ?? false, foto);
                    })
                    .ToList()))
            .ToList();

        var cfg = await configuracion.EjecutarAsync(empresaId, ct).ConfigureAwait(false);
        return Results.Ok(new CartaPublicaDto(empresa.RazonSocial, Autopedido.CodigoDe(idi), cfg.Tema, categorias));
    }

    private static IResult Qr(Guid empresaId, HttpContext http)
    {
        var url = $"{http.Request.Scheme}://{http.Request.Host}/carta.html?e={empresaId}";
        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(datos).GetGraphic(6);
        return Results.Content(svg, "image/svg+xml");
    }

    private static async Task<IResult> PedirAsync(
        Guid empresaId, Guid mesaId, DatosPedidoWeb datos, IContextoEmpresaMutable contexto, CrearPedidoWeb caso, IConsultaPlanBar planes, CancellationToken ct)
    {
        contexto.Fijar(empresaId);
        if (!PlanesBar.IncluyeFuncionesPro(await planes.ObtenerPlanAsync(empresaId, ct).ConfigureAwait(false)))
        {
            return Results.NotFound();
        }

        var r = await caso.EjecutarAsync(empresaId, mesaId, datos, ct).ConfigureAwait(false);
        return r.EsCorrecto ? Results.Ok(r.Valor) : ResultadosHttp.AProblema(r.Error);
    }

    private static async Task<IResult> AvisarAsync(
        Guid empresaId, Guid mesaId, DatosAvisoMesa datos, IContextoEmpresaMutable contexto, CrearAvisoMesa caso, IConsultaPlanBar planes, CancellationToken ct)
    {
        contexto.Fijar(empresaId);
        if (!PlanesBar.IncluyeFuncionesPro(await planes.ObtenerPlanAsync(empresaId, ct).ConfigureAwait(false)))
        {
            return Results.NotFound();
        }

        return (await caso.EjecutarAsync(empresaId, mesaId, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> FotoAsync(
        Guid empresaId, Guid productoId, IContextoEmpresaMutable contexto, ObtenerFotoProducto caso, IConsultaPlanBar planes, CancellationToken ct)
    {
        contexto.Fijar(empresaId);
        if (!PlanesBar.IncluyeFuncionesPro(await planes.ObtenerPlanAsync(empresaId, ct).ConfigureAwait(false)))
        {
            return Results.NotFound();
        }

        var foto = await caso.EjecutarAsync(empresaId, productoId, ct).ConfigureAwait(false);
        return foto is null ? Results.NotFound() : Results.File(foto.Datos, foto.Tipo);
    }
}

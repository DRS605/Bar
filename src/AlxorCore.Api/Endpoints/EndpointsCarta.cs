using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using QRCoder;

namespace AlxorCore.Api.Endpoints;

/// <summary>
/// Carta pública (solo lectura) de un local y su código QR. Son endpoints <b>anónimos</b>: el cliente
/// del bar escanea el QR y ve la carta sin cuenta. La lectura se acota al local indicado en la URL
/// (fijando el contexto de empresa) y solo expone lo que ya es público: nombre, categoría y precio.
/// </summary>
public static class EndpointsCarta
{
    public sealed record CartaItemDto(string Nombre, decimal Precio);
    public sealed record CartaCategoriaDto(string Nombre, IReadOnlyList<CartaItemDto> Items);
    public sealed record CartaPublicaDto(string Local, IReadOnlyList<CartaCategoriaDto> Categorias);

    public static IEndpointRouteBuilder MapearCarta(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var carta = rutas.MapGroup("/carta").WithTags("Carta pública");

        carta.MapGet("/{empresaId:guid}/datos", DatosAsync)
            .WithSummary("Carta pública (solo lectura) de un local: categorías, artículos y precios.")
            .AllowAnonymous();

        carta.MapGet("/{empresaId:guid}/qr.svg", Qr)
            .WithSummary("Código QR (SVG) que enlaza a la carta pública del local.")
            .AllowAnonymous();

        return rutas;
    }

    private static async Task<IResult> DatosAsync(
        Guid empresaId, IContextoEmpresaMutable contexto, IConsultaProductos productos, IConsultaEmpresas empresas, CancellationToken ct)
    {
        // Lectura pública acotada a este local (el filtro de empresa y la RLS usan la empresa fijada).
        contexto.Fijar(empresaId);

        var empresa = await empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Results.NotFound();
        }

        var lista = await productos.ListarAsync(empresaId, incluirInactivos: false, ct).ConfigureAwait(false);
        var categorias = lista
            .Where(p => p.PrecioUnitario > 0)
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Categoria) ? "Otros" : p.Categoria!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CartaCategoriaDto(
                g.Key,
                g.OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new CartaItemDto(p.Nombre, p.PrecioUnitario))
                    .ToList()))
            .ToList();

        return Results.Ok(new CartaPublicaDto(empresa.RazonSocial, categorias));
    }

    private static IResult Qr(Guid empresaId, HttpContext http)
    {
        var url = $"{http.Request.Scheme}://{http.Request.Host}/carta.html?e={empresaId}";
        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(datos).GetGraphic(6);
        return Results.Content(svg, "image/svg+xml");
    }
}

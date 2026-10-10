using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Idioma en el que el cliente ve la carta pública / hace el autopedido.</summary>
public enum IdiomaCarta
{
    /// <summary>Español (idioma base).</summary>
    Es = 1,

    /// <summary>Inglés.</summary>
    En = 2,

    /// <summary>Francés.</summary>
    Fr = 3,
}

/// <summary>Estado de un pedido hecho por el cliente desde el móvil (autopedido por QR).</summary>
public enum EstadoPedidoWeb
{
    /// <summary>Recibido, a la espera de que el camarero lo acepte o rechace.</summary>
    Pendiente = 1,

    /// <summary>Aceptado: sus líneas se han añadido a la comanda de la mesa.</summary>
    Aceptado = 2,

    /// <summary>Rechazado por el camarero.</summary>
    Rechazado = 3,
}

/// <summary>Se ha recibido un pedido de un cliente desde la mesa (pendiente de aceptación del camarero).</summary>
public sealed record PedidoWebRecibido(Guid PedidoId, Guid EmpresaId, Guid MesaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Línea de un pedido web: un producto del catálogo, su cantidad y una nota opcional para cocina.</summary>
public sealed class LineaPedidoWeb : EntidadBase<Guid>
{
    public const int LongitudMaximaNota = 200;

    private LineaPedidoWeb(Guid id)
        : base(id)
    {
    }

    internal LineaPedidoWeb(Guid empresaId, Guid pedidoWebId, Guid productoId, string descripcion, decimal cantidad, string? nota, decimal? precio = null)
        : base(Guid.NewGuid())
    {
        EmpresaId = empresaId;
        PedidoWebId = pedidoWebId;
        ProductoId = productoId;
        Descripcion = descripcion.Length > LongitudMaximaNota ? descripcion[..LongitudMaximaNota] : descripcion;
        Cantidad = cantidad;
        nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        Nota = nota is { Length: > LongitudMaximaNota } ? nota[..LongitudMaximaNota] : nota;
        Precio = precio;
    }

    /// <summary>Empresa (para el aislamiento multiempresa de la tabla de líneas).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Pedido web al que pertenece la línea.</summary>
    public Guid PedidoWebId { get; private set; }

    /// <summary>Producto del catálogo pedido.</summary>
    public Guid ProductoId { get; private set; }

    /// <summary>Nombre del producto (en español) congelado al pedir, para mostrarlo al camarero.</summary>
    public string Descripcion { get; private set; } = null!;

    /// <summary>Cantidad pedida.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Nota de preparación opcional que el cliente escribe («sin cebolla»…).</summary>
    public string? Nota { get; private set; }

    /// <summary>Precio fijado de la línea cuando no es un producto del catálogo (p. ej. el menú del día); nulo en líneas de producto.</summary>
    public decimal? Precio { get; private set; }
}

/// <summary>
/// Pedido hecho por el cliente desde su móvil escaneando el QR de la mesa (autopedido, estilo Qamarero).
/// Se recibe <see cref="EstadoPedidoWeb.Pendiente"/> y no toca la cuenta hasta que un camarero lo
/// <see cref="Aceptar"/> (sus líneas se añaden entonces a la comanda de la mesa) o lo <see cref="Rechazar"/>.
/// El pago no es online: el cliente paga al final con el camarero.
/// </summary>
public sealed class PedidoWeb : RaizAgregadoEmpresa<Guid>
{
    /// <summary>Tope de líneas de un pedido, para acotar el tamaño de una petición anónima.</summary>
    public const int MaximoLineas = 100;

    private readonly List<LineaPedidoWeb> _lineas = new();

    private PedidoWeb(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private PedidoWeb(Guid id, Guid empresaId, Guid mesaId, IdiomaCarta idioma, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        MesaId = mesaId;
        Idioma = idioma;
        Estado = EstadoPedidoWeb.Pendiente;
        RecibidoEn = ahora;
    }

    /// <summary>Mesa desde la que se pidió (identificada por el QR escaneado).</summary>
    public Guid MesaId { get; private set; }

    /// <summary>Idioma en el que el cliente estaba viendo la carta al pedir.</summary>
    public IdiomaCarta Idioma { get; private set; }

    /// <summary>Estado del pedido en su ciclo de vida.</summary>
    public EstadoPedidoWeb Estado { get; private set; }

    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>Momento en que el camarero lo aceptó o rechazó.</summary>
    public DateTimeOffset? ResueltoEn { get; private set; }

    /// <summary>Comanda a la que se añadieron sus líneas al aceptarlo (nulo mientras está pendiente).</summary>
    public Guid? ComandaId { get; private set; }

    public IReadOnlyList<LineaPedidoWeb> Lineas => _lineas.AsReadOnly();

    /// <summary>Crea un pedido pendiente a partir de los artículos elegidos por el cliente.</summary>
    public static Resultado<PedidoWeb> Crear(
        Guid empresaId, Guid mesaId, IdiomaCarta idioma,
        IEnumerable<(Guid ProductoId, string Descripcion, decimal Cantidad, string? Nota, decimal? Precio)> items, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        ArgumentNullException.ThrowIfNull(items);

        var lista = items.Where(i => i.Cantidad > 0).ToList();
        if (lista.Count == 0)
        {
            return Resultado.Fallo<PedidoWeb>(Error.Validacion("pedido_web.sin_lineas", "El pedido no tiene artículos."));
        }

        if (lista.Count > MaximoLineas)
        {
            return Resultado.Fallo<PedidoWeb>(Error.Validacion("pedido_web.demasiadas_lineas", "El pedido tiene demasiados artículos."));
        }

        var pedido = new PedidoWeb(Guid.NewGuid(), empresaId, mesaId, idioma, reloj.AhoraUtc);
        foreach (var item in lista)
        {
            pedido._lineas.Add(new LineaPedidoWeb(empresaId, pedido.Id, item.ProductoId, item.Descripcion, item.Cantidad, item.Nota, item.Precio));
        }

        pedido.RegistrarEvento(new PedidoWebRecibido(pedido.Id, empresaId, mesaId, reloj.AhoraUtc));
        return Resultado.Ok(pedido);
    }

    /// <summary>Marca el pedido como aceptado; sus líneas se han añadido a la comanda indicada.</summary>
    public Resultado Aceptar(Guid comandaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (Estado != EstadoPedidoWeb.Pendiente)
        {
            return Resultado.Fallo(Error.Conflicto("pedido_web.no_pendiente", "El pedido ya no está pendiente."));
        }

        Estado = EstadoPedidoWeb.Aceptado;
        ComandaId = comandaId;
        ResueltoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Rechaza el pedido (no se añade nada a la cuenta).</summary>
    public Resultado Rechazar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (Estado != EstadoPedidoWeb.Pendiente)
        {
            return Resultado.Fallo(Error.Conflicto("pedido_web.no_pendiente", "El pedido ya no está pendiente."));
        }

        Estado = EstadoPedidoWeb.Rechazado;
        ResueltoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }
}

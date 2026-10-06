using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Vista de una mesa, con su ocupación actual deducida de la comanda abierta (si la hay).</summary>
public sealed record MesaDto(
    Guid Id,
    string Nombre,
    string? Zona,
    int Capacidad,
    string Forma,
    double PosX,
    double PosY,
    bool Activa,
    bool Ocupada,
    Guid? ComandaAbiertaId,
    decimal TotalComandaAbierta)
{
    public static MesaDto Desde(Mesa m, bool ocupada = false, Guid? comandaAbiertaId = null, decimal totalComandaAbierta = 0m) =>
        new(m.Id, m.Nombre, m.Zona, m.Capacidad, m.Forma.ToString(), m.PosX, m.PosY, m.Activa, ocupada, comandaAbiertaId, totalComandaAbierta);
}

/// <summary>Vista de una línea de comanda.</summary>
public sealed record LineaComandaDto(
    Guid Id,
    Guid ProductoId,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    string CodigoIva,
    decimal PorcentajeIva,
    decimal Base,
    decimal CuotaIva,
    decimal Total,
    decimal CantidadCobrada,
    decimal CantidadPendienteCobro,
    string? Nota)
{
    public static LineaComandaDto Desde(LineaComanda l) =>
        new(l.Id, l.ProductoId, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva, l.Total, l.CantidadCobrada, l.CantidadPendienteCobro, l.Nota);
}

/// <summary>Vista completa de una comanda con sus líneas.</summary>
public sealed record ComandaDto(
    Guid Id,
    Guid MesaId,
    string Estado,
    DateTimeOffset AbiertaEn,
    DateTimeOffset? CerradaEn,
    string? Notas,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal Total,
    string? MetodoCobro,
    Guid? FacturaId,
    string? NumeroTicket,
    bool TieneCobroParcial,
    decimal TotalPendienteCobro,
    decimal DescuentoPorcentaje,
    IReadOnlyList<LineaComandaDto> Lineas)
{
    public static ComandaDto Desde(Comanda c) => new(
        c.Id, c.MesaId, c.Estado.ToString(), c.AbiertaEn, c.CerradaEn, c.Notas,
        c.BaseImponible, c.CuotaIva, c.Total, c.MetodoCobro?.ToString(), c.FacturaId, c.NumeroTicket,
        c.TieneCobroParcial, c.TotalPendienteCobro, c.DescuentoPorcentaje,
        c.Lineas.Select(LineaComandaDto.Desde).ToList());
}

/// <summary>Resumen de una comanda para listados.</summary>
public sealed record ComandaResumen(
    Guid Id,
    Guid MesaId,
    string MesaNombre,
    string Estado,
    DateTimeOffset AbiertaEn,
    int NumeroLineas,
    decimal Total);

/// <summary>Repositorio de mesas (escritura).</summary>
public interface IRepositorioMesas
{
    Task<Mesa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Mesa mesa);
}

/// <summary>Consultas de lectura de mesas.</summary>
public interface IConsultaMesas
{
    Task<MesaDto?> ObtenerAsync(Guid mesaId, CancellationToken ct = default);

    Task<IReadOnlyList<MesaDto>> ListarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default);
}

/// <summary>Repositorio de comandas (escritura). Las lecturas cargan también las líneas.</summary>
public interface IRepositorioComandas
{
    Task<Comanda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Comanda abierta de una mesa, o <c>null</c> si la mesa está libre.</summary>
    Task<Comanda?> ObtenerAbiertaPorMesaAsync(Guid mesaId, CancellationToken ct = default);

    void Agregar(Comanda comanda);
}

/// <summary>Consultas de lectura de comandas.</summary>
public interface IConsultaComandas
{
    Task<ComandaDto?> ObtenerAsync(Guid comandaId, CancellationToken ct = default);

    Task<IReadOnlyList<ComandaResumen>> ListarAbiertasAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Hostelería.</summary>
public interface IUnidadDeTrabajoHosteleria : IUnidadDeTrabajo;

// ---------------------------------------------------------------------------
// Autopedido por QR: carta interactiva, pedidos del cliente y avisos de mesa.
// ---------------------------------------------------------------------------

/// <summary>Línea de un pedido web tal como llega del cliente (resumen para el camarero).</summary>
public sealed record LineaPedidoWebResumen(Guid ProductoId, string Descripcion, decimal Cantidad, string? Nota);

/// <summary>Pedido hecho por el cliente desde la mesa, pendiente de que el camarero lo acepte.</summary>
public sealed record PedidoWebResumen(
    Guid Id,
    Guid MesaId,
    string MesaNombre,
    string Idioma,
    DateTimeOffset RecibidoEn,
    IReadOnlyList<LineaPedidoWebResumen> Lineas);

/// <summary>Aviso pendiente lanzado por un cliente desde una mesa.</summary>
public sealed record AvisoMesaDto(Guid Id, Guid MesaId, string MesaNombre, string Tipo, DateTimeOffset RecibidoEn);

/// <summary>Traducción de un texto de la carta a un idioma.</summary>
public sealed record TraduccionCartaDto(string Ambito, string Clave, string Idioma, string Nombre, string? Descripcion)
{
    public static TraduccionCartaDto Desde(TraduccionCarta t) =>
        new(t.Ambito.ToString(), t.Clave, t.Idioma.ToString(), t.Nombre, t.Descripcion);
}

/// <summary>Repositorio de pedidos web (autopedido).</summary>
public interface IRepositorioPedidosWeb
{
    Task<PedidoWeb?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(PedidoWeb pedido);
}

/// <summary>Consultas de lectura de pedidos web.</summary>
public interface IConsultaPedidosWeb
{
    Task<IReadOnlyList<PedidoWebResumen>> ListarPendientesAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Repositorio de avisos de mesa.</summary>
public interface IRepositorioAvisos
{
    Task<AvisoMesa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(AvisoMesa aviso);
}

/// <summary>Consultas de lectura de avisos de mesa.</summary>
public interface IConsultaAvisos
{
    Task<IReadOnlyList<AvisoMesaDto>> ListarPendientesAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Repositorio de traducciones de la carta.</summary>
public interface IRepositorioTraducciones
{
    Task<TraduccionCarta?> ObtenerAsync(Guid empresaId, AmbitoTraduccion ambito, string clave, IdiomaCarta idioma, CancellationToken ct = default);

    void Agregar(TraduccionCarta traduccion);

    void Quitar(TraduccionCarta traduccion);
}

/// <summary>Consultas de lectura de traducciones de la carta.</summary>
public interface IConsultaTraducciones
{
    Task<IReadOnlyList<TraduccionCartaDto>> ListarAsync(Guid empresaId, CancellationToken ct = default);

    /// <summary>Traducciones de un idioma concreto (para pintar la carta pública).</summary>
    Task<IReadOnlyList<TraduccionCartaDto>> ListarPorIdiomaAsync(Guid empresaId, IdiomaCarta idioma, CancellationToken ct = default);
}

/// <summary>Ficha de carta de un producto: sus alérgenos, distintivos (recomendado/picante) y si tiene foto.</summary>
public sealed record FichaCartaDto(Guid ProductoId, IReadOnlyList<string> Alergenos, bool Recomendado, bool Picante, bool TieneFoto)
{
    public static FichaCartaDto Desde(FichaCarta f) => new(f.ProductoId, Dominio.Alergenos.ANombres(f.Alergenos), f.Recomendado, f.Picante, f.TieneFoto);
}

/// <summary>Imagen de un producto (para servirla en la carta).</summary>
public sealed record FotoProducto(byte[] Datos, string Tipo);

/// <summary>Repositorio de fichas de carta (alérgenos y foto por producto).</summary>
public interface IRepositorioFichasCarta
{
    Task<FichaCarta?> ObtenerPorProductoAsync(Guid productoId, CancellationToken ct = default);

    void Agregar(FichaCarta ficha);
}

/// <summary>Consultas de lectura de fichas de carta.</summary>
public interface IConsultaFichasCarta
{
    Task<IReadOnlyList<FichaCartaDto>> ListarAsync(Guid empresaId, CancellationToken ct = default);

    Task<FotoProducto?> ObtenerFotoAsync(Guid empresaId, Guid productoId, CancellationToken ct = default);
}

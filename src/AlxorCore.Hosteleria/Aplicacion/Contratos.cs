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
    decimal CantidadPendienteCobro)
{
    public static LineaComandaDto Desde(LineaComanda l) =>
        new(l.Id, l.ProductoId, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva, l.Total, l.CantidadCobrada, l.CantidadPendienteCobro);
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
    IReadOnlyList<LineaComandaDto> Lineas)
{
    public static ComandaDto Desde(Comanda c) => new(
        c.Id, c.MesaId, c.Estado.ToString(), c.AbiertaEn, c.CerradaEn, c.Notas,
        c.BaseImponible, c.CuotaIva, c.Total, c.MetodoCobro?.ToString(), c.FacturaId, c.NumeroTicket,
        c.TieneCobroParcial, c.TotalPendienteCobro,
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

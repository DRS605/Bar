using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>Un artículo del ranking de ventas: unidades vendidas, importe (neto) y margen.</summary>
public sealed record VentaProductoDto(Guid? ProductoId, string Descripcion, decimal Unidades, decimal Importe, decimal Margen);

/// <summary>Ventas de un día de la semana (1 = lunes … 7 = domingo).</summary>
public sealed record VentaDiaSemanaDto(int DiaSemana, string Nombre, decimal Importe, int Tickets);

/// <summary>
/// Informe comercial de ventas de un periodo: número de tickets, venta total y <b>ticket medio</b>,
/// el reparto de la venta <b>por día de la semana</b> (para ver los días fuertes) y los artículos
/// <b>más vendidos por unidades</b>. Distinto del informe de beneficio (que ordena por margen).
/// </summary>
public sealed record InformeVentasDto(
    DateOnly Desde,
    DateOnly Hasta,
    int Tickets,
    decimal VentaTotal,
    decimal TicketMedio,
    IReadOnlyList<VentaProductoDto> TopProductos,
    IReadOnlyList<VentaDiaSemanaDto> PorDiaSemana);

/// <summary>
/// Caso de uso: informe comercial de ventas de un periodo. Se calcula sobre las facturas emitidas
/// (tickets incluidos): sus totales dan el número de tickets, la venta y el ticket medio, y su fecha
/// de emisión el reparto por día de la semana; las líneas dan el ranking de artículos por unidades.
/// </summary>
public sealed class GenerarInformeVentas
{
    private const int TopN = 10;
    private static readonly string[] Dias = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

    private readonly IConsultaFacturas _facturas;

    public GenerarInformeVentas(IConsultaFacturas facturas) => _facturas = facturas;

    public async Task<InformeVentasDto> EjecutarAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var todas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var facturas = todas
            .Where(f => f.FechaEmision >= desde && f.FechaEmision <= hasta && f.Estado != "Anulada")
            .ToList();

        var tickets = facturas.Count;
        var ventaTotal = Redondeo.Dos(facturas.Sum(f => f.Total));
        var ticketMedio = tickets > 0 ? Redondeo.Dos(ventaTotal / tickets) : 0m;

        // Reparto por día de la semana (fecha de emisión, sin ambigüedad de zona horaria). Siempre L..D.
        var porDia = Enumerable.Range(1, 7)
            .Select(d =>
            {
                var delDia = facturas.Where(f => DiaIso(f.FechaEmision) == d).ToList();
                return new VentaDiaSemanaDto(d, Dias[d - 1], Redondeo.Dos(delDia.Sum(f => f.Total)), delDia.Count);
            })
            .ToList();

        // Artículos más vendidos por unidades (las líneas ya vienen agregadas por el margen).
        var lineas = await _facturas.ListarLineasMargenAsync(empresaId, desde, hasta, ct).ConfigureAwait(false);
        var topProductos = lineas
            .GroupBy(l => l.ProductoId is { } id ? id.ToString() : "c:" + l.Descripcion)
            .Select(g => new VentaProductoDto(
                g.First().ProductoId,
                g.First().Descripcion,
                g.Sum(l => l.Cantidad),
                Redondeo.Dos(g.Sum(l => l.Ingreso)),
                Redondeo.Dos(g.Sum(l => l.Ingreso - l.Coste))))
            .OrderByDescending(p => p.Unidades)
            .ThenByDescending(p => p.Importe)
            .Take(TopN)
            .ToList();

        return new InformeVentasDto(desde, hasta, tickets, ventaTotal, ticketMedio, topProductos, porDia);
    }

    // Lunes = 1 … Domingo = 7 (DayOfWeek pone el domingo a 0).
    private static int DiaIso(DateOnly fecha) => fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek;
}

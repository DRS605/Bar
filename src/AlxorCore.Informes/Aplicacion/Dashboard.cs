using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Tesoreria.Aplicacion;
using AlxorCore.Tesoreria.Dominio;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>Resumen para el panel principal.</summary>
public sealed record DashboardDto(
    int Anio,
    int Mes,
    decimal FacturadoMes,
    decimal GastadoMes,
    int NumeroFacturasMes,
    decimal PendienteCobro,
    decimal PendientePago);

/// <summary>Caso de uso: datos del panel principal (totales del mes y pendientes).</summary>
public sealed class ObtenerDashboard
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;
    private readonly IConsultaTesoreria _tesoreria;
    private readonly IReloj _reloj;

    public ObtenerDashboard(IConsultaFacturas facturas, IConsultaGastos gastos, IConsultaTesoreria tesoreria, IReloj reloj)
    {
        _facturas = facturas;
        _gastos = gastos;
        _tesoreria = tesoreria;
        _reloj = reloj;
    }

    public async Task<DashboardDto> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);

        var facturas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);

        var facturasMes = facturas.Where(f => f.FechaEmision.Year == hoy.Year && f.FechaEmision.Month == hoy.Month).ToList();
        var gastosMes = gastos.Where(g => g.Fecha.Year == hoy.Year && g.Fecha.Month == hoy.Month).ToList();

        var totalFacturado = facturas.Sum(f => f.Total);
        var totalGastos = gastos.Sum(g => g.Total);
        var cobrado = await _tesoreria.TotalLiquidadoAsync(TipoDocumentoTesoreria.Factura, ct).ConfigureAwait(false);
        var pagado = await _tesoreria.TotalLiquidadoAsync(TipoDocumentoTesoreria.Gasto, ct).ConfigureAwait(false);

        return new DashboardDto(
            hoy.Year,
            hoy.Month,
            Redondeo.Dos(facturasMes.Sum(f => f.Total)),
            Redondeo.Dos(gastosMes.Sum(g => g.Total)),
            facturasMes.Count,
            Redondeo.Dos(totalFacturado - cobrado),
            Redondeo.Dos(totalGastos - pagado));
    }
}

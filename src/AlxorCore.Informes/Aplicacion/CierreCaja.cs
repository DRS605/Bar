using AlxorCore.Nucleo.Comun;
using AlxorCore.Tesoreria.Aplicacion;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>Cobros de un día agrupados por método de pago.</summary>
public sealed record CierreCajaMetodoDto(string Metodo, decimal Importe, int Numero);

/// <summary>
/// Cierre de caja (arqueo) de un día: total cobrado, desglosado por método de pago, total pagado
/// (salidas) y neto. Pensado para cuadrar la caja de una tienda al cerrar.
/// </summary>
public sealed record CierreCajaDto(
    DateOnly Dia, decimal TotalCobrado, decimal TotalPagado, decimal Neto, IReadOnlyList<CierreCajaMetodoDto> CobrosPorMetodo);

/// <summary>Caso de uso: cierre de caja de un día a partir de los movimientos de Tesorería.</summary>
public sealed class GenerarCierreCaja
{
    private readonly IConsultaTesoreria _tesoreria;

    public GenerarCierreCaja(IConsultaTesoreria tesoreria) => _tesoreria = tesoreria;

    public async Task<CierreCajaDto> EjecutarAsync(Guid empresaId, DateOnly dia, CancellationToken ct = default)
    {
        var movimientos = await _tesoreria.ListarPorPeriodoAsync(empresaId, dia, dia, ct).ConfigureAwait(false);

        var cobros = movimientos.Where(m => m.Sentido == "Cobro").ToList();
        var pagos = movimientos.Where(m => m.Sentido == "Pago").ToList();

        var porMetodo = cobros
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Metodo) ? "Sin especificar" : c.Metodo!)
            .Select(g => new CierreCajaMetodoDto(g.Key, Redondeo.Dos(g.Sum(x => x.Importe)), g.Count()))
            .OrderByDescending(m => m.Importe)
            .ToList();

        var totalCobrado = Redondeo.Dos(cobros.Sum(c => c.Importe));
        var totalPagado = Redondeo.Dos(pagos.Sum(p => p.Importe));
        return new CierreCajaDto(dia, totalCobrado, totalPagado, Redondeo.Dos(totalCobrado - totalPagado), porMetodo);
    }
}

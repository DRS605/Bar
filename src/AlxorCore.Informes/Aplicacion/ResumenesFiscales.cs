using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>
/// Resumen del <b>modelo 303</b> (autoliquidación trimestral de IVA): IVA devengado
/// (repercutido en las facturas emitidas del trimestre) menos IVA deducible (soportado en los
/// gastos del trimestre).
/// </summary>
public sealed record Modelo303Dto(
    int Anio, int Trimestre, DateOnly Desde, DateOnly Hasta,
    decimal IvaDevengadoBase, decimal IvaDevengadoCuota,
    decimal IvaDeducibleBase, decimal IvaDeducibleCuota,
    decimal Resultado);

/// <summary>
/// Resumen del <b>modelo 130</b> (pago fraccionado del IRPF en estimación directa). Es
/// <b>acumulativo</b> desde el 1 de enero: sobre el rendimiento neto acumulado (ingresos − gastos)
/// se aplica el 20 %, del que se descuentan las retenciones soportadas y los pagos fraccionados de
/// los trimestres anteriores del mismo ejercicio.
/// </summary>
public sealed record Modelo130Dto(
    int Anio, int Trimestre, DateOnly Desde, DateOnly Hasta,
    decimal IngresosAcumulados, decimal GastosAcumulados, decimal RendimientoAcumulado,
    decimal PagoFraccionadoBruto, decimal RetencionesAcumuladas, decimal PagosAnteriores,
    decimal Resultado);

/// <summary>Resumen fiscal de un trimestre: modelos 303 (IVA) y 130 (IRPF).</summary>
public sealed record ResumenTrimestralDto(Modelo303Dto Modelo303, Modelo130Dto Modelo130);

/// <summary>
/// Caso de uso: calcula los resúmenes fiscales de un trimestre (303 y 130) a partir de las facturas
/// emitidas y los gastos registrados. Es una <b>ayuda informativa</b> para preparar la
/// autoliquidación con la gestoría, no un envío oficial a la AEAT.
/// </summary>
public sealed class GenerarResumenesFiscales
{
    private const decimal PorcentajePagoFraccionado = 0.20m;

    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;

    public GenerarResumenesFiscales(IConsultaFacturas facturas, IConsultaGastos gastos)
    {
        _facturas = facturas;
        _gastos = gastos;
    }

    public async Task<ResumenTrimestralDto> EjecutarAsync(Guid empresaId, int anio, int trimestre, CancellationToken ct = default)
    {
        if (trimestre is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(trimestre), trimestre, "El trimestre debe estar entre 1 y 4.");
        }

        var facturas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);

        // Solo cuentan las facturas realmente emitidas (se excluyen las anuladas y las ya
        // sustituidas por una rectificativa, que aportaría los importes corregidos).
        var emitidas = facturas.Where(f => f.Estado == "Emitida").ToList();

        return new ResumenTrimestralDto(
            Calcular303(anio, trimestre, emitidas, gastos),
            Calcular130(anio, trimestre, emitidas, gastos));
    }

    private static Modelo303Dto Calcular303(int anio, int trimestre, IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<GastoDto> gastos)
    {
        var (desde, hasta) = RangoTrimestre(anio, trimestre);

        var devengado = facturas.Where(f => f.FechaEmision >= desde && f.FechaEmision <= hasta).ToList();
        var deducible = gastos.Where(g => g.Fecha >= desde && g.Fecha <= hasta).ToList();

        var devBase = Redondeo.Dos(devengado.Sum(f => f.BaseImponible));
        var devCuota = Redondeo.Dos(devengado.Sum(f => f.CuotaIva));
        var dedBase = Redondeo.Dos(deducible.Sum(g => g.BaseImponible));
        var dedCuota = Redondeo.Dos(deducible.Sum(g => g.CuotaIva));

        return new Modelo303Dto(anio, trimestre, desde, hasta, devBase, devCuota, dedBase, dedCuota, Redondeo.Dos(devCuota - dedCuota));
    }

    private static Modelo130Dto Calcular130(int anio, int trimestre, IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<GastoDto> gastos)
    {
        // El 130 es acumulativo: para calcular los "pagos anteriores" hay que recorrer trimestre a
        // trimestre desde el primero hasta el pedido, arrastrando lo ya ingresado.
        decimal pagosAnteriores = 0m;
        Modelo130Dto? actual = null;

        for (var t = 1; t <= trimestre; t++)
        {
            var (_, hasta) = RangoTrimestre(anio, t);
            var inicioAnio = new DateOnly(anio, 1, 1);

            var ingresos = Redondeo.Dos(facturas.Where(f => f.FechaEmision >= inicioAnio && f.FechaEmision <= hasta).Sum(f => f.BaseImponible));
            var gastosAcum = Redondeo.Dos(gastos.Where(g => g.Fecha >= inicioAnio && g.Fecha <= hasta).Sum(g => g.BaseImponible));
            var retenciones = Redondeo.Dos(facturas.Where(f => f.FechaEmision >= inicioAnio && f.FechaEmision <= hasta).Sum(f => f.RetencionIrpf));

            var rendimiento = Redondeo.Dos(ingresos - gastosAcum);
            var bruto = Redondeo.Dos(Math.Max(0m, rendimiento * PorcentajePagoFraccionado));
            var resultado = Redondeo.Dos(Math.Max(0m, bruto - retenciones - pagosAnteriores));

            var (desdeT, hastaT) = RangoTrimestre(anio, t);
            actual = new Modelo130Dto(anio, t, desdeT, hastaT, ingresos, gastosAcum, rendimiento, bruto, retenciones, Redondeo.Dos(pagosAnteriores), resultado);
            pagosAnteriores += resultado;
        }

        return actual!;
    }

    private static (DateOnly Desde, DateOnly Hasta) RangoTrimestre(int anio, int trimestre)
    {
        var mesInicio = ((trimestre - 1) * 3) + 1;
        var desde = new DateOnly(anio, mesInicio, 1);
        var hasta = desde.AddMonths(3).AddDays(-1);
        return (desde, hasta);
    }
}

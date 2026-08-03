using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>Beneficio de un producto (o concepto) en el periodo: ingresos, coste y margen.</summary>
public sealed record BeneficioProductoDto(
    Guid? ProductoId, string Descripcion, decimal Cantidad, decimal Ingresos, decimal Coste, decimal Margen);

/// <summary>
/// Informe de beneficio de un periodo. <b>Margen bruto</b> = ingresos de venta − coste (precio de
/// compra). <b>Beneficio neto</b> = margen bruto − gastos genéricos del periodo.
/// </summary>
public sealed record BeneficioDto(
    DateOnly Desde, DateOnly Hasta,
    decimal Ingresos, decimal Coste, decimal MargenBruto,
    decimal Gastos, decimal BeneficioNeto,
    IReadOnlyList<BeneficioProductoDto> PorProducto);

/// <summary>
/// Caso de uso: calcula el beneficio de un periodo a partir del margen de las líneas de las facturas
/// emitidas (venta − compra) y los gastos genéricos registrados.
/// </summary>
public sealed class GenerarBeneficio
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;

    public GenerarBeneficio(IConsultaFacturas facturas, IConsultaGastos gastos)
    {
        _facturas = facturas;
        _gastos = gastos;
    }

    public async Task<BeneficioDto> EjecutarAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var lineas = await _facturas.ListarLineasMargenAsync(empresaId, desde, hasta, ct).ConfigureAwait(false);
        var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);

        var porProducto = lineas
            .GroupBy(l => l.ProductoId is { } id ? id.ToString() : "c:" + l.Descripcion)
            .Select(g =>
            {
                var ingresos = Redondeo.Dos(g.Sum(l => l.Ingreso));
                var coste = Redondeo.Dos(g.Sum(l => l.Coste));
                return new BeneficioProductoDto(
                    g.First().ProductoId, g.First().Descripcion, g.Sum(l => l.Cantidad),
                    ingresos, coste, Redondeo.Dos(ingresos - coste));
            })
            .OrderByDescending(p => p.Margen)
            .ToList();

        var totalIngresos = Redondeo.Dos(lineas.Sum(l => l.Ingreso));
        var totalCoste = Redondeo.Dos(lineas.Sum(l => l.Coste));
        var margenBruto = Redondeo.Dos(totalIngresos - totalCoste);

        var totalGastos = Redondeo.Dos(gastos.Where(gg => gg.Fecha >= desde && gg.Fecha <= hasta).Sum(gg => gg.BaseImponible));
        var beneficioNeto = Redondeo.Dos(margenBruto - totalGastos);

        return new BeneficioDto(desde, hasta, totalIngresos, totalCoste, margenBruto, totalGastos, beneficioNeto, porProducto);
    }
}

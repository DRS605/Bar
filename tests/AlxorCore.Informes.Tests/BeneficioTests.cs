using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Informes.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Informes.Tests;

public class BeneficioTests
{
    private static readonly Guid Empresa = Guid.NewGuid();

    private static GastoDto Gasto(DateOnly fecha, decimal baseImp) =>
        new(Guid.NewGuid(), null, "Proveedor", "Gasto", fecha, baseImp, "IVA21", 21m, baseImp * 0.21m, 0m, 0m, baseImp * 1.21m, "Registrado");

    private sealed class FakeFacturas(IReadOnlyList<LineaMargenDto> lineas) : IConsultaFacturas
    {
        public Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default) => Task.FromResult<FacturaDto?>(null);

        public Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FacturaResumen>>([]);

        public Task<IReadOnlyList<LineaMargenDto>> ListarLineasMargenAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default) =>
            Task.FromResult(lineas);
    }

    private sealed class FakeGastos(IReadOnlyList<GastoDto> lista) : IConsultaGastos
    {
        public Task<GastoDto?> ObtenerAsync(Guid gastoId, CancellationToken ct = default) => Task.FromResult<GastoDto?>(null);

        public Task<IReadOnlyList<GastoDto>> ListarAsync(Guid empresaId, CancellationToken ct = default) => Task.FromResult(lista);
    }

    private static GenerarBeneficio Caso(IReadOnlyList<LineaMargenDto> lineas, IReadOnlyList<GastoDto> gastos) =>
        new(new FakeFacturas(lineas), new FakeGastos(gastos));

    [Fact]
    public async Task Margen_bruto_es_ingresos_menos_coste()
    {
        var caso = Caso(
            [
                new LineaMargenDto(Guid.NewGuid(), "Producto A", 2, Ingreso: 200m, Coste: 120m),
                new LineaMargenDto(Guid.NewGuid(), "Producto B", 1, Ingreso: 100m, Coste: 30m),
            ],
            []);

        var b = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        b.Ingresos.Should().Be(300m);
        b.Coste.Should().Be(150m);
        b.MargenBruto.Should().Be(150m); // 300 - 150
        b.Gastos.Should().Be(0m);
        b.BeneficioNeto.Should().Be(150m);
        b.PorProducto.Should().HaveCount(2);
        b.PorProducto[0].Margen.Should().Be(80m); // Producto A (200-120), ordenado por margen desc
    }

    [Fact]
    public async Task Beneficio_neto_descuenta_los_gastos_del_periodo()
    {
        var caso = Caso(
            [new LineaMargenDto(null, "Servicio", 1, Ingreso: 1000m, Coste: 0m)],
            [
                Gasto(new DateOnly(2026, 3, 5), 200m), // dentro
                Gasto(new DateOnly(2026, 3, 20), 100m), // dentro
                Gasto(new DateOnly(2025, 12, 31), 999m), // fuera del periodo
            ]);

        var b = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        b.MargenBruto.Should().Be(1000m);
        b.Gastos.Should().Be(300m); // solo los dos de 2026
        b.BeneficioNeto.Should().Be(700m); // 1000 - 300
    }

    [Fact]
    public async Task Agrupa_por_producto_sumando_lineas_repetidas()
    {
        var producto = Guid.NewGuid();
        var caso = Caso(
            [
                new LineaMargenDto(producto, "Café", 10, Ingreso: 20m, Coste: 8m),
                new LineaMargenDto(producto, "Café", 5, Ingreso: 10m, Coste: 4m),
            ],
            []);

        var b = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        b.PorProducto.Should().ContainSingle();
        b.PorProducto[0].Cantidad.Should().Be(15);
        b.PorProducto[0].Ingresos.Should().Be(30m);
        b.PorProducto[0].Coste.Should().Be(12m);
        b.PorProducto[0].Margen.Should().Be(18m);
    }
}

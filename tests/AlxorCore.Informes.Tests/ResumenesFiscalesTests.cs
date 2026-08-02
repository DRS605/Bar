using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Informes.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Informes.Tests;

public class ResumenesFiscalesTests
{
    private static readonly Guid Empresa = Guid.NewGuid();

    private static FacturaResumen Factura(DateOnly fecha, decimal baseImp, decimal cuota, decimal retencion = 0m, string estado = "Emitida") =>
        new(Guid.NewGuid(), "FA2026/0001", fecha, fecha, "Cliente", "B12345674", baseImp, cuota, retencion, baseImp + cuota - retencion, estado, "Ordinaria");

    private static GastoDto Gasto(DateOnly fecha, decimal baseImp, decimal cuota) =>
        new(Guid.NewGuid(), null, "Proveedor", "Concepto", fecha, baseImp, "IVA21", 21m, cuota, 0m, 0m, baseImp + cuota, "Registrado");

    private sealed class FakeFacturas(IReadOnlyList<FacturaResumen> lista) : IConsultaFacturas
    {
        public Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default) => Task.FromResult<FacturaDto?>(null);

        public Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default) => Task.FromResult(lista);
    }

    private sealed class FakeGastos(IReadOnlyList<GastoDto> lista) : IConsultaGastos
    {
        public Task<GastoDto?> ObtenerAsync(Guid gastoId, CancellationToken ct = default) => Task.FromResult<GastoDto?>(null);

        public Task<IReadOnlyList<GastoDto>> ListarAsync(Guid empresaId, CancellationToken ct = default) => Task.FromResult(lista);
    }

    private static GenerarResumenesFiscales Caso(IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<GastoDto> gastos) =>
        new(new FakeFacturas(facturas), new FakeGastos(gastos));

    [Fact]
    public async Task El_303_es_iva_repercutido_menos_soportado_del_trimestre()
    {
        var caso = Caso(
            [Factura(new DateOnly(2026, 2, 10), 1000m, 210m)],
            [Gasto(new DateOnly(2026, 3, 5), 400m, 84m)]);

        var resumen = await caso.EjecutarAsync(Empresa, 2026, 1);

        resumen.Modelo303.IvaDevengadoBase.Should().Be(1000m);
        resumen.Modelo303.IvaDevengadoCuota.Should().Be(210m);
        resumen.Modelo303.IvaDeducibleBase.Should().Be(400m);
        resumen.Modelo303.IvaDeducibleCuota.Should().Be(84m);
        resumen.Modelo303.Resultado.Should().Be(126m); // 210 - 84
    }

    [Fact]
    public async Task El_303_solo_toma_las_fechas_del_trimestre_pedido()
    {
        var caso = Caso(
            [
                Factura(new DateOnly(2026, 2, 10), 1000m, 210m), // T1
                Factura(new DateOnly(2026, 5, 10), 500m, 105m),  // T2
            ],
            []);

        var t1 = await caso.EjecutarAsync(Empresa, 2026, 1);
        var t2 = await caso.EjecutarAsync(Empresa, 2026, 2);

        t1.Modelo303.IvaDevengadoCuota.Should().Be(210m);
        t2.Modelo303.IvaDevengadoCuota.Should().Be(105m);
    }

    [Fact]
    public async Task El_130_aplica_el_20_por_ciento_al_rendimiento_menos_retenciones()
    {
        var caso = Caso(
            [Factura(new DateOnly(2026, 2, 10), 1000m, 210m, retencion: 50m)],
            [Gasto(new DateOnly(2026, 3, 5), 400m, 84m)]);

        var resumen = await caso.EjecutarAsync(Empresa, 2026, 1);
        var m = resumen.Modelo130;

        m.IngresosAcumulados.Should().Be(1000m);
        m.GastosAcumulados.Should().Be(400m);
        m.RendimientoAcumulado.Should().Be(600m);
        m.PagoFraccionadoBruto.Should().Be(120m); // 20% de 600
        m.RetencionesAcumuladas.Should().Be(50m);
        m.PagosAnteriores.Should().Be(0m);
        m.Resultado.Should().Be(70m); // 120 - 50 - 0
    }

    [Fact]
    public async Task El_130_es_acumulativo_y_descuenta_los_pagos_de_trimestres_anteriores()
    {
        var caso = Caso(
            [
                Factura(new DateOnly(2026, 2, 10), 1000m, 210m), // T1
                Factura(new DateOnly(2026, 5, 10), 1000m, 210m), // T2
            ],
            []);

        var t2 = await caso.EjecutarAsync(Empresa, 2026, 2);
        var m = t2.Modelo130;

        m.IngresosAcumulados.Should().Be(2000m);
        m.RendimientoAcumulado.Should().Be(2000m);
        m.PagoFraccionadoBruto.Should().Be(400m); // 20% de 2000 acumulado
        m.PagosAnteriores.Should().Be(200m);       // el T1 ya ingresó 20% de 1000
        m.Resultado.Should().Be(200m);             // 400 - 0 - 200
    }

    [Fact]
    public async Task El_130_nunca_es_negativo()
    {
        var caso = Caso(
            [Factura(new DateOnly(2026, 2, 10), 100m, 21m, retencion: 1000m)],
            []);

        var resumen = await caso.EjecutarAsync(Empresa, 2026, 1);
        resumen.Modelo130.Resultado.Should().Be(0m);
    }

    [Fact]
    public async Task Las_facturas_anuladas_o_rectificadas_no_cuentan()
    {
        var caso = Caso(
            [
                Factura(new DateOnly(2026, 2, 10), 1000m, 210m, estado: "Emitida"),
                Factura(new DateOnly(2026, 2, 11), 500m, 105m, estado: "Rectificada"),
                Factura(new DateOnly(2026, 2, 12), 300m, 63m, estado: "Anulada"),
            ],
            []);

        var resumen = await caso.EjecutarAsync(Empresa, 2026, 1);
        resumen.Modelo303.IvaDevengadoBase.Should().Be(1000m);
        resumen.Modelo303.IvaDevengadoCuota.Should().Be(210m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Trimestre_fuera_de_rango_es_error(int trimestre)
    {
        var caso = Caso([], []);
        var accion = async () => await caso.EjecutarAsync(Empresa, 2026, trimestre);
        await accion.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}

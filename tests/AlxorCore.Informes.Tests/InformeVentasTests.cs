using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Informes.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Informes.Tests;

public class InformeVentasTests
{
    private static readonly Guid Empresa = Guid.NewGuid();

    private static FacturaResumen Ticket(DateOnly fecha, decimal total, string estado = "Emitida") =>
        new(Guid.NewGuid(), "T2026/000001", fecha, fecha, "Contado", null, total / 1.1m, total - total / 1.1m, 0m, total, estado, "Simplificada");

    private sealed class FakeFacturas(IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<LineaMargenDto> lineas) : IConsultaFacturas
    {
        public Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default) => Task.FromResult<FacturaDto?>(null);

        public Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default) => Task.FromResult(facturas);

        public Task<IReadOnlyList<LineaMargenDto>> ListarLineasMargenAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default) => Task.FromResult(lineas);
    }

    private static GenerarInformeVentas Caso(IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<LineaMargenDto> lineas) =>
        new(new FakeFacturas(facturas, lineas));

    [Fact]
    public async Task Calcula_tickets_venta_total_y_ticket_medio_ignorando_anuladas_y_fuera_de_rango()
    {
        var caso = Caso(
            [
                Ticket(new DateOnly(2026, 3, 2), 10m),   // lunes, dentro
                Ticket(new DateOnly(2026, 3, 6), 30m),   // viernes, dentro
                Ticket(new DateOnly(2026, 3, 6), 100m, "Anulada"), // anulada: se ignora
                Ticket(new DateOnly(2026, 2, 27), 999m), // fuera de rango
            ],
            []);

        var v = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        v.Tickets.Should().Be(2);
        v.VentaTotal.Should().Be(40m);
        v.TicketMedio.Should().Be(20m);
    }

    [Fact]
    public async Task Reparte_por_dia_de_la_semana_siempre_de_lunes_a_domingo()
    {
        var caso = Caso(
            [
                Ticket(new DateOnly(2026, 3, 6), 30m),   // viernes
                Ticket(new DateOnly(2026, 3, 6), 20m),   // viernes
                Ticket(new DateOnly(2026, 3, 2), 10m),   // lunes
            ],
            []);

        var v = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        v.PorDiaSemana.Should().HaveCount(7);
        v.PorDiaSemana[0].Nombre.Should().Be("Lunes");
        v.PorDiaSemana[6].Nombre.Should().Be("Domingo");
        v.PorDiaSemana.Single(d => d.Nombre == "Viernes").Importe.Should().Be(50m);
        v.PorDiaSemana.Single(d => d.Nombre == "Viernes").Tickets.Should().Be(2);
        v.PorDiaSemana.Single(d => d.Nombre == "Lunes").Importe.Should().Be(10m);
        v.PorDiaSemana.Single(d => d.Nombre == "Martes").Importe.Should().Be(0m);
    }

    [Fact]
    public async Task Ordena_el_top_de_productos_por_unidades_vendidas()
    {
        var cana = Guid.NewGuid();
        var tapa = Guid.NewGuid();
        var caso = Caso(
            [Ticket(new DateOnly(2026, 3, 2), 10m)],
            [
                new LineaMargenDto(cana, "Caña", 40, Ingreso: 60m, Coste: 20m),
                new LineaMargenDto(cana, "Caña", 10, Ingreso: 15m, Coste: 5m), // se acumula: 50 uds
                new LineaMargenDto(tapa, "Tapa", 12, Ingreso: 48m, Coste: 18m),
            ]);

        var v = await caso.EjecutarAsync(Empresa, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        v.TopProductos.Should().HaveCount(2);
        v.TopProductos[0].Descripcion.Should().Be("Caña");
        v.TopProductos[0].Unidades.Should().Be(50);
        v.TopProductos[0].Importe.Should().Be(75m);
        v.TopProductos[0].Margen.Should().Be(50m); // 75 - 25
        v.TopProductos[1].Descripcion.Should().Be("Tapa");
    }
}

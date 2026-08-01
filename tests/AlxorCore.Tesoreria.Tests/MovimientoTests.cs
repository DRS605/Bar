using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Tesoreria.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Tesoreria.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class MovimientoTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Fact]
    public void Crear_movimiento_valido()
    {
        var mov = Movimiento.Crear(Guid.NewGuid(), TipoDocumentoTesoreria.Factura, Guid.NewGuid(), SentidoMovimiento.Cobro, 100m, new DateOnly(2026, 1, 5), "transferencia", Reloj);
        mov.EsCorrecto.Should().BeTrue();
        mov.Valor.Importe.Should().Be(100m);
        mov.Valor.EventosDominio.Should().ContainSingle(e => e is MovimientoRegistrado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Crear_rechaza_importe_no_positivo(decimal importe)
    {
        Movimiento.Crear(Guid.NewGuid(), TipoDocumentoTesoreria.Gasto, Guid.NewGuid(), SentidoMovimiento.Pago, importe, new DateOnly(2026, 1, 5), null, Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(100, 0, EstadoSaldo.Pendiente)]
    [InlineData(100, 40, EstadoSaldo.Parcial)]
    [InlineData(100, 100, EstadoSaldo.Liquidado)]
    [InlineData(100, 120, EstadoSaldo.Liquidado)]
    public void DerivarEstado_del_saldo(decimal total, decimal liquidado, EstadoSaldo esperado)
    {
        Movimiento.DerivarEstado(total, liquidado).Should().Be(esperado);
    }
}

using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas de los movimientos de caja (fondo, entradas y salidas).</summary>
public sealed class CajaTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Fact]
    public void Fondo_y_entrada_suman_salida_resta()
    {
        MovimientoCaja.Crear(Guid.NewGuid(), TipoMovimientoCaja.FondoInicial, 100m, "cambio", null, null, Reloj)
            .Valor.ImporteConSigno.Should().Be(100m);
        MovimientoCaja.Crear(Guid.NewGuid(), TipoMovimientoCaja.Salida, 20m, "proveedor", null, null, Reloj)
            .Valor.ImporteConSigno.Should().Be(-20m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Importe_no_positivo_falla(decimal importe) =>
        MovimientoCaja.Crear(Guid.NewGuid(), TipoMovimientoCaja.Entrada, importe, null, null, null, Reloj)
            .Error.Codigo.Should().Be("caja.importe_invalido");

    [Fact]
    public void Crear_guarda_fecha_del_dia_y_concepto()
    {
        var m = MovimientoCaja.Crear(Guid.NewGuid(), TipoMovimientoCaja.Entrada, 10m, "  aporte  ", null, "Ana", Reloj).Valor;
        m.Concepto.Should().Be("aporte");
        m.UsuarioNombre.Should().Be("Ana");
        m.Fecha.Should().Be(DateOnly.FromDateTime(Reloj.AhoraUtc.UtcDateTime));
    }
}

using AlxorCore.Gastos.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Gastos.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class GastoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DateOnly Fecha = new(2026, 1, 20);

    [Fact]
    public void Registrar_calcula_iva_soportado_y_total()
    {
        var gasto = Gasto.Registrar(Empresa, "Proveedor SL", "Material de oficina", Fecha, 100m, "IVA21", 0m, Reloj).Valor;

        gasto.CuotaIva.Should().Be(21m);
        gasto.RetencionIrpf.Should().Be(0m);
        gasto.Total.Should().Be(121m);
        gasto.Estado.Should().Be(EstadoGasto.Registrado);
        gasto.EventosDominio.Should().ContainSingle(e => e is GastoRegistrado);
    }

    [Fact]
    public void Registrar_aplica_retencion_de_irpf()
    {
        // Servicio profesional: base 1000, IVA 21% = 210, IRPF 15% = 150 -> total 1060
        var gasto = Gasto.Registrar(Empresa, "Asesoría", "Servicios profesionales", Fecha, 1000m, "IVA21", 15m, Reloj).Valor;

        gasto.CuotaIva.Should().Be(210m);
        gasto.RetencionIrpf.Should().Be(150m);
        gasto.Total.Should().Be(1060m);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Registrar_rechaza_concepto_vacio(string? concepto)
    {
        Gasto.Registrar(Empresa, null, concepto, Fecha, 100m, "IVA21", 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Registrar_rechaza_base_negativa()
    {
        Gasto.Registrar(Empresa, null, "X", Fecha, -1m, "IVA21", 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Registrar_rechaza_iva_desconocido()
    {
        Gasto.Registrar(Empresa, null, "X", Fecha, 100m, "IVA99", 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Anular_cambia_el_estado()
    {
        var gasto = Gasto.Registrar(Empresa, null, "X", Fecha, 100m, "IVA21", 0m, Reloj).Valor;
        gasto.Anular(Reloj);
        gasto.Estado.Should().Be(EstadoGasto.Anulado);
    }
}

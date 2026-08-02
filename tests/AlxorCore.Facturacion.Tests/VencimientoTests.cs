using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public class VencimientoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly DateOnly Emision = new(2026, 1, 15);
    private static readonly ClienteFacturado Cliente =
        new(Guid.NewGuid(), "Cliente SL", "B12345674", "Calle 1", "28001", "Madrid", "Madrid", "ES");

    private static IReadOnlyList<NuevaLinea> Lineas() => [new NuevaLinea("Servicio", 1m, 100m, "IVA21", 21m)];

    [Fact]
    public void Sin_vencimiento_indicado_es_contado_igual_a_la_emision()
    {
        var f = Factura.Emitir(Guid.NewGuid(), new NumeroFactura("FA", 2026, 1), Emision, Emision, Cliente, Lineas(), 0m, Reloj).Valor;
        f.FechaVencimiento.Should().Be(Emision);
    }

    [Fact]
    public void Con_vencimiento_lo_conserva()
    {
        var venc = Emision.AddDays(30);
        var f = Factura.Emitir(Guid.NewGuid(), new NumeroFactura("FA", 2026, 1), Emision, Emision, Cliente, Lineas(), 0m, Reloj, venc).Valor;
        f.FechaVencimiento.Should().Be(venc);
    }

    [Fact]
    public void Rechaza_vencimiento_anterior_a_la_emision()
    {
        Factura.Emitir(Guid.NewGuid(), new NumeroFactura("FA", 2026, 1), Emision, Emision, Cliente, Lineas(), 0m, Reloj, Emision.AddDays(-1))
            .EsFallo.Should().BeTrue();
    }
}

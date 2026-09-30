using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

/// <summary>
/// Precios con IVA incluido (hostelería/TPV): el importe que paga el cliente ya lleva el impuesto y la
/// base se desglosa hacia atrás, cuadrando al céntimo.
/// </summary>
public class PrecioConIvaIncluidoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly DateOnly Fecha = new(2026, 1, 15);

    private static NuevaLinea Bruta(decimal cantidad, decimal precio, decimal iva = 10m) =>
        new("Artículo", cantidad, precio, "IVA10", iva, PrecioConIvaIncluido: true);

    [Theory]
    [InlineData(3.00, 10, 0, 2.73, 0.27, 3.00)]   // caña (2 × 1,50)
    [InlineData(4.50, 10, 0, 4.09, 0.41, 4.50)]   // tortilla
    [InlineData(1.00, 21, 0, 0.83, 0.17, 1.00)]   // 21 %
    public void Desglose_desde_bruto_cuadra_al_centimo(
        double bruto, double iva, double recargo, double baseEsp, double ivaEsp, double totalEsp)
    {
        var (b, cuota, rec) = DesgloseIva.DesdeBruto((decimal)bruto, (decimal)iva, (decimal)recargo);

        b.Should().Be((decimal)baseEsp);
        cuota.Should().Be((decimal)ivaEsp);
        (b + cuota + rec).Should().Be((decimal)totalEsp); // siempre cuadra con el importe con IVA incluido
    }

    [Fact]
    public void Linea_con_iva_incluido_desglosa_la_base_y_conserva_el_total()
    {
        var t = Factura.EmitirSimplificada(Guid.NewGuid(), new NumeroFactura("T", 2026, 1), Fecha,
            ClienteFacturado.Contado, [Bruta(2m, 1.50m)], Reloj).Valor;

        t.BaseImponible.Should().Be(2.73m);
        t.CuotaIva.Should().Be(0.27m);
        t.Total.Should().Be(3.00m); // el cliente paga exactamente 2 × 1,50
    }

    [Fact]
    public void Sin_la_marca_el_precio_es_base_y_el_iva_se_suma_encima()
    {
        // Contraste: la factura B2B ordinaria sigue tratando el precio como base (IVA aparte).
        var f = Factura.EmitirSimplificada(Guid.NewGuid(), new NumeroFactura("T", 2026, 1), Fecha,
            ClienteFacturado.Contado, [new("Artículo", 2m, 1.50m, "IVA10", 10m)], Reloj).Valor;

        f.BaseImponible.Should().Be(3.00m);
        f.CuotaIva.Should().Be(0.30m);
        f.Total.Should().Be(3.30m);
    }
}

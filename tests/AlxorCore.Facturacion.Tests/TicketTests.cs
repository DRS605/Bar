using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public class TicketTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly DateOnly Fecha = new(2026, 1, 15);

    private static NumeroFactura Numero() => new("T", 2026, 1);
    private static NuevaLinea Linea(decimal cantidad, decimal precio, decimal iva = 21m) =>
        new("Artículo", cantidad, precio, "IVA21", iva);

    [Fact]
    public void EmitirSimplificada_sin_cliente_usa_contado_y_marca_tipo()
    {
        var t = Factura.EmitirSimplificada(Guid.NewGuid(), Numero(), Fecha, ClienteFacturado.Contado,
            [Linea(2m, 10m)], Reloj);

        t.EsCorrecto.Should().BeTrue();
        t.Valor.TipoFactura.Should().Be(TipoFactura.Simplificada);
        t.Valor.ClienteId.Should().BeNull();
        t.Valor.ClienteNombre.Should().Be("Cliente de contado");
        t.Valor.NumeroCompleto.Should().Be("T2026/000001");
    }

    [Fact]
    public void EmitirSimplificada_calcula_base_iva_y_total_sin_irpf()
    {
        var t = Factura.EmitirSimplificada(Guid.NewGuid(), Numero(), Fecha, ClienteFacturado.Contado,
            [Linea(2m, 10m)], Reloj).Valor;

        t.BaseImponible.Should().Be(20m);
        t.CuotaIva.Should().Be(4.20m);
        t.RetencionIrpf.Should().Be(0m);
        t.Total.Should().Be(24.20m);
    }

    [Fact]
    public void EmitirSimplificada_rechaza_importe_por_encima_del_tope()
    {
        // 3.000 € de base + IVA supera con creces el tope de 3.000 € de total.
        var t = Factura.EmitirSimplificada(Guid.NewGuid(), Numero(), Fecha, ClienteFacturado.Contado,
            [Linea(1m, 3000m)], Reloj);

        t.EsFallo.Should().BeTrue();
        t.Error.Codigo.Should().Be("ticket.importe_excedido");
    }

    [Fact]
    public void EmitirSimplificada_rechaza_sin_lineas()
    {
        Factura.EmitirSimplificada(Guid.NewGuid(), Numero(), Fecha, ClienteFacturado.Contado, [], Reloj)
            .EsFallo.Should().BeTrue();
    }
}

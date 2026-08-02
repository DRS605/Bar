using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public class RectificativaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly DateOnly Fecha = new(2026, 1, 15);
    private static readonly ClienteFacturado Cliente =
        new(Guid.NewGuid(), "Cliente SL", "B12345674", "Calle 1", "28001", "Madrid", "Madrid", "ES");

    private static IReadOnlyList<NuevaLinea> Lineas(decimal precio = 100m) =>
        [new NuevaLinea("Servicio corregido", 1m, precio, "IVA21", 21m)];

    private static Factura Original() =>
        Factura.Emitir(Guid.NewGuid(), new NumeroFactura("FA", 2026, 1), Fecha, Fecha, Cliente, Lineas(), 0m, Reloj).Valor;

    [Fact]
    public void EmitirRectificativa_referencia_la_original_y_marca_tipo_R1()
    {
        var original = Original();
        var r = Factura.EmitirRectificativa(Guid.NewGuid(), new NumeroFactura("R", 2026, 1), Fecha, Cliente,
            Lineas(80m), 0m, original.Id, "Error en el importe", Reloj);

        r.EsCorrecto.Should().BeTrue();
        r.Valor.TipoFactura.Should().Be(TipoFactura.Rectificativa);
        Verifactu.TipoCodigo(r.Valor.TipoFactura).Should().Be("R1");
        r.Valor.RectificaFacturaId.Should().Be(original.Id);
        r.Valor.MotivoRectificacion.Should().Be("Error en el importe");
        r.Valor.Total.Should().Be(96.80m);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmitirRectificativa_exige_motivo(string? motivo)
    {
        Factura.EmitirRectificativa(Guid.NewGuid(), new NumeroFactura("R", 2026, 1), Fecha, Cliente,
            Lineas(), 0m, Guid.NewGuid(), motivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void MarcarRectificada_solo_una_vez()
    {
        var original = Original();
        original.MarcarRectificada().EsCorrecto.Should().BeTrue();
        original.Estado.Should().Be(EstadoFactura.Rectificada);
        // Ya rectificada: no se puede volver a rectificar.
        original.MarcarRectificada().EsFallo.Should().BeTrue();
    }
}

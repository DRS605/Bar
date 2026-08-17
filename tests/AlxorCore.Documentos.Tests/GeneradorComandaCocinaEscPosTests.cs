using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura.Impresion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Documentos.Tests;

public class GeneradorComandaCocinaEscPosTests
{
    private static readonly Encoding Cp858 = CrearCp858();

    private static Encoding CrearCp858()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858);
    }

    private static DatosComandaCocina Datos() => new(
        "Mesa 4", new DateTimeOffset(2026, 2, 14, 21, 0, 0, TimeSpan.Zero),
        new List<LineaCocina> { new(2m, "Caña"), new(1m, "Tortilla") }, "sin cebolla");

    [Fact]
    public void Lista_articulos_con_cantidad_y_sin_precios()
    {
        var bytes = new GeneradorComandaCocinaEscPos().Generar(Datos());

        bytes.Take(2).Should().Equal(new byte[] { 0x1B, 0x40 });                   // ESC @ (inicializa)
        bytes.TakeLast(4).Should().Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 });   // GS V B (corta)

        var texto = Cp858.GetString(bytes);
        texto.Should().Contain("Mesa 4");
        texto.Should().Contain("2 x Caña").And.Contain("1 x Tortilla");
        texto.Should().Contain("sin cebolla");
        texto.Should().NotContain("€"); // la comanda de cocina no lleva precios
    }
}

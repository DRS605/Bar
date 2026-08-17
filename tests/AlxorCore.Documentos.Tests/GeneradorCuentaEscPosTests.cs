using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura.Impresion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Documentos.Tests;

public class GeneradorCuentaEscPosTests
{
    private static readonly Encoding Cp858 = CrearCp858();

    private static Encoding CrearCp858()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858);
    }

    private static DatosCuenta Datos() => new(
        "Bar Sol de Levante", "Mesa 4", new DateTimeOffset(2026, 2, 14, 21, 0, 0, TimeSpan.Zero),
        new List<LineaCuenta> { new(2m, "Caña", 1.50m, 3.30m), new(1m, "Tortilla", 4.50m, 4.95m) },
        7.50m, 0.75m, 8.25m, "sin gluten");

    [Fact]
    public void Genera_cuenta_con_importes_total_y_aviso_de_no_fiscal()
    {
        var bytes = new GeneradorCuentaEscPos().Generar(Datos());

        bytes.Take(2).Should().Equal(new byte[] { 0x1B, 0x40 });                   // ESC @ (inicializa)
        BytesContienen(bytes, new byte[] { 0x1B, 0x74, 19 }).Should().BeTrue();     // ESC t 19 (CP858)
        bytes.TakeLast(4).Should().Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 });    // GS V B (corta)

        var texto = Cp858.GetString(bytes);
        texto.Should().Contain("Bar Sol de Levante");
        texto.Should().Contain("CUENTA");
        texto.Should().Contain("Mesa 4");
        texto.Should().Contain("Caña").And.Contain("Tortilla");
        texto.Should().Contain("€");                 // la cuenta sí lleva importes
        texto.Should().Contain("TOTAL");
        texto.Should().Contain("sin gluten");
        texto.Should().Contain("Documento sin valor fiscal");
        texto.Should().Contain("No es una factura");
    }

    private static bool BytesContienen(byte[] fuente, byte[] patron)
    {
        for (var i = 0; i + patron.Length <= fuente.Length; i++)
        {
            var coincide = true;
            for (var j = 0; j < patron.Length; j++)
            {
                if (fuente[i + j] != patron[j]) { coincide = false; break; }
            }

            if (coincide) return true;
        }

        return false;
    }
}

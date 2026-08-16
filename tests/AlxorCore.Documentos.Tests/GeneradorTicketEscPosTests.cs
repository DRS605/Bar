using System.Text;
using AlxorCore.Documentos.Infraestructura.Impresion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Documentos.Tests;

public class GeneradorTicketEscPosTests
{
    private static readonly Encoding Cp858 = CrearCp858();

    private static Encoding CrearCp858()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858);
    }

    private static EmpresaDto Empresa() =>
        new(Guid.NewGuid(), "12345678Z", "Bar Pepe", RegimenIva.General, "EUR", "ES", null, null);

    private static FacturaDto Factura() => new(
        Guid.NewGuid(), "T2026/000007", new DateOnly(2026, 2, 14), new DateOnly(2026, 2, 14), new DateOnly(2026, 2, 14),
        null, "Cliente contado", null, 7.00m, 0.70m, 0m, 0m, false, 0m, 7.70m, "Emitida", "Simplificada", null, null, null, null, null,
        new List<LineaFacturaDto>
        {
            new("Caña", 3m, 1.50m, 0m, "IVA10", 10m, 4.50m, 0.45m, 0m, 0m, 0m, 0m),
            new("Croquetas", 1m, 4.00m, 0m, "IVA10", 10m, 4.00m, 0.40m, 0m, 0m, 0m, 0m),
        });

    private readonly byte[] _ticket = new GeneradorTicketEscPos().Generar(Factura(), Empresa());

    [Fact]
    public void Empieza_inicializando_la_impresora_y_selecciona_pagina_de_codigos()
    {
        _ticket.Take(2).Should().Equal(new byte[] { 0x1B, 0x40 });          // ESC @
        BytesContienen(_ticket, new byte[] { 0x1B, 0x74, 19 }).Should().BeTrue(); // ESC t 19 (CP858)
    }

    [Fact]
    public void Termina_cortando_el_papel()
    {
        _ticket.TakeLast(4).Should().Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // GS V B
    }

    [Fact]
    public void Incluye_local_numero_lineas_y_total()
    {
        var texto = Cp858.GetString(_ticket);
        texto.Should().Contain("Bar Pepe");
        texto.Should().Contain("12345678Z");
        texto.Should().Contain("T2026/000007");
        texto.Should().Contain("Caña").And.Contain("Croquetas");
        texto.Should().Contain("TOTAL");
        texto.Should().Contain("7,70 €");   // símbolo del euro correcto en CP858
        texto.Should().Contain("Comandia");
    }

    private static bool BytesContienen(byte[] fuente, byte[] patron)
    {
        for (var i = 0; i + patron.Length <= fuente.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < patron.Length; j++)
            {
                if (fuente[i + j] != patron[j]) { ok = false; break; }
            }

            if (ok) return true;
        }

        return false;
    }
}

using AlxorCore.Tesoreria.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Tesoreria.Tests;

public class Norma43Tests
{
    // Construye un registro de 80 caracteres colocando campos en posiciones (0-based).
    private static string Registro(string codigo, params (int Inicio, string Valor)[] campos)
    {
        var buf = new char[80];
        Array.Fill(buf, ' ');
        for (var i = 0; i < codigo.Length; i++)
        {
            buf[i] = codigo[i];
        }

        foreach (var (inicio, valor) in campos)
        {
            for (var i = 0; i < valor.Length && inicio + i < 80; i++)
            {
                buf[inicio + i] = valor[i];
            }
        }

        return new string(buf);
    }

    private static string ImporteN43(decimal valor) => ((long)(valor * 100)).ToString("D14", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Parsea_cabecera_apuntes_y_saldos()
    {
        var cabecera = Registro("11",
            (2, "0049"), (6, "1500"), (10, "1234567890"),
            (20, "260101"), (26, "260131"),
            (32, "2"), (33, ImporteN43(1000m)));
        // Abono de 121,00 (haber = 2)
        var abono = Registro("22", (10, "260115"), (16, "260115"), (22, "03"), (27, "2"), (28, ImporteN43(121m)));
        var abonoConcepto = Registro("23", (2, "01"), (4, "TRANSFERENCIA CLIENTE SL"));
        // Cargo de 60,50 (debe = 1)
        var cargo = Registro("22", (10, "260116"), (16, "260116"), (22, "12"), (27, "1"), (28, ImporteN43(60.50m)));
        var footer = Registro("33", (58, "2"), (59, ImporteN43(1060.50m)));
        var fin = Registro("88");

        var contenido = string.Join("\r\n", cabecera, abono, abonoConcepto, cargo, footer, fin);

        var r = ParserNorma43.Parsear(contenido);

        r.EsCorrecto.Should().BeTrue();
        var e = r.Valor;
        e.Cuenta.Should().Be("004915001234567890");
        e.Desde.Should().Be(new DateOnly(2026, 1, 1));
        e.Hasta.Should().Be(new DateOnly(2026, 1, 31));
        e.SaldoInicial.Should().Be(1000m);
        e.SaldoFinal.Should().Be(1060.50m);
        e.Apuntes.Should().HaveCount(2);

        e.Apuntes[0].Importe.Should().Be(121m);       // haber = positivo
        e.Apuntes[0].Fecha.Should().Be(new DateOnly(2026, 1, 15));
        e.Apuntes[0].Concepto.Should().Contain("TRANSFERENCIA CLIENTE SL");
        e.Apuntes[1].Importe.Should().Be(-60.50m);    // debe = negativo
    }

    [Fact]
    public void Fichero_vacio_o_sin_apuntes_devuelve_error()
    {
        ParserNorma43.Parsear("").EsFallo.Should().BeTrue();
        ParserNorma43.Parsear("texto que no es norma 43").EsFallo.Should().BeTrue();
    }
}

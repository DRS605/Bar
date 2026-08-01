using AlxorCore.Informes.Aplicacion;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Informes.Tests;

public class LibroIvaCsvTests
{
    [Fact]
    public void Generar_incluye_cabecera_asientos_y_totales()
    {
        var libro = new LibroIvaDto(
            TipoLibroIva.Repercutido,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            [
                new AsientoIva(new DateOnly(2026, 1, 15), "FA2026/000001", "Cliente SL", "B12345674", 200m, 42m),
            ],
            200m,
            42m);

        var csv = ExportadorLibroIvaCsv.Generar(libro);

        csv.Should().Contain("Fecha;Documento;Tercero;NIF;Base;Cuota IVA");
        csv.Should().Contain("15/01/2026;FA2026/000001;Cliente SL;B12345674;200,00;42,00");
        csv.Should().Contain("TOTALES;;;;200,00;42,00");
    }

    [Fact]
    public void Generar_escapa_valores_con_punto_y_coma()
    {
        var libro = new LibroIvaDto(
            TipoLibroIva.Soportado,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            [new AsientoIva(new DateOnly(2026, 2, 1), "Compra; urgente", "Proveedor", null, 100m, 21m)],
            100m,
            21m);

        var csv = ExportadorLibroIvaCsv.Generar(libro);

        csv.Should().Contain("\"Compra; urgente\"");
    }
}

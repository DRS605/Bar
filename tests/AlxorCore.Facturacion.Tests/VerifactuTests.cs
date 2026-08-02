using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public class VerifactuTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly DateOnly Fecha = new(2026, 1, 15);
    private static readonly ClienteFacturado Cliente =
        new(Guid.NewGuid(), "Cliente SL", "B12345674", "Calle 1", "28001", "Madrid", "Madrid", "ES");

    private static Factura Emitir(string prefijo = "FA", long n = 1) =>
        Factura.Emitir(Guid.NewGuid(), new NumeroFactura(prefijo, 2026, n), Fecha, Fecha, Cliente,
            [new NuevaLinea("Servicio", 1m, 100m, "IVA21", 21m)], 0m, Reloj).Valor;

    [Theory]
    [InlineData(TipoFactura.Ordinaria, "F1")]
    [InlineData(TipoFactura.Simplificada, "F2")]
    [InlineData(TipoFactura.Rectificativa, "R1")]
    public void TipoCodigo_mapea_los_tipos_verifactu(TipoFactura tipo, string esperado)
    {
        Verifactu.TipoCodigo(tipo).Should().Be(esperado);
    }

    [Fact]
    public void CalcularHuella_es_determinista_y_hex_de_64_caracteres()
    {
        var ahora = Reloj.AhoraUtc;
        var h1 = Verifactu.CalcularHuella("B44531218", "FA2026/000001", Fecha, "F1", 21m, 121m, null, ahora);
        var h2 = Verifactu.CalcularHuella("B44531218", "FA2026/000001", Fecha, "F1", 21m, 121m, null, ahora);

        h1.Should().Be(h2);
        h1.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void CalcularHuella_cambia_con_la_huella_anterior()
    {
        var ahora = Reloj.AhoraUtc;
        var sinCadena = Verifactu.CalcularHuella("B44531218", "FA2026/000002", Fecha, "F1", 21m, 121m, null, ahora);
        var conCadena = Verifactu.CalcularHuella("B44531218", "FA2026/000002", Fecha, "F1", 21m, 121m, "ABC123", ahora);

        conCadena.Should().NotBe(sinCadena);
    }

    [Fact]
    public void RegistrarVerifactu_calcula_la_huella_y_encadena()
    {
        var primera = Emitir("FA", 1);
        primera.RegistrarVerifactu("B44531218", null, Reloj.AhoraUtc);

        var segunda = Emitir("FA", 2);
        segunda.RegistrarVerifactu("B44531218", primera.Huella, Reloj.AhoraUtc);

        primera.Huella.Should().NotBeNullOrEmpty();
        primera.HuellaAnterior.Should().BeNull();
        primera.EstadoEnvioAeat.Should().Be("Registrado");
        segunda.HuellaAnterior.Should().Be(primera.Huella);
        segunda.Huella.Should().NotBe(primera.Huella);
    }

    [Fact]
    public void UrlCotejo_incluye_nif_numero_fecha_e_importe()
    {
        var url = Verifactu.UrlCotejo("B44531218", "FA2026/000001", Fecha, 121m);
        url.Should().StartWith(Verifactu.BaseUrlCotejo);
        url.Should().Contain("nif=B44531218").And.Contain("numserie=FA2026").And.Contain("importe=121.00");
    }
}

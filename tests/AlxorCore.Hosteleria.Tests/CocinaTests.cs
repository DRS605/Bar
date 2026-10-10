using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas de las zonas de preparación y el estado «servido» de la pantalla de cocina.</summary>
public sealed class CocinaTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Theory]
    [InlineData("Cocina", ZonaPreparacion.Cocina)]
    [InlineData("barra", ZonaPreparacion.Barra)]
    [InlineData(null, ZonaPreparacion.Cocina)]
    [InlineData("otra", ZonaPreparacion.Cocina)]
    public void Normalizar_zona(string? texto, ZonaPreparacion esperada) =>
        ZonasPreparacion.Normalizar(texto).Should().Be(esperada);

    [Fact]
    public void Servir_marca_la_linea_enviada_como_servida()
    {
        var c = Comanda.Abrir(Guid.NewGuid(), Guid.NewGuid(), null, null, null, Reloj);
        c.AgregarLinea(Guid.NewGuid(), "Caña", 2m, 2m, "IVA10", 10m, Reloj);
        c.EnviarACocina();
        var linea = c.Lineas[0];

        linea.CantidadPendienteServir.Should().Be(2m);

        c.ServirLinea(linea.Id).EsCorrecto.Should().BeTrue();
        linea.CantidadPendienteServir.Should().Be(0m);
    }

    [Fact]
    public void Servir_linea_inexistente_falla()
    {
        var c = Comanda.Abrir(Guid.NewGuid(), Guid.NewGuid(), null, null, null, Reloj);
        c.ServirLinea(Guid.NewGuid()).Error!.Codigo.Should().Be("comanda.linea_no_encontrada");
    }
}

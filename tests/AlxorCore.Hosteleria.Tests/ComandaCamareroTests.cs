using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas de la atribución de la comanda a un camarero.</summary>
public sealed class ComandaCamareroTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Fact]
    public void Abrir_guarda_el_camarero()
    {
        var usuario = Guid.NewGuid();
        var comanda = Comanda.Abrir(Guid.NewGuid(), Guid.NewGuid(), null, usuario, "Ana", Reloj);

        comanda.UsuarioId.Should().Be(usuario);
        comanda.UsuarioNombre.Should().Be("Ana");
    }

    [Fact]
    public void Abrir_sin_camarero_deja_los_campos_nulos()
    {
        var comanda = Comanda.Abrir(Guid.NewGuid(), Guid.NewGuid(), null, null, null, Reloj);

        comanda.UsuarioId.Should().BeNull();
        comanda.UsuarioNombre.Should().BeNull();
    }
}

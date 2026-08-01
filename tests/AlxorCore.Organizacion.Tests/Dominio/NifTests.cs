using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Organizacion.Tests.Dominio;

public class NifTests
{
    [Theory]
    [InlineData("12345678Z")]      // DNI válido
    [InlineData("  12345678-z ")]  // se normaliza (mayúsculas, sin espacios ni guiones)
    [InlineData("X1234567L")]      // NIE válido
    [InlineData("B12345674")]      // CIF válido (control numérico)
    public void Crear_acepta_identificadores_validos(string entrada)
    {
        Nif.Crear(entrada).EsCorrecto.Should().BeTrue($"«{entrada}» debería ser válido");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345678A")]  // letra de control incorrecta
    [InlineData("X1234567A")]  // NIE con control incorrecto
    [InlineData("1234")]        // formato incorrecto
    [InlineData("ABCDEFGHI")]   // no es un NIF
    public void Crear_rechaza_identificadores_invalidos(string? entrada)
    {
        Nif.Crear(entrada).EsFallo.Should().BeTrue($"«{entrada}» debería ser inválido");
    }

    [Fact]
    public void Crear_normaliza_a_mayusculas()
    {
        Nif.Crear("x1234567l").Valor.Valor.Should().Be("X1234567L");
    }
}

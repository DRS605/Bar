using AlxorCore.Identidad.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Identidad.Tests.Dominio;

public class EmailTests
{
    [Theory]
    [InlineData("  Ana@Ejemplo.COM  ", "ana@ejemplo.com")]
    [InlineData("cliente@dominio.es", "cliente@dominio.es")]
    public void Crear_normaliza_correo_valido(string entrada, string esperado)
    {
        var resultado = Email.Crear(entrada);

        resultado.EsCorrecto.Should().BeTrue();
        resultado.Valor.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("sin-arroba")]
    [InlineData("dos@@arrobas.com")]
    [InlineData("sin@dominio")]
    [InlineData("@ejemplo.com")]
    [InlineData("con espacio@ejemplo.com")]
    public void Crear_rechaza_correo_invalido(string? entrada)
    {
        var resultado = Email.Crear(entrada);

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Tipo.Should().Be(Nucleo.Resultados.TipoError.Validacion);
    }

    [Fact]
    public void Dos_emails_con_mismo_valor_son_iguales()
    {
        var a = Email.Crear("igual@ejemplo.com").Valor;
        var b = Email.Crear("IGUAL@ejemplo.com").Valor;

        a.Should().Be(b);
    }
}

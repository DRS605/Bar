using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Organizacion.Tests.Dominio;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class SerieNumeracionTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Fact]
    public void AsignarSiguiente_es_correlativo_y_avanza_el_contador()
    {
        var serie = SerieNumeracion.Crear(Guid.NewGuid(), TipoDocumento.Factura, 2026, "FA", Reloj).Valor;

        var primero = serie.AsignarSiguiente();
        var segundo = serie.AsignarSiguiente();

        primero.Numero.Should().Be(1);
        segundo.Numero.Should().Be(2);
        serie.SiguienteNumero.Should().Be(3);
    }

    [Fact]
    public void NumeroDocumento_se_formatea_con_prefijo_ejercicio_y_correlativo()
    {
        var serie = SerieNumeracion.Crear(Guid.NewGuid(), TipoDocumento.Factura, 2026, "FA", Reloj).Valor;

        serie.AsignarSiguiente().Formateado.Should().Be("FA2026/000001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_rechaza_prefijo_vacio(string prefijo)
    {
        SerieNumeracion.Crear(Guid.NewGuid(), TipoDocumento.Factura, 2026, prefijo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_ejercicio_fuera_de_rango()
    {
        SerieNumeracion.Crear(Guid.NewGuid(), TipoDocumento.Factura, 1999, "FA", Reloj).EsFallo.Should().BeTrue();
    }
}

public class EmpresaYMembresiaTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    private static Nif UnNif() => Nif.Crear("B12345674").Valor;

    [Fact]
    public void Crear_empresa_valida_emite_evento()
    {
        var empresa = Empresa.Crear(UnNif(), "Mi Empresa SL", Direccion.Vacia, RegimenIva.General, Reloj);

        empresa.EsCorrecto.Should().BeTrue();
        empresa.Valor.Moneda.Should().Be("EUR");
        empresa.Valor.EventosDominio.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_empresa_rechaza_razon_social_vacia(string razon)
    {
        Empresa.Crear(UnNif(), razon, Direccion.Vacia, RegimenIva.General, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Membresia_propietario_tiene_rol_propietario_y_esta_activa()
    {
        var membresia = Membresia.CrearPropietario(Guid.NewGuid(), Guid.NewGuid(), Reloj);

        membresia.RolCodigo.Should().Be("propietario");
        membresia.EstaActiva.Should().BeTrue();

        membresia.Revocar();
        membresia.EstaActiva.Should().BeFalse();
    }
}

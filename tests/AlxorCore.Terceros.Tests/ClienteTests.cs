using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Terceros.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class ClienteTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_cliente_valido_emite_evento_y_queda_activo()
    {
        var cliente = Cliente.Crear(Empresa, "Cliente SL", "B12345674", "info@cliente.es", Direccion.Vacia, 15m, Reloj);

        cliente.EsCorrecto.Should().BeTrue();
        cliente.Valor.Activo.Should().BeTrue();
        cliente.Valor.PorcentajeIrpfDefecto.Should().Be(15m);
        cliente.Valor.EmpresaId.Should().Be(Empresa);
        cliente.Valor.EventosDominio.Should().ContainSingle(e => e is ClienteCreado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Cliente.Crear(Empresa, nombre, null, null, Direccion.Vacia, 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void Crear_rechaza_irpf_fuera_de_rango(decimal irpf)
    {
        Cliente.Crear(Empresa, "Cliente", null, null, Direccion.Vacia, irpf, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var cliente = Cliente.Crear(Empresa, "Antiguo", null, null, Direccion.Vacia, 0m, Reloj).Valor;

        var r = cliente.Actualizar("Nuevo Nombre", "12345678Z", "nuevo@x.es", Direccion.Crear("Calle 1", "28001", "Madrid", "Madrid"), 7m, Reloj);

        r.EsCorrecto.Should().BeTrue();
        cliente.Nombre.Should().Be("Nuevo Nombre");
        cliente.PorcentajeIrpfDefecto.Should().Be(7m);
        cliente.Direccion.Poblacion.Should().Be("Madrid");
    }

    [Fact]
    public void Desactivar_marca_inactivo()
    {
        var cliente = Cliente.Crear(Empresa, "Cliente", null, null, Direccion.Vacia, 0m, Reloj).Valor;
        cliente.Desactivar(Reloj);
        cliente.Activo.Should().BeFalse();
    }
}

using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Terceros.Tests;

public class ProveedorTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_proveedor_valido_queda_activo_y_emite_evento()
    {
        var p = Proveedor.Crear(Empresa, "Suministros Turia SL", "B12345674", "info@turia.es", Direccion.Vacia, 0m, Reloj);

        p.EsCorrecto.Should().BeTrue();
        p.Valor.Activo.Should().BeTrue();
        p.Valor.EmpresaId.Should().Be(Empresa);
        p.Valor.EventosDominio.Should().ContainSingle(e => e is ProveedorCreado);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Proveedor.Crear(Empresa, nombre, null, null, Direccion.Vacia, 0m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_cambia_los_datos()
    {
        var p = Proveedor.Crear(Empresa, "Antiguo", null, null, Direccion.Vacia, 0m, Reloj).Valor;
        p.Actualizar("Gestoría Bellver", "12345678Z", null, Direccion.Vacia, 15m, Reloj).EsCorrecto.Should().BeTrue();
        p.Nombre.Should().Be("Gestoría Bellver");
        p.PorcentajeIrpfDefecto.Should().Be(15m);
    }
}

using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Catalogo.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class ImpuestoTests
{
    [Theory]
    [InlineData("IVA21", 21)]
    [InlineData("IVA10", 10)]
    [InlineData("IVA4", 4)]
    [InlineData("IVA0", 0)]
    public void PorCodigo_resuelve_los_tipos_de_iva(string codigo, decimal porcentaje)
    {
        var impuesto = Impuesto.PorCodigoImpuesto(codigo);
        impuesto.EsCorrecto.Should().BeTrue();
        impuesto.Valor.Porcentaje.Should().Be(porcentaje);
    }

    [Fact]
    public void PorCodigo_falla_con_codigo_desconocido()
    {
        Impuesto.PorCodigoImpuesto("IVA99").EsFallo.Should().BeTrue();
    }
}

public class ProductoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_producto_valido_usa_iva_general_por_defecto()
    {
        var producto = Producto.Crear(Empresa, "REF1", "Servicio de consultoría", TipoProducto.Servicio, 100m, null, null, Reloj);

        producto.EsCorrecto.Should().BeTrue();
        producto.Valor.CodigoIva.Should().Be("IVA21");
        producto.Valor.Unidad.Should().Be("ud");
        producto.Valor.EventosDominio.Should().ContainSingle(e => e is ProductoCreado);
    }

    [Fact]
    public void Crear_producto_con_iva_reducido()
    {
        var producto = Producto.Crear(Empresa, null, "Libro", TipoProducto.Bien, 20m, "IVA4", "ud", Reloj);
        producto.Valor.CodigoIva.Should().Be("IVA4");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Producto.Crear(Empresa, null, nombre, TipoProducto.Servicio, 10m, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_precio_negativo()
    {
        Producto.Crear(Empresa, null, "X", TipoProducto.Servicio, -1m, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_iva_desconocido()
    {
        Producto.Crear(Empresa, null, "X", TipoProducto.Servicio, 10m, "IVA99", null, Reloj).EsFallo.Should().BeTrue();
    }
}

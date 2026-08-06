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

    [Theory]
    [InlineData(21, 5.2)]
    [InlineData(10, 1.4)]
    [InlineData(4, 0.5)]
    [InlineData(0, 0)]
    public void Recargo_de_equivalencia_por_tipo_de_iva(decimal iva, decimal recargo)
    {
        Impuesto.RecargoEquivalencia(iva).Should().Be(recargo);
    }
}

public class ProductoTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_producto_valido_usa_iva_general_por_defecto()
    {
        var producto = Producto.Crear(Empresa, "REF1", "Servicio de consultoría", TipoProducto.Servicio, 100m, 40m, null, null, Reloj);

        producto.EsCorrecto.Should().BeTrue();
        producto.Valor.CodigoIva.Should().Be("IVA21");
        producto.Valor.Unidad.Should().Be("ud");
        producto.Valor.PrecioCompra.Should().Be(40m);
        producto.Valor.EventosDominio.Should().ContainSingle(e => e is ProductoCreado);
    }

    [Fact]
    public void Crear_producto_con_iva_reducido()
    {
        var producto = Producto.Crear(Empresa, null, "Libro", TipoProducto.Bien, 20m, 0m, "IVA4", "ud", Reloj);
        producto.Valor.CodigoIva.Should().Be("IVA4");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        Producto.Crear(Empresa, null, nombre, TipoProducto.Servicio, 10m, 0m, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_precio_negativo()
    {
        Producto.Crear(Empresa, null, "X", TipoProducto.Servicio, -1m, 0m, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_precio_compra_negativo()
    {
        Producto.Crear(Empresa, null, "X", TipoProducto.Servicio, 10m, -5m, null, null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_iva_desconocido()
    {
        Producto.Crear(Empresa, null, "X", TipoProducto.Servicio, 10m, 0m, "IVA99", null, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Producto_sin_control_de_stock_no_admite_movimientos()
    {
        var producto = Producto.Crear(Empresa, null, "Servicio", TipoProducto.Servicio, 10m, 0m, null, null, Reloj).Valor;
        var mov = producto.RegistrarMovimientoStock(TipoMovimientoStock.Entrada, 5m, null, Reloj);
        mov.EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Entrada_y_salida_de_stock_actualizan_las_existencias()
    {
        var producto = Producto.Crear(Empresa, null, "Café 1kg", TipoProducto.Bien, 10m, 6m, null, null, Reloj, controlarStock: true, stockInicial: 20m).Valor;
        producto.Stock.Should().Be(20m);

        var entrada = producto.RegistrarMovimientoStock(TipoMovimientoStock.Entrada, 30m, "Compra", Reloj);
        entrada.EsCorrecto.Should().BeTrue();
        entrada.Valor.Cantidad.Should().Be(30m);
        producto.Stock.Should().Be(50m);

        var venta = producto.RegistrarMovimientoStock(TipoMovimientoStock.Venta, 12m, null, Reloj);
        venta.Valor.Cantidad.Should().Be(-12m);
        producto.Stock.Should().Be(38m);
    }

    [Fact]
    public void Ajuste_fija_el_stock_al_valor_contado()
    {
        var producto = Producto.Crear(Empresa, null, "Harina", TipoProducto.Bien, 2m, 1m, null, null, Reloj, controlarStock: true, stockInicial: 100m).Valor;
        var ajuste = producto.RegistrarMovimientoStock(TipoMovimientoStock.Ajuste, 90m, "Recuento", Reloj);
        ajuste.Valor.Cantidad.Should().Be(-10m);       // delta aplicado
        ajuste.Valor.StockResultante.Should().Be(90m);
        producto.Stock.Should().Be(90m);
    }
}

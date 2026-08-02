using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Facturacion.Tests;

public class FacturaRecurrenteTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private static IReadOnlyList<LineaPlantilla> UnaLinea(decimal cantidad = 1m, decimal precio = 90m) =>
        [new LineaPlantilla("Mantenimiento web mensual", cantidad, precio, "IVA21", 21m)];

    private static FacturaRecurrente Crear(Periodicidad periodicidad = Periodicidad.Mensual, DateOnly? fin = null, decimal irpf = 0m) =>
        FacturaRecurrente.Crear(Empresa, "Cuota mantenimiento", ClienteId, periodicidad, Inicio, fin, irpf, UnaLinea(), Reloj).Valor;

    [Fact]
    public void Crear_valida_queda_activa_con_proxima_emision_y_evento()
    {
        var r = FacturaRecurrente.Crear(Empresa, "Cuota mantenimiento", ClienteId, Periodicidad.Mensual, Inicio, null, 0m, UnaLinea(), Reloj);

        r.EsCorrecto.Should().BeTrue();
        r.Valor.Activa.Should().BeTrue();
        r.Valor.ProximaEmision.Should().Be(Inicio);
        r.Valor.FacturasGeneradas.Should().Be(0);
        r.Valor.EmpresaId.Should().Be(Empresa);
        r.Valor.EventosDominio.Should().ContainSingle(e => e is FacturaRecurrenteCreada);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Crear_rechaza_nombre_vacio(string? nombre)
    {
        FacturaRecurrente.Crear(Empresa, nombre, ClienteId, Periodicidad.Mensual, Inicio, null, 0m, UnaLinea(), Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_sin_lineas()
    {
        FacturaRecurrente.Crear(Empresa, "Cuota", ClienteId, Periodicidad.Mensual, Inicio, null, 0m, [], Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_rechaza_fecha_fin_anterior_a_la_primera_emision()
    {
        FacturaRecurrente.Crear(Empresa, "Cuota", ClienteId, Periodicidad.Mensual, Inicio, Inicio.AddDays(-1), 0m, UnaLinea(), Reloj)
            .EsFallo.Should().BeTrue();
    }

    [Theory]
    [InlineData(Periodicidad.Semanal, "2026-01-08")]
    [InlineData(Periodicidad.Mensual, "2026-02-01")]
    [InlineData(Periodicidad.Trimestral, "2026-04-01")]
    [InlineData(Periodicidad.Semestral, "2026-07-01")]
    [InlineData(Periodicidad.Anual, "2027-01-01")]
    public void RegistrarEmision_avanza_la_proxima_fecha_segun_la_periodicidad(Periodicidad periodicidad, string esperada)
    {
        var r = Crear(periodicidad);

        r.RegistrarEmision(Inicio);

        r.ProximaEmision.Should().Be(DateOnly.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));
        r.FacturasGeneradas.Should().Be(1);
        r.UltimaEmision.Should().Be(Inicio);
        r.Activa.Should().BeTrue();
    }

    [Fact]
    public void RegistrarEmision_se_desactiva_al_superar_la_fecha_de_fin()
    {
        var r = Crear(Periodicidad.Mensual, fin: new DateOnly(2026, 1, 20));

        r.RegistrarEmision(Inicio); // avanza a 2026-02-01, que supera el fin → se pausa sola.

        r.Activa.Should().BeFalse();
    }

    [Fact]
    public void EstaVencida_es_true_solo_si_activa_y_con_fecha_vencida()
    {
        var r = Crear();

        r.EstaVencida(Inicio).Should().BeTrue();
        r.EstaVencida(Inicio.AddDays(-1)).Should().BeFalse();

        r.Desactivar();
        r.EstaVencida(Inicio).Should().BeFalse();
    }

    [Fact]
    public void Actualizar_cambia_datos_y_lineas()
    {
        var r = Crear();

        var res = r.Actualizar("Nueva cuota", Periodicidad.Trimestral, new DateOnly(2026, 3, 1), null, 15m,
            [new LineaPlantilla("Consultoría", 2m, 200m, "IVA21", 21m)]);

        res.EsCorrecto.Should().BeTrue();
        r.Nombre.Should().Be("Nueva cuota");
        r.Periodicidad.Should().Be(Periodicidad.Trimestral);
        r.PorcentajeIrpf.Should().Be(15m);
        r.Lineas.Should().ContainSingle(l => l.Descripcion == "Consultoría");
    }
}

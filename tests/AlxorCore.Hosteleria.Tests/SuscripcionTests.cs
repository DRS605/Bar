using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas del plan contratado (Essential vs Pro) y la suscripción del local.</summary>
public sealed class PlanesBarTests
{
    [Theory]
    [InlineData("Essential", PlanBar.Essential)]
    [InlineData("essential", PlanBar.Essential)]
    [InlineData("Pro", PlanBar.Pro)]
    [InlineData("pro", PlanBar.Pro)]
    public void Normalizar_reconoce_los_planes_validos(string texto, PlanBar esperado) =>
        PlanesBar.Normalizar(texto).Should().Be(esperado);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("premium")]
    [InlineData("99")]
    public void Normalizar_cae_al_plan_por_defecto_si_no_es_valido(string? texto) =>
        PlanesBar.Normalizar(texto).Should().Be(PlanesBar.PorDefecto);

    [Fact]
    public void El_plan_por_defecto_es_Pro_para_no_capar_a_los_locales_existentes() =>
        PlanesBar.PorDefecto.Should().Be(PlanBar.Pro);

    [Fact]
    public void Solo_Pro_incluye_las_funciones_Pro()
    {
        PlanesBar.IncluyeFuncionesPro(PlanBar.Pro).Should().BeTrue();
        PlanesBar.IncluyeFuncionesPro(PlanBar.Essential).Should().BeFalse();
    }
}

public sealed class SuscripcionBarTests
{
    private static readonly IReloj Reloj = new RelojFijo();

    [Fact]
    public void Crear_fija_empresa_y_plan()
    {
        var empresa = Guid.NewGuid();
        var s = SuscripcionBar.Crear(empresa, PlanBar.Essential, Reloj);

        s.EmpresaId.Should().Be(empresa);
        s.Plan.Should().Be(PlanBar.Essential);
        s.ActualizadaEn.Should().Be(Reloj.AhoraUtc);
    }

    [Fact]
    public void CambiarPlan_actualiza_el_plan()
    {
        var s = SuscripcionBar.Crear(Guid.NewGuid(), PlanBar.Essential, Reloj);

        s.CambiarPlan(PlanBar.Pro, Reloj);

        s.Plan.Should().Be(PlanBar.Pro);
    }
}

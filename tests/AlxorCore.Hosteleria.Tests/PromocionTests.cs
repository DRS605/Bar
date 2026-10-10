using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas de las promociones (descuentos por ámbito, días y franja horaria).</summary>
public sealed class PromocionTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    private static Promocion Crear(AmbitoPromocion ambito = AmbitoPromocion.Todo, string? cat = null, Guid? prod = null, string? dias = null, TimeOnly? i = null, TimeOnly? f = null, decimal pct = 20m) =>
        Promocion.Crear(Empresa, "Promo", pct, ambito, cat, prod, dias, i, f, Reloj).Valor;

    [Fact]
    public void Crear_valida_porcentaje_y_ambito()
    {
        Promocion.Crear(Empresa, "P", 0m, AmbitoPromocion.Todo, null, null, null, null, null, Reloj).Error!.Codigo.Should().Be("promocion.porcentaje_invalido");
        Promocion.Crear(Empresa, "P", 20m, AmbitoPromocion.Categoria, null, null, null, null, null, Reloj).Error!.Codigo.Should().Be("promocion.sin_categoria");
        Promocion.Crear(Empresa, "", 20m, AmbitoPromocion.Todo, null, null, null, null, null, Reloj).Error!.Codigo.Should().Be("promocion.sin_nombre");
    }

    [Fact]
    public void Ambito_todo_aplica_a_cualquiera()
    {
        Crear().Aplica("Cervezas", Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(12, 0)).Should().BeTrue();
    }

    [Fact]
    public void Ambito_categoria_solo_su_categoria()
    {
        var p = Crear(AmbitoPromocion.Categoria, cat: "Cervezas");
        p.Aplica("Cervezas", Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(12, 0)).Should().BeTrue();
        p.Aplica("Vinos", Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(12, 0)).Should().BeFalse();
    }

    [Fact]
    public void Filtra_por_dia()
    {
        var p = Crear(dias: "1"); // lunes
        p.Aplica(null, Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(12, 0)).Should().BeTrue();
        p.Aplica(null, Guid.NewGuid(), DayOfWeek.Tuesday, new TimeOnly(12, 0)).Should().BeFalse();
    }

    [Fact]
    public void Franja_horaria_normal_y_nocturna()
    {
        var tarde = Crear(i: new TimeOnly(18, 0), f: new TimeOnly(20, 0));
        tarde.Aplica(null, Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(19, 0)).Should().BeTrue();
        tarde.Aplica(null, Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(21, 0)).Should().BeFalse();

        var noche = Crear(i: new TimeOnly(22, 0), f: new TimeOnly(2, 0));
        noche.Aplica(null, Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(23, 30)).Should().BeTrue();
        noche.Aplica(null, Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(1, 0)).Should().BeTrue();
        noche.Aplica(null, Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(12, 0)).Should().BeFalse();
    }

    [Fact]
    public void Pausada_no_aplica()
    {
        var p = Crear();
        p.CambiarActiva(false);
        p.Aplica("Cervezas", Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(12, 0)).Should().BeFalse();
    }
}

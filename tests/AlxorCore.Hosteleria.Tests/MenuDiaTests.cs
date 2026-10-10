using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas del menú del día (precio, secciones y platos).</summary>
public sealed class MenuDiaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    private static (string, string)[] Platos() => new[]
    {
        ("Primeros", "Ensalada mixta"),
        ("Primeros", "Lentejas"),
        ("Segundos", "Merluza"),
        ("Postres", "Flan"),
    };

    [Fact]
    public void Fijar_agrupa_los_platos_por_seccion_en_orden()
    {
        var menu = MenuDia.Crear(Empresa, Reloj);

        menu.Fijar(12.95m, activo: true, incluye: "Pan y bebida", Platos(), Reloj).EsCorrecto.Should().BeTrue();

        var dto = MenuDiaDto.Desde(menu);
        dto.Precio.Should().Be(12.95m);
        dto.Activo.Should().BeTrue();
        dto.Incluye.Should().Be("Pan y bebida");
        dto.Secciones.Select(s => s.Titulo).Should().ContainInOrder("Primeros", "Segundos", "Postres");
        dto.Secciones[0].Platos.Should().ContainInOrder("Ensalada mixta", "Lentejas");
    }

    [Fact]
    public void Fijar_descarta_los_platos_con_nombre_vacio()
    {
        var menu = MenuDia.Crear(Empresa, Reloj);

        menu.Fijar(10m, true, null, new[] { ("Primeros", "Sopa"), ("Primeros", "   "), ("Segundos", "") }, Reloj);

        menu.Platos.Should().HaveCount(1);
        menu.Platos[0].Nombre.Should().Be("Sopa");
    }

    [Fact]
    public void Fijar_rechaza_precio_negativo()
    {
        var menu = MenuDia.Crear(Empresa, Reloj);
        menu.Fijar(-1m, true, null, Platos(), Reloj).Error.Codigo.Should().Be("menu_dia.precio_negativo");
    }

    [Fact]
    public void Fijar_rechaza_precio_excesivo()
    {
        var menu = MenuDia.Crear(Empresa, Reloj);
        menu.Fijar(5000m, true, null, Platos(), Reloj).Error.Codigo.Should().Be("menu_dia.precio_excesivo");
    }

    [Fact]
    public void Incluye_vacio_se_guarda_como_nulo()
    {
        var menu = MenuDia.Crear(Empresa, Reloj);
        menu.Fijar(12m, true, "   ", Platos(), Reloj);
        menu.Incluye.Should().BeNull();
    }
}

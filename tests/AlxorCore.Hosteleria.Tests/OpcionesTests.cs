using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

/// <summary>Pruebas de los formatos/extras de producto y su resolución de precio.</summary>
public sealed class OpcionesTests
{
    private static GrupoOpcion Tamano(bool obligatorio = true)
    {
        var g = GrupoOpcion.Crear(Guid.NewGuid(), Guid.NewGuid(), "Tamaño", SeleccionOpcion.Unica, obligatorio, 0);
        g.AgregarOpcion("Media ración", -3m, 0);
        g.AgregarOpcion("Ración", 0m, 1);
        return g;
    }

    [Fact]
    public void Resolver_suma_deltas_y_compone_texto()
    {
        var tam = Tamano();
        var extras = GrupoOpcion.Crear(Guid.NewGuid(), Guid.NewGuid(), "Extras", SeleccionOpcion.Multiple, false, 1);
        extras.AgregarOpcion("Queso", 1m, 0);
        var media = tam.Opciones.First(o => o.Nombre.StartsWith("Media"));
        var queso = extras.Opciones.First();

        var r = ResolverOpciones.Resolver(new[] { tam, extras }, new[] { media.Id, queso.Id });

        r.Error.Should().BeNull();
        r.PrecioDelta.Should().Be(-2m);
        r.Texto.Should().Be("Media ración, Queso");
    }

    [Fact]
    public void Resolver_exige_las_obligatorias()
    {
        var tam = Tamano(obligatorio: true);
        ResolverOpciones.Resolver(new[] { tam }, Array.Empty<Guid>()).Error!.Codigo.Should().Be("opcion.obligatoria");
    }

    [Fact]
    public void Resolver_rechaza_dos_en_un_grupo_unico()
    {
        var tam = Tamano();
        var ids = tam.Opciones.Select(o => o.Id).ToArray();
        ResolverOpciones.Resolver(new[] { tam }, ids).Error!.Codigo.Should().Be("opcion.seleccion_unica");
    }
}

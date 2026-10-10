using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas del menú del día: gestión por el bar (Pro) y publicación en la carta del cliente.</summary>
public sealed class MenuDiaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public MenuDiaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record SeccionResp(string Titulo, List<string> Platos);
    private sealed record MenuResp(decimal Precio, bool Activo, string? Incluye, List<SeccionResp> Secciones);
    private sealed record CartaResp(string Local, MenuResp? MenuDia);

    private static object MenuEjemplo(bool activo) => new
    {
        Precio = 12.95m,
        Activo = activo,
        Incluye = "Pan y bebida",
        Secciones = new[]
        {
            new { Titulo = "Primeros", Platos = new[] { "Ensalada mixta", "Lentejas" } },
            new { Titulo = "Segundos", Platos = new[] { "Merluza", "Pollo asado" } },
            new { Titulo = "Postres", Platos = new[] { "Flan", "Café" } },
        },
    };

    [Fact]
    public async Task Menu_por_defecto_viene_vacio_e_inactivo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var menu = (await cliente.GetFromJsonAsync<MenuResp>("/carta/menu-dia"))!;

        menu.Activo.Should().BeFalse();
        menu.Secciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_y_leer_el_menu_del_dia()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var put = await cliente.PutAsJsonAsync("/carta/menu-dia", MenuEjemplo(activo: true));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var menu = (await cliente.GetFromJsonAsync<MenuResp>("/carta/menu-dia"))!;
        menu.Precio.Should().Be(12.95m);
        menu.Activo.Should().BeTrue();
        menu.Incluye.Should().Be("Pan y bebida");
        menu.Secciones.Select(s => s.Titulo).Should().ContainInOrder("Primeros", "Segundos", "Postres");
        menu.Secciones[0].Platos.Should().ContainInOrder("Ensalada mixta", "Lentejas");
    }

    [Fact]
    public async Task El_menu_activo_aparece_en_la_carta_publica()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PutAsJsonAsync("/carta/menu-dia", MenuEjemplo(activo: true))).EnsureSuccessStatusCode();

        var anon = _fabrica.CreateClient();
        var carta = (await anon.GetFromJsonAsync<CartaResp>($"/carta/{empresaId}/datos?idioma=es"))!;

        carta.MenuDia.Should().NotBeNull();
        carta.MenuDia!.Precio.Should().Be(12.95m);
        carta.MenuDia.Secciones.Should().HaveCount(3);
    }

    [Fact]
    public async Task El_menu_inactivo_no_aparece_en_la_carta_publica()
    {
        var (cliente, empresaId) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PutAsJsonAsync("/carta/menu-dia", MenuEjemplo(activo: false))).EnsureSuccessStatusCode();

        var anon = _fabrica.CreateClient();
        var carta = (await anon.GetFromJsonAsync<CartaResp>($"/carta/{empresaId}/datos?idioma=es"))!;

        carta.MenuDia.Should().BeNull();
    }

    [Fact]
    public async Task En_Essential_el_menu_del_dia_esta_capado()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        (await cliente.PutAsJsonAsync("/suscripcion", new { Plan = "Essential" })).EnsureSuccessStatusCode();

        (await cliente.GetAsync(new Uri("/carta/menu-dia", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

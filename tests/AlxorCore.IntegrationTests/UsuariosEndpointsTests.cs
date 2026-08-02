using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la gestión de usuarios de la empresa (miembros y roles).</summary>
public sealed class UsuariosEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public UsuariosEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record MiembroDto(Guid UsuarioId, string Email, string Nombre, bool EmailVerificado, string Rol, string RolNombre, string Estado, bool EsYo);
    private sealed record InvitarResp(Guid UsuarioId, bool Creado, string? EnlaceContrasena);

    [Fact]
    public async Task El_creador_de_la_empresa_es_su_unico_miembro_propietario()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        var miembros = await cliente.GetFromJsonAsync<List<MiembroDto>>("/usuarios");
        miembros.Should().ContainSingle();
        miembros![0].Rol.Should().Be("propietario");
        miembros[0].EsYo.Should().BeTrue();
    }

    [Fact]
    public async Task Invitar_a_un_usuario_nuevo_lo_crea_y_lo_hace_miembro()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = $"invitado{Guid.NewGuid():N}@ejemplo.com";

        var resp = await cliente.PostAsJsonAsync("/usuarios/invitar", new { Email = email, Nombre = "Nuevo", Rol = "usuario" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitado = await resp.Content.ReadFromJsonAsync<InvitarResp>();
        invitado!.Creado.Should().BeTrue();
        invitado.EnlaceContrasena.Should().NotBeNullOrEmpty(); // token para fijar contraseña (fuera de producción)

        var miembros = await cliente.GetFromJsonAsync<List<MiembroDto>>("/usuarios");
        miembros.Should().HaveCount(2);
        miembros.Should().Contain(m => m.Email == email && m.Rol == "usuario");
    }

    [Fact]
    public async Task Cambiar_rol_y_revocar_a_un_miembro()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var email = $"invitado{Guid.NewGuid():N}@ejemplo.com";
        var invitado = await (await cliente.PostAsJsonAsync("/usuarios/invitar", new { Email = email, Rol = "usuario" }))
            .Content.ReadFromJsonAsync<InvitarResp>();

        var cambio = await cliente.PostAsJsonAsync($"/usuarios/{invitado!.UsuarioId}/rol", new { Rol = "solo_lectura" });
        cambio.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var revoca = await cliente.PostAsync(new Uri($"/usuarios/{invitado.UsuarioId}/revocar", UriKind.Relative), content: null);
        revoca.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var miembros = await cliente.GetFromJsonAsync<List<MiembroDto>>("/usuarios");
        miembros.Should().Contain(m => m.UsuarioId == invitado.UsuarioId && m.Rol == "solo_lectura" && m.Estado == "Revocada");
    }

    [Fact]
    public async Task No_puedo_revocarme_a_mi_mismo()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var yo = (await cliente.GetFromJsonAsync<List<MiembroDto>>("/usuarios"))![0];

        var resp = await cliente.PostAsync(new Uri($"/usuarios/{yo.UsuarioId}/revocar", UriKind.Relative), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

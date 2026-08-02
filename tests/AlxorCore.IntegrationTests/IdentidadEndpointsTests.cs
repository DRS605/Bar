using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de extremo a extremo del módulo Identidad sobre PostgreSQL real.</summary>
public sealed class IdentidadEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public IdentidadEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private static string EmailUnico() => $"u{Guid.NewGuid():N}@ejemplo.com";

    private sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);

    private sealed record LoginPeticion(string Email, string Contrasena);

    private sealed record PerfilDto(Guid Id, string Email, string Nombre, bool EmailVerificado);

    private sealed record RegistroConTokenDto(PerfilDto Perfil, string TokenVerificacion);

    private sealed record LoginRespuesta(string Token, DateTimeOffset ExpiraEn, PerfilDto Usuario);

    [Fact]
    public async Task Salud_responde_ok()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/salud", UriKind.Relative));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Flujo_registro_login_y_perfil_funciona_de_extremo_a_extremo()
    {
        var cliente = _fabrica.CreateClient();
        var email = EmailUnico();

        // Registro
        var registro = await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));
        registro.StatusCode.Should().Be(HttpStatusCode.Created);

        // Login
        var login = await cliente.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "contrasena123"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var datos = await login.Content.ReadFromJsonAsync<LoginRespuesta>();
        datos.Should().NotBeNull();
        datos!.Token.Should().NotBeNullOrWhiteSpace();
        datos.Usuario.Email.Should().Be(email);
        datos.Usuario.EmailVerificado.Should().BeFalse();

        // Perfil con el token
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", datos.Token);
        var perfil = await cliente.GetFromJsonAsync<PerfilDto>("/auth/perfil");
        perfil.Should().NotBeNull();
        perfil!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Registro_con_email_duplicado_devuelve_409()
    {
        var cliente = _fabrica.CreateClient();
        var email = EmailUnico();

        var primero = await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));
        primero.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundo = await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Otro", "contrasena123"));
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_con_credenciales_incorrectas_devuelve_401()
    {
        var cliente = _fabrica.CreateClient();
        var email = EmailUnico();
        await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));

        var login = await cliente.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "incorrecta"));

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Perfil_sin_token_devuelve_401()
    {
        var cliente = _fabrica.CreateClient();

        var perfil = await cliente.GetAsync(new Uri("/auth/perfil", UriKind.Relative));

        perfil.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verificar_email_marca_el_perfil_como_verificado()
    {
        var cliente = _fabrica.CreateClient();
        var email = EmailUnico();
        var registro = await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));
        var token = (await registro.Content.ReadFromJsonAsync<RegistroConTokenDto>())!.TokenVerificacion;

        var verificar = await cliente.PostAsJsonAsync("/auth/verificar-email", new { Token = token });
        verificar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await cliente.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "contrasena123"));
        var datos = await login.Content.ReadFromJsonAsync<LoginRespuesta>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", datos!.Token);

        var perfil = await cliente.GetFromJsonAsync<PerfilDto>("/auth/perfil");
        perfil!.EmailVerificado.Should().BeTrue();
    }
}

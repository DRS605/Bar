using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AlxorCore.IntegrationTests;

/// <summary>Ayudas comunes para las pruebas de integración (registro, login, alta y selección de empresa).</summary>
internal static class Ayudas
{
    private static int _contadorNif = 20_000_000;

    private sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);
    private sealed record LoginPeticion(string Email, string Contrasena);
    private sealed record LoginRespuesta(string Token);
    private sealed record CrearEmpresaPeticion(string Nif, string RazonSocial);
    private sealed record EmpresaResp(Guid Id);
    private sealed record SeleccionResp(string Token, Guid EmpresaId);

    public static string EmailUnico() => $"u{Guid.NewGuid():N}@ejemplo.com";

    /// <summary>Genera un DNI válido y único.</summary>
    public static string GenerarNif()
    {
        const string letras = "TRWAGMYFPDXBNJZSQVHLCKE";
        var numero = System.Threading.Interlocked.Increment(ref _contadorNif) % 100_000_000;
        return $"{numero:D8}{letras[numero % 23]}";
    }

    /// <summary>Registra e inicia sesión; devuelve un cliente autenticado sin empresa activa.</summary>
    public static async Task<HttpClient> AutenticadoAsync(FabricaApiPruebas fabrica)
    {
        var cliente = fabrica.CreateClient();
        var email = EmailUnico();
        await cliente.PostAsJsonAsync("/auth/registro", new RegistroPeticion(email, "Ana", "contrasena123"));
        var login = await cliente.PostAsJsonAsync("/auth/login", new LoginPeticion(email, "contrasena123"));
        var datos = await login.Content.ReadFromJsonAsync<LoginRespuesta>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", datos!.Token);
        return cliente;
    }

    /// <summary>Autentica, crea una empresa y la selecciona; devuelve el cliente con token de empresa y su id.</summary>
    public static async Task<(HttpClient Cliente, Guid EmpresaId)> ConEmpresaAsync(FabricaApiPruebas fabrica)
    {
        var cliente = await AutenticadoAsync(fabrica).ConfigureAwait(false);
        var crear = await cliente.PostAsJsonAsync("/empresas", new CrearEmpresaPeticion(GenerarNif(), "Empresa de Pruebas SL"));
        var empresa = await crear.Content.ReadFromJsonAsync<EmpresaResp>();
        var seleccion = await cliente.PostAsync(new Uri($"/empresas/{empresa!.Id}/seleccionar", UriKind.Relative), content: null);
        var alcance = await seleccion.Content.ReadFromJsonAsync<SeleccionResp>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alcance!.Token);
        return (cliente, alcance.EmpresaId);
    }
}

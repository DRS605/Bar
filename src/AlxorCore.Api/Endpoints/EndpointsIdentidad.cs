using AlxorCore.Api.Comun;
using AlxorCore.Api.Contratos;
using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Identidad (autenticación y perfil).</summary>
public static class EndpointsIdentidad
{
    public static IEndpointRouteBuilder MapearIdentidad(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/auth").WithTags("Autenticación");

        grupo.MapPost("/registro", RegistrarAsync)
            .WithName("RegistrarUsuario")
            .WithSummary("Crea una cuenta de usuario.")
            .AllowAnonymous();

        grupo.MapPost("/login", LoginAsync)
            .WithName("IniciarSesion")
            .WithSummary("Inicia sesión y devuelve un token de acceso.")
            .AllowAnonymous();

        grupo.MapGet("/perfil", PerfilAsync)
            .WithName("ObtenerPerfil")
            .WithSummary("Devuelve el perfil del usuario autenticado.")
            .RequireAuthorization();

        grupo.MapPost("/verificar-email", VerificarEmailAsync)
            .WithName("VerificarEmail")
            .WithSummary("Marca como verificado el correo del usuario autenticado.")
            .RequireAuthorization();

        return rutas;
    }

    private static async Task<IResult> RegistrarAsync(
        RegistroPeticion peticion,
        RegistrarUsuario casoDeUso,
        CancellationToken ct)
    {
        var resultado = await casoDeUso
            .EjecutarAsync(new RegistrarUsuarioComando(peticion.Email, peticion.Nombre, peticion.Contrasena), ct)
            .ConfigureAwait(false);

        return resultado.EsCorrecto
            ? resultado.ACreado($"/auth/perfil")
            : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> LoginAsync(
        LoginPeticion peticion,
        IniciarSesion casoDeUso,
        CancellationToken ct)
    {
        var resultado = await casoDeUso
            .EjecutarAsync(new IniciarSesionComando(peticion.Email, peticion.Contrasena), ct)
            .ConfigureAwait(false);

        return resultado.AOk();
    }

    private static async Task<IResult> PerfilAsync(
        System.Security.Claims.ClaimsPrincipal usuario,
        ObtenerPerfil casoDeUso,
        CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var resultado = await casoDeUso.EjecutarAsync(usuarioId.Value, ct).ConfigureAwait(false);
        return resultado.AOk();
    }

    private static async Task<IResult> VerificarEmailAsync(
        System.Security.Claims.ClaimsPrincipal usuario,
        VerificarEmail casoDeUso,
        CancellationToken ct)
    {
        var usuarioId = usuario.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return ResultadosHttp.AProblema(Error.NoAutenticado("auth.token_invalido", "El token no identifica al usuario."));
        }

        var resultado = await casoDeUso.EjecutarAsync(usuarioId.Value, ct).ConfigureAwait(false);
        return resultado.ASinContenido();
    }
}

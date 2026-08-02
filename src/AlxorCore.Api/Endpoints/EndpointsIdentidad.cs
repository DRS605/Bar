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
            .WithSummary("Verifica el correo con el token del enlace.")
            .AllowAnonymous();

        grupo.MapPost("/recuperar", RecuperarAsync)
            .WithName("RecuperarContrasena")
            .WithSummary("Solicita un enlace de restablecimiento de contraseña.")
            .AllowAnonymous();

        grupo.MapPost("/restablecer", RestablecerAsync)
            .WithName("RestablecerContrasena")
            .WithSummary("Fija una nueva contraseña con el token del enlace.")
            .AllowAnonymous();

        return rutas;
    }

    private static async Task<IResult> RegistrarAsync(
        RegistroPeticion peticion,
        RegistrarUsuario casoDeUso,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        var resultado = await casoDeUso
            .EjecutarAsync(new RegistrarUsuarioComando(peticion.Email, peticion.Nombre, peticion.Contrasena), ct)
            .ConfigureAwait(false);

        if (resultado.EsFallo)
        {
            return ResultadosHttp.AProblema(resultado.Error);
        }

        // El token del enlace de verificación solo se expone fuera de producción (en producción va solo por correo).
        var cuerpo = entorno.IsProduction()
            ? (object)resultado.Valor.Perfil
            : new { resultado.Valor.Perfil, resultado.Valor.TokenVerificacion };
        return Results.Created("/auth/perfil", cuerpo);
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
        VerificarEmailPeticion peticion,
        VerificarEmail casoDeUso,
        CancellationToken ct)
    {
        var resultado = await casoDeUso.EjecutarAsync(peticion.Token, ct).ConfigureAwait(false);
        return resultado.ASinContenido();
    }

    private static async Task<IResult> RecuperarAsync(
        RecuperarPeticion peticion,
        RecuperarContrasena casoDeUso,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        var token = await casoDeUso.EjecutarAsync(peticion.Email, ct).ConfigureAwait(false);

        // Respuesta uniforme (no revela si el correo existe). Fuera de producción devuelve el token para pruebas.
        var mensaje = "Si el correo corresponde a una cuenta, te hemos enviado un enlace para restablecer la contraseña.";
        return entorno.IsProduction() || token is null
            ? Results.Ok(new { mensaje })
            : Results.Ok(new { mensaje, token });
    }

    private static async Task<IResult> RestablecerAsync(
        RestablecerPeticion peticion,
        RestablecerContrasena casoDeUso,
        CancellationToken ct)
    {
        var resultado = await casoDeUso
            .EjecutarAsync(new RestablecerContrasenaComando(peticion.Token, peticion.NuevaContrasena), ct)
            .ConfigureAwait(false);
        return resultado.ASinContenido();
    }
}

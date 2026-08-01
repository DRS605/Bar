using AlxorCore.Identidad.Aplicacion.Modelos;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Credenciales para iniciar sesión.</summary>
public sealed record IniciarSesionComando(string Email, string Contrasena);

/// <summary>
/// Caso de uso: autenticación por correo y contraseña. Ante credenciales incorrectas devuelve
/// siempre el mismo error genérico para no revelar si el correo existe (evita enumeración).
/// </summary>
public sealed class IniciarSesion
{
    private static readonly Error CredencialesInvalidas =
        Error.NoAutenticado("auth.credenciales_invalidas", "El correo o la contraseña no son correctos.");

    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IProveedorTokens _tokens;

    public IniciarSesion(IRepositorioUsuarios usuarios, IHasherContrasena hasher, IProveedorTokens tokens)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<Resultado<ResultadoAutenticacion>> EjecutarAsync(IniciarSesionComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var email = Email.Crear(comando.Email);
        if (email.EsFallo)
        {
            return Resultado.Fallo<ResultadoAutenticacion>(CredencialesInvalidas);
        }

        var usuario = await _usuarios.ObtenerPorEmailAsync(email.Valor, ct).ConfigureAwait(false);
        if (usuario is null || !_hasher.Verificar(usuario.HashContrasena.Valor, comando.Contrasena ?? string.Empty))
        {
            return Resultado.Fallo<ResultadoAutenticacion>(CredencialesInvalidas);
        }

        if (!usuario.PuedeAutenticarse)
        {
            return Resultado.Fallo<ResultadoAutenticacion>(
                Error.Prohibido("auth.cuenta_suspendida", "La cuenta está suspendida."));
        }

        var token = _tokens.GenerarToken(usuario);
        var resultado = new ResultadoAutenticacion(token.Token, token.ExpiraEn, PerfilUsuario.Desde(usuario));
        return Resultado.Ok(resultado);
    }
}

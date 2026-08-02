using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>
/// Caso de uso: solicitar el restablecimiento de contraseña. Emite un token (con caducidad corta),
/// lo almacena (solo el hash) y envía el enlace por correo. Por seguridad <b>no revela</b> si el
/// correo existe: siempre responde igual y devuelve el token solo para uso interno (correo).
/// </summary>
public sealed class RecuperarContrasena
{
    /// <summary>Caducidad del enlace de restablecimiento.</summary>
    public static readonly TimeSpan Caducidad = TimeSpan.FromHours(1);

    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioVerificacionEmail _correo;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RecuperarContrasena(
        IRepositorioUsuarios usuarios, IServicioVerificacionEmail correo, IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _correo = correo;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    /// <summary>Devuelve el token generado (o <c>null</c> si el correo no corresponde a ninguna cuenta).</summary>
    public async Task<string?> EjecutarAsync(string emailTexto, CancellationToken ct = default)
    {
        var email = Email.Crear(emailTexto);
        if (email.EsFallo)
        {
            return null;
        }

        var usuario = await _usuarios.ObtenerPorEmailAsync(email.Valor, ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return null;
        }

        var token = TokenCuenta.Nuevo();
        usuario.EmitirTokenRestablecimiento(token, _reloj.AhoraUtc + Caducidad, _reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        await _correo.EnviarRestablecimientoAsync(usuario, token, ct).ConfigureAwait(false);
        return token;
    }
}

/// <summary>Datos para restablecer la contraseña con el token del enlace.</summary>
public sealed record RestablecerContrasenaComando(string Token, string NuevaContrasena);

/// <summary>
/// Caso de uso: fijar una nueva contraseña usando el token de restablecimiento. Valida el token (por
/// su hash) y su caducidad, y consume el token para que no pueda reutilizarse.
/// </summary>
public sealed class RestablecerContrasena
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RestablecerContrasena(
        IRepositorioUsuarios usuarios, IHasherContrasena hasher, IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(RestablecerContrasenaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (string.IsNullOrEmpty(comando.NuevaContrasena) || comando.NuevaContrasena.Length < RegistrarUsuario.LongitudMinimaContrasena)
        {
            return Resultado.Fallo(Error.Validacion(
                "contrasena.corta", $"La contraseña debe tener al menos {RegistrarUsuario.LongitudMinimaContrasena} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(comando.Token))
        {
            return Resultado.Fallo(Error.Validacion("restablecimiento.token_invalido", "El enlace de restablecimiento no es válido."));
        }

        var usuario = await _usuarios.ObtenerPorTokenRestablecimientoAsync(TokenCuenta.Hash(comando.Token), ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Resultado.Fallo(Error.Validacion("restablecimiento.token_invalido", "El enlace de restablecimiento no es válido."));
        }

        var hash = HashContrasena.DesdeHash(_hasher.Hash(comando.NuevaContrasena));
        var resultado = usuario.RestablecerConToken(comando.Token, hash, _reloj);
        if (resultado.EsFallo)
        {
            return resultado;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

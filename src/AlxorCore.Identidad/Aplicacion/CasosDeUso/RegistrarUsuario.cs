using AlxorCore.Identidad.Aplicacion.Modelos;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Datos de entrada para registrar un usuario.</summary>
public sealed record RegistrarUsuarioComando(string Email, string Nombre, string Contrasena);

/// <summary>Resultado del registro: el perfil creado y el token de verificación de correo emitido.</summary>
public sealed record ResultadoRegistro(PerfilUsuario Perfil, string TokenVerificacion);

/// <summary>
/// Caso de uso: alta de un usuario nuevo. Valida el correo y la contraseña, garantiza que el
/// correo no esté ya en uso, cifra la contraseña, crea el agregado y dispara (stub) el envío
/// del correo de verificación.
/// </summary>
public sealed class RegistrarUsuario
{
    /// <summary>Longitud mínima exigida a la contraseña.</summary>
    public const int LongitudMinimaContrasena = 8;

    /// <summary>Longitud máxima admitida para la contraseña (evita abusos de coste de hashing).</summary>
    public const int LongitudMaximaContrasena = 128;

    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IServicioVerificacionEmail _verificacionEmail;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarUsuario(
        IRepositorioUsuarios usuarios,
        IHasherContrasena hasher,
        IServicioVerificacionEmail verificacionEmail,
        IUnidadDeTrabajoIdentidad unidadDeTrabajo,
        IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _verificacionEmail = verificacionEmail;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    /// <summary>Caducidad del enlace de verificación de correo.</summary>
    public static readonly TimeSpan CaducidadVerificacion = TimeSpan.FromHours(48);

    public async Task<Resultado<ResultadoRegistro>> EjecutarAsync(RegistrarUsuarioComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var email = Email.Crear(comando.Email);
        if (email.EsFallo)
        {
            return Resultado.Fallo<ResultadoRegistro>(email.Error);
        }

        var errorContrasena = ValidarContrasena(comando.Contrasena);
        if (errorContrasena is not null)
        {
            return Resultado.Fallo<ResultadoRegistro>(errorContrasena);
        }

        if (await _usuarios.ExisteEmailAsync(email.Valor, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<ResultadoRegistro>(
                Error.Conflicto("usuario.email_en_uso", "Ya existe una cuenta con ese correo electrónico."));
        }

        var hash = HashContrasena.DesdeHash(_hasher.Hash(comando.Contrasena));

        var usuario = Usuario.Registrar(email.Valor, comando.Nombre, hash, _reloj);
        if (usuario.EsFallo)
        {
            return Resultado.Fallo<ResultadoRegistro>(usuario.Error);
        }

        var token = TokenCuenta.Nuevo();
        usuario.Valor.EmitirTokenVerificacion(token, _reloj.AhoraUtc + CaducidadVerificacion, _reloj);

        _usuarios.Agregar(usuario.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);

        await _verificacionEmail.EnviarVerificacionAsync(usuario.Valor, token, ct).ConfigureAwait(false);

        return Resultado.Ok(new ResultadoRegistro(PerfilUsuario.Desde(usuario.Valor), token));
    }

    private static Error? ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena)
        {
            return Error.Validacion(
                "contrasena.corta",
                $"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres.");
        }

        if (contrasena.Length > LongitudMaximaContrasena)
        {
            return Error.Validacion("contrasena.larga", "La contraseña es demasiado larga.");
        }

        return null;
    }
}

using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Resultado de invitar a un usuario: su resumen y el token para que fije su contraseña.</summary>
public sealed record ResultadoUsuarioInvitado(UsuarioResumen Usuario, string TokenRestablecimiento);

/// <summary>
/// Caso de uso: crear un usuario <b>invitado</b> a la plataforma. Se da de alta con una contraseña
/// aleatoria (que nadie conoce) y se emite un token de restablecimiento para que el invitado fije la
/// suya mediante el enlace. Lo usa la invitación de miembros a una empresa.
/// </summary>
public sealed class CrearUsuarioInvitado
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly IServicioVerificacionEmail _correo;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearUsuarioInvitado(
        IRepositorioUsuarios usuarios, IHasherContrasena hasher, IServicioVerificacionEmail correo,
        IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _correo = correo;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ResultadoUsuarioInvitado>> EjecutarAsync(string emailTexto, string? nombre, CancellationToken ct = default)
    {
        var email = Email.Crear(emailTexto);
        if (email.EsFallo)
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(email.Error);
        }

        if (await _usuarios.ExisteEmailAsync(email.Valor, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(
                Error.Conflicto("usuario.email_en_uso", "Ya existe una cuenta con ese correo electrónico."));
        }

        // Contraseña aleatoria: el invitado no la usa, fijará la suya con el token de restablecimiento.
        var hash = HashContrasena.DesdeHash(_hasher.Hash(TokenCuenta.Nuevo()));
        var nombreEfectivo = string.IsNullOrWhiteSpace(nombre) ? emailTexto.Split('@')[0] : nombre;

        var usuario = Usuario.Registrar(email.Valor, nombreEfectivo, hash, _reloj);
        if (usuario.EsFallo)
        {
            return Resultado.Fallo<ResultadoUsuarioInvitado>(usuario.Error);
        }

        var token = TokenCuenta.Nuevo();
        usuario.Valor.EmitirTokenRestablecimiento(token, _reloj.AhoraUtc + RecuperarContrasena.Caducidad, _reloj);

        _usuarios.Agregar(usuario.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        await _correo.EnviarRestablecimientoAsync(usuario.Valor, token, ct).ConfigureAwait(false);

        var resumen = new UsuarioResumen(usuario.Valor.Id, usuario.Valor.Email.Valor, usuario.Valor.Nombre, usuario.Valor.EmailVerificado);
        return Resultado.Ok(new ResultadoUsuarioInvitado(resumen, token));
    }
}

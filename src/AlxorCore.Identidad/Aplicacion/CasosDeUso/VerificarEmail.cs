using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>
/// Caso de uso: verificar el correo a partir del <b>token</b> del enlace enviado al registrarse.
/// Busca al usuario por el hash del token y confirma si es válido y no ha caducado.
/// </summary>
public sealed class VerificarEmail
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IUnidadDeTrabajoIdentidad _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public VerificarEmail(IRepositorioUsuarios usuarios, IUnidadDeTrabajoIdentidad unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Resultado.Fallo(Error.Validacion("verificacion.token_invalido", "El enlace de verificación no es válido."));
        }

        var usuario = await _usuarios.ObtenerPorTokenVerificacionAsync(TokenCuenta.Hash(token), ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Resultado.Fallo(Error.Validacion("verificacion.token_invalido", "El enlace de verificación no es válido."));
        }

        var resultado = usuario.ConfirmarEmailConToken(token, _reloj);
        if (resultado.EsFallo)
        {
            return resultado;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

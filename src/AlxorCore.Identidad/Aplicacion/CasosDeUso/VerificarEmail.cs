using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>
/// Caso de uso: marcar como verificado el correo del usuario autenticado. En el MVP la
/// confirmación se hace sobre el propio usuario (el envío real de un enlace con token llegará
/// con el módulo Documentos).
/// </summary>
public sealed class VerificarEmail
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public VerificarEmail(IRepositorioUsuarios usuarios, IUnidadDeTrabajo unidadDeTrabajo, IReloj reloj)
    {
        _usuarios = usuarios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("usuario.no_encontrado", "El usuario no existe."));
        }

        usuario.VerificarEmail(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

using AlxorCore.Identidad.Aplicacion.Modelos;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Identidad.Aplicacion.CasosDeUso;

/// <summary>Caso de uso: obtener el perfil del usuario autenticado.</summary>
public sealed class ObtenerPerfil
{
    private readonly IRepositorioUsuarios _usuarios;

    public ObtenerPerfil(IRepositorioUsuarios usuarios) => _usuarios = usuarios;

    public async Task<Resultado<PerfilUsuario>> EjecutarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct).ConfigureAwait(false);

        return usuario is null
            ? Resultado.Fallo<PerfilUsuario>(Error.NoEncontrado("usuario.no_encontrado", "El usuario no existe."))
            : Resultado.Ok(PerfilUsuario.Desde(usuario));
    }
}

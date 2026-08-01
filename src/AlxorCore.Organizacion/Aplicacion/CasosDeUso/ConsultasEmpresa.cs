using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Caso de uso: listar las empresas del usuario autenticado.</summary>
public sealed class ListarMisEmpresas
{
    private readonly IConsultasOrganizacion _consultas;

    public ListarMisEmpresas(IConsultasOrganizacion consultas) => _consultas = consultas;

    public async Task<IReadOnlyList<EmpresaResumen>> EjecutarAsync(Guid usuarioId, CancellationToken ct = default) =>
        await _consultas.ListarEmpresasDeUsuarioAsync(usuarioId, ct).ConfigureAwait(false);
}

/// <summary>Caso de uso: obtener una empresa por su identificador (dentro del contexto de la empresa activa).</summary>
public sealed class ObtenerEmpresa
{
    private readonly IRepositorioEmpresas _empresas;

    public ObtenerEmpresa(IRepositorioEmpresas empresas) => _empresas = empresas;

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var empresa = await _empresas.ObtenerPorIdAsync(empresaId, ct).ConfigureAwait(false);
        return empresa is null
            ? Resultado.Fallo<EmpresaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."))
            : Resultado.Ok(EmpresaDto.Desde(empresa));
    }
}

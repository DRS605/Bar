using AlxorCore.Organizacion.Aplicacion.Modelos;

namespace AlxorCore.Organizacion.Aplicacion.Puertos;

/// <summary>Consultas de lectura optimizadas del módulo Organización.</summary>
public interface IConsultasOrganizacion
{
    /// <summary>Lista las empresas en las que el usuario tiene una membresía activa, con su rol.</summary>
    Task<IReadOnlyList<EmpresaResumen>> ListarEmpresasDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}

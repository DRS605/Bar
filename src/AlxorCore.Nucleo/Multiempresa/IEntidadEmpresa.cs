namespace AlxorCore.Nucleo.Multiempresa;

/// <summary>
/// Marca una entidad que pertenece a una empresa (tenant). La infraestructura de persistencia
/// aplica automáticamente un filtro por <see cref="EmpresaId"/> y la Row-Level Security de
/// PostgreSQL, de modo que ningún módulo pueda leer o escribir datos de otra empresa.
/// </summary>
public interface IEntidadEmpresa
{
    /// <summary>Empresa (tenant) a la que pertenece la entidad.</summary>
    Guid EmpresaId { get; }
}

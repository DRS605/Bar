using AlxorCore.Nucleo.Multiempresa;

namespace AlxorCore.Nucleo.Dominio;

/// <summary>
/// Raíz de agregado que pertenece a una empresa (tenant). Toda entidad de negocio de ALXOR Core
/// —salvo las globales, como el usuario— hereda de aquí y lleva su <see cref="EmpresaId"/>.
/// </summary>
/// <typeparam name="TId">Tipo del identificador.</typeparam>
public abstract class RaizAgregadoEmpresa<TId> : RaizAgregado<TId>, IEntidadEmpresa
    where TId : notnull
{
    protected RaizAgregadoEmpresa(TId id, Guid empresaId)
        : base(id)
    {
        EmpresaId = empresaId;
    }

    /// <summary>Empresa a la que pertenece el agregado.</summary>
    public Guid EmpresaId { get; protected init; }
}

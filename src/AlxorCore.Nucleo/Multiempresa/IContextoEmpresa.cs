namespace AlxorCore.Nucleo.Multiempresa;

/// <summary>
/// Contexto de la empresa (tenant) activa durante una petición. Es la pieza central de la
/// estrategia multiempresa: la infraestructura de persistencia filtra por esta empresa y
/// fija <c>app.empresa_actual</c> en PostgreSQL para que la Row-Level Security actúe como
/// red de seguridad. Se resuelve del token de la petición y su vida es por-petición.
/// </summary>
public interface IContextoEmpresa
{
    /// <summary>Identificador de la empresa activa, o <c>null</c> si la petición no está ligada a una empresa.</summary>
    Guid? EmpresaId { get; }

    /// <summary>Indica si hay una empresa activa resuelta.</summary>
    bool TieneEmpresa => EmpresaId is not null;

    /// <summary>Devuelve la empresa activa o lanza si no hay ninguna. Útil en operaciones que la exigen.</summary>
    Guid EmpresaRequerida => EmpresaId
        ?? throw new InvalidOperationException("La operación requiere una empresa activa y no hay ninguna en el contexto.");
}

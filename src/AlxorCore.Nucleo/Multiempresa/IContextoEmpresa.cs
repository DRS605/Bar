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

/// <summary>
/// Contexto de empresa cuya empresa activa puede <b>fijarse por código</b>. Lo usan los procesos en
/// segundo plano (sin petición HTTP), como la facturación automática periódica, para recorrer varias
/// empresas: crean un ámbito por empresa y fijan la suya antes de operar, manteniendo el aislamiento.
/// </summary>
public interface IContextoEmpresaMutable : IContextoEmpresa
{
    /// <summary>Fija la empresa activa del ámbito actual. Tiene prioridad sobre cualquier otra fuente.</summary>
    void Fijar(Guid empresaId);
}

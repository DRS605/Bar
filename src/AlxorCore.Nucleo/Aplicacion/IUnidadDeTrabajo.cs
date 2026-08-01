namespace AlxorCore.Nucleo.Aplicacion;

/// <summary>
/// Unidad de trabajo: confirma de forma atómica todos los cambios acumulados en el contexto
/// de persistencia y publica los eventos de dominio dentro de la misma transacción, de modo
/// que negocio y auditoría queden siempre consistentes.
/// </summary>
public interface IUnidadDeTrabajo
{
    /// <summary>Persiste los cambios pendientes y devuelve el número de filas afectadas.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken ct = default);
}

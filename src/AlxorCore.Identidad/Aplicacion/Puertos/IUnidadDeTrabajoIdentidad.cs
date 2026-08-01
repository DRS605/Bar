using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>
/// Unidad de trabajo específica del módulo Identidad. Cada módulo define la suya (aunque todas
/// comparten el contrato base) para que la inyección de dependencias resuelva el DbContext correcto
/// y no se mezclen las unidades de trabajo de módulos distintos.
/// </summary>
public interface IUnidadDeTrabajoIdentidad : IUnidadDeTrabajo;

using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.Puertos;

/// <summary>
/// Servicio de numeración correlativa. Es el punto de entrada que otros módulos (Facturación) usan
/// para obtener el siguiente número de un documento. La implementación asegura la asignación
/// atómica y sin huecos bloqueando la fila de la serie dentro de la transacción en curso.
/// </summary>
public interface IServicioNumeracion
{
    /// <summary>
    /// Asigna el siguiente número para (empresa + tipo de documento + ejercicio). Debe ejecutarse
    /// dentro de la transacción del documento que se está creando.
    /// </summary>
    Task<Resultado<NumeroDocumento>> SiguienteAsync(
        Guid empresaId,
        TipoDocumento tipoDocumento,
        int ejercicio,
        CancellationToken ct = default);
}

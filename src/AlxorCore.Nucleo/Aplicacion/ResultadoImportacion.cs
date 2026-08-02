namespace AlxorCore.Nucleo.Aplicacion;

/// <summary>Error de una fila concreta durante una importación por lotes (p. ej. CSV).</summary>
public sealed record ErrorFila(int Fila, string Mensaje);

/// <summary>
/// Resultado de una importación por lotes. En modo <b>previsualización</b> valida sin persistir
/// (<see cref="Importadas"/> = 0); al confirmar, importa las filas válidas.
/// </summary>
public sealed record ResultadoImportacion(
    int Total,
    int Correctas,
    int Importadas,
    bool Previsualizacion,
    IReadOnlyList<ErrorFila> Errores);

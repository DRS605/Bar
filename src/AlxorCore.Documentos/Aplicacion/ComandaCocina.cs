namespace AlxorCore.Documentos.Aplicacion;

/// <summary>Una línea de la comanda de cocina: cantidad y qué preparar.</summary>
public sealed record LineaCocina(decimal Cantidad, string Descripcion);

/// <summary>Datos para imprimir una comanda de cocina/barra (sin precios): mesa, hora y qué preparar.</summary>
public sealed record DatosComandaCocina(string Mesa, DateTimeOffset Hora, IReadOnlyList<LineaCocina> Lineas, string? Notas);

/// <summary>Puerto de generación de la comanda de cocina/barra en ESC/POS (impresora térmica).</summary>
public interface IGeneradorComandaCocina
{
    /// <summary>Genera los bytes ESC/POS de la comanda de cocina.</summary>
    byte[] Generar(DatosComandaCocina datos);
}

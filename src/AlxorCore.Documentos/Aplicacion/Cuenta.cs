namespace AlxorCore.Documentos.Aplicacion;

/// <summary>Una línea de la cuenta previa: qué se consumió, a qué precio y su importe.</summary>
public sealed record LineaCuenta(decimal Cantidad, string Descripcion, decimal PrecioUnitario, decimal Total);

/// <summary>
/// Datos para imprimir la <b>cuenta previa</b> (pre-ticket) de una mesa: lo consumido con sus importes,
/// pero <b>sin valor fiscal</b> (no es la factura). Es lo que el cliente pide antes de pagar.
/// </summary>
public sealed record DatosCuenta(
    string Local,
    string Mesa,
    DateTimeOffset Hora,
    IReadOnlyList<LineaCuenta> Lineas,
    decimal Base,
    decimal CuotaIva,
    decimal Total,
    string? Notas);

/// <summary>Puerto de generación de la cuenta previa (pre-ticket) en ESC/POS (impresora térmica).</summary>
public interface IGeneradorCuenta
{
    /// <summary>Genera los bytes ESC/POS de la cuenta previa.</summary>
    byte[] Generar(DatosCuenta datos);
}

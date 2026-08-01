using System.Text;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>Tipo de libro de IVA.</summary>
public enum TipoLibroIva
{
    /// <summary>IVA repercutido (facturas emitidas).</summary>
    Repercutido = 1,

    /// <summary>IVA soportado (gastos / facturas recibidas).</summary>
    Soportado = 2,
}

/// <summary>Asiento (fila) de un libro de IVA.</summary>
public sealed record AsientoIva(DateOnly Fecha, string Documento, string Tercero, string? Nif, decimal Base, decimal Cuota);

/// <summary>Libro de IVA de un periodo, con sus asientos y totales.</summary>
public sealed record LibroIvaDto(
    TipoLibroIva Tipo, DateOnly Desde, DateOnly Hasta, IReadOnlyList<AsientoIva> Asientos, decimal TotalBase, decimal TotalCuota);

/// <summary>Caso de uso: generar un libro de IVA (repercutido o soportado) de un periodo.</summary>
public sealed class GenerarLibroIva
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;

    public GenerarLibroIva(IConsultaFacturas facturas, IConsultaGastos gastos)
    {
        _facturas = facturas;
        _gastos = gastos;
    }

    public async Task<LibroIvaDto> EjecutarAsync(Guid empresaId, TipoLibroIva tipo, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        List<AsientoIva> asientos;

        if (tipo == TipoLibroIva.Repercutido)
        {
            var facturas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
            asientos = facturas
                .Where(f => f.FechaEmision >= desde && f.FechaEmision <= hasta)
                .OrderBy(f => f.FechaEmision).ThenBy(f => f.NumeroCompleto)
                .Select(f => new AsientoIva(f.FechaEmision, f.NumeroCompleto, f.ClienteNombre, f.ClienteNif, f.BaseImponible, f.CuotaIva))
                .ToList();
        }
        else
        {
            var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);
            asientos = gastos
                .Where(g => g.Fecha >= desde && g.Fecha <= hasta)
                .OrderBy(g => g.Fecha)
                .Select(g => new AsientoIva(g.Fecha, g.Concepto, g.ProveedorTexto ?? string.Empty, null, g.BaseImponible, g.CuotaIva))
                .ToList();
        }

        var totalBase = Redondeo.Dos(asientos.Sum(a => a.Base));
        var totalCuota = Redondeo.Dos(asientos.Sum(a => a.Cuota));
        return new LibroIvaDto(tipo, desde, hasta, asientos, totalBase, totalCuota);
    }
}

/// <summary>Genera el CSV de un libro de IVA para la gestoría (separador «;», decimales con coma).</summary>
public static class ExportadorLibroIvaCsv
{
    public static string Generar(LibroIvaDto libro)
    {
        ArgumentNullException.ThrowIfNull(libro);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha;Documento;Tercero;NIF;Base;Cuota IVA");

        foreach (var a in libro.Asientos)
        {
            sb.Append(a.Fecha.ToString("dd/MM/yyyy")).Append(';')
                .Append(Escapar(a.Documento)).Append(';')
                .Append(Escapar(a.Tercero)).Append(';')
                .Append(Escapar(a.Nif ?? string.Empty)).Append(';')
                .Append(Redondeo.Formatear(a.Base)).Append(';')
                .Append(Redondeo.Formatear(a.Cuota)).Append('\n');
        }

        sb.Append("TOTALES;;;;")
            .Append(Redondeo.Formatear(libro.TotalBase)).Append(';')
            .Append(Redondeo.Formatear(libro.TotalCuota)).Append('\n');

        return sb.ToString();
    }

    private static string Escapar(string valor) =>
        valor.Contains(';', StringComparison.Ordinal) || valor.Contains('"', StringComparison.Ordinal)
            ? "\"" + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : valor;
}

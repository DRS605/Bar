using System.Globalization;

namespace AlxorCore.Api.Comun;

/// <summary>Cuerpo de una importación CSV: el contenido y si es solo previsualización.</summary>
public sealed record ImportarCsvPeticion(string Contenido, bool Previsualizar = true);

/// <summary>Utilidades de conversión de valores CSV (números, IVA, tipo de producto).</summary>
public static class ImportacionCsv
{
    /// <summary>Interpreta un número admitiendo coma o punto decimal y símbolos como € o %.</summary>
    public static decimal Numero(string? valor, decimal porDefecto = 0m)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return porDefecto;
        }

        var limpio = valor.Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal) // separador de millares
            .Replace(",", ".", StringComparison.Ordinal)          // coma decimal → punto
            .Trim();

        return decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : porDefecto;
    }

    /// <summary>Normaliza un valor de IVA (21, "21%", "IVA21") al código del catálogo (IVA21…).</summary>
    public static string? CodigoIva(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var v = valor.Trim().ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (v.StartsWith("IVA", StringComparison.Ordinal))
        {
            return v;
        }

        var porcentaje = Numero(v);
        return porcentaje switch
        {
            21m => "IVA21",
            10m => "IVA10",
            4m => "IVA4",
            0m => "IVA0",
            _ => "IVA21",
        };
    }
}

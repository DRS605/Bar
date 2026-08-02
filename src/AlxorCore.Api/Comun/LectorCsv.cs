using System.Globalization;
using System.Text;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Lector de CSV sencillo y robusto para las importaciones: detecta el separador (<c>;</c>, <c>,</c>
/// o tabulador), respeta los campos entrecomillados (con <c>""</c> como comilla escapada y saltos de
/// línea dentro de comillas) y expone cada fila como un diccionario columna→valor por su cabecera.
/// </summary>
public static class LectorCsv
{
    /// <summary>Una fila del CSV: su número de línea (empezando en 2, tras la cabecera) y sus valores por columna.</summary>
    public sealed record FilaCsv(int Numero, IReadOnlyDictionary<string, string> Valores)
    {
        /// <summary>Valor de la primera columna cuyo nombre normalizado coincida con alguno de los alias.</summary>
        public string? Campo(params string[] alias)
        {
            foreach (var a in alias)
            {
                if (Valores.TryGetValue(Normalizar(a), out var v) && !string.IsNullOrWhiteSpace(v))
                {
                    return v.Trim();
                }
            }

            return null;
        }
    }

    /// <summary>Parsea el contenido y devuelve sus filas (la primera línea es la cabecera).</summary>
    public static IReadOnlyList<FilaCsv> Parsear(string contenido)
    {
        var registros = DividirEnRegistros(contenido ?? string.Empty);
        if (registros.Count == 0)
        {
            return [];
        }

        var separador = DetectarSeparador(registros[0]);
        var cabecera = ParsearLinea(registros[0], separador).Select(Normalizar).ToList();

        var filas = new List<FilaCsv>();
        for (var i = 1; i < registros.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(registros[i]))
            {
                continue;
            }

            var campos = ParsearLinea(registros[i], separador);
            var valores = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < cabecera.Count && c < campos.Count; c++)
            {
                valores[cabecera[c]] = campos[c];
            }

            filas.Add(new FilaCsv(i + 1, valores));
        }

        return filas;
    }

    /// <summary>Normaliza un nombre de columna: minúsculas, sin acentos ni espacios sobrantes.</summary>
    public static string Normalizar(string texto)
    {
        var descompuesto = (texto ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static char DetectarSeparador(string cabecera)
    {
        int Contar(char c)
        {
            var n = 0;
            var dentro = false;
            foreach (var ch in cabecera)
            {
                if (ch == '"')
                {
                    dentro = !dentro;
                }
                else if (ch == c && !dentro)
                {
                    n++;
                }
            }

            return n;
        }

        var puntoComa = Contar(';');
        var tab = Contar('\t');
        var coma = Contar(',');
        if (puntoComa >= coma && puntoComa >= tab)
        {
            return ';';
        }

        return tab > coma ? '\t' : ',';
    }

    // Divide el texto en registros por saltos de línea que NO estén dentro de comillas.
    private static List<string> DividirEnRegistros(string contenido)
    {
        var registros = new List<string>();
        var actual = new StringBuilder();
        var dentroComillas = false;
        for (var i = 0; i < contenido.Length; i++)
        {
            var c = contenido[i];
            if (c == '"')
            {
                dentroComillas = !dentroComillas;
                actual.Append(c);
            }
            else if ((c == '\n' || c == '\r') && !dentroComillas)
            {
                if (c == '\r' && i + 1 < contenido.Length && contenido[i + 1] == '\n')
                {
                    i++;
                }

                registros.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }

        if (actual.Length > 0)
        {
            registros.Add(actual.ToString());
        }

        return registros;
    }

    private static List<string> ParsearLinea(string linea, char separador)
    {
        var campos = new List<string>();
        var sb = new StringBuilder();
        var dentroComillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (dentroComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    dentroComillas = !dentroComillas;
                }
            }
            else if (c == separador && !dentroComillas)
            {
                campos.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        campos.Add(sb.ToString());
        return campos;
    }
}

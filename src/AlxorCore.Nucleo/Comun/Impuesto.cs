using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Nucleo.Comun;

/// <summary>Tipo de impuesto.</summary>
public enum TipoImpuesto
{
    /// <summary>IVA repercutido/soportado.</summary>
    Iva = 1,

    /// <summary>Retención de IRPF.</summary>
    Irpf = 2,
}

/// <summary>
/// Catálogo de tipos de IVA españoles. Al ser tipos nacionales y estables, se modelan como
/// constantes de código (no como datos editables por empresa): mantiene la fiscalidad simple y
/// versionada. Las facturas guardan una copia del porcentaje aplicado (snapshot), por lo que un
/// cambio futuro de tipos no altera las facturas ya emitidas.
/// </summary>
public sealed class Impuesto
{
    public static readonly Impuesto IvaGeneral = new("IVA21", TipoImpuesto.Iva, 21m, "IVA general (21%)");
    public static readonly Impuesto IvaReducido = new("IVA10", TipoImpuesto.Iva, 10m, "IVA reducido (10%)");
    public static readonly Impuesto IvaSuperreducido = new("IVA4", TipoImpuesto.Iva, 4m, "IVA superreducido (4%)");
    public static readonly Impuesto IvaExento = new("IVA0", TipoImpuesto.Iva, 0m, "Exento / 0%");

    private static readonly Dictionary<string, Impuesto> PorCodigo =
        new[] { IvaGeneral, IvaReducido, IvaSuperreducido, IvaExento }
            .ToDictionary(i => i.Codigo, StringComparer.OrdinalIgnoreCase);

    private Impuesto(string codigo, TipoImpuesto tipo, decimal porcentaje, string nombre)
    {
        Codigo = codigo;
        Tipo = tipo;
        Porcentaje = porcentaje;
        Nombre = nombre;
    }

    /// <summary>Código estable (p. ej. <c>IVA21</c>).</summary>
    public string Codigo { get; }

    public TipoImpuesto Tipo { get; }

    /// <summary>Porcentaje (p. ej. 21).</summary>
    public decimal Porcentaje { get; }

    /// <summary>Nombre legible en español.</summary>
    public string Nombre { get; }

    /// <summary>Todos los tipos de IVA disponibles.</summary>
    public static IReadOnlyCollection<Impuesto> TodosIva => PorCodigo.Values;

    /// <summary>Resuelve un impuesto por su código.</summary>
    public static Resultado<Impuesto> PorCodigoImpuesto(string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && PorCodigo.TryGetValue(codigo, out var impuesto))
        {
            return Resultado.Ok(impuesto);
        }

        return Resultado.Fallo<Impuesto>(Error.Validacion("impuesto.desconocido", $"El impuesto «{codigo}» no existe."));
    }
}

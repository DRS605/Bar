namespace AlxorCore.Nucleo.Resultados;

/// <summary>
/// Clasificación de un <see cref="Error"/>. Permite traducir el fallo a una respuesta
/// adecuada en los bordes de la aplicación (por ejemplo, un código HTTP) sin que el
/// dominio conozca esos detalles.
/// </summary>
public enum TipoError
{
    /// <summary>Datos de entrada no válidos.</summary>
    Validacion,

    /// <summary>El recurso solicitado no existe.</summary>
    NoEncontrado,

    /// <summary>Conflicto con el estado actual (por ejemplo, un duplicado).</summary>
    Conflicto,

    /// <summary>Falta de autenticación.</summary>
    NoAutenticado,

    /// <summary>Autenticado pero sin permiso.</summary>
    Prohibido,

    /// <summary>Fallo inesperado.</summary>
    Fallo,
}

/// <summary>
/// Representa un error de negocio de forma explícita, evitando el uso de excepciones para
/// flujos esperados. Se compone de un código estable (apto para clientes e i18n) y un
/// mensaje legible en español.
/// </summary>
public sealed record Error(string Codigo, string Mensaje, TipoError Tipo = TipoError.Fallo)
{
    /// <summary>Error nulo usado cuando una operación es correcta.</summary>
    public static readonly Error Ninguno = new(string.Empty, string.Empty, TipoError.Fallo);

    public static Error Validacion(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Validacion);

    public static Error NoEncontrado(string codigo, string mensaje) => new(codigo, mensaje, TipoError.NoEncontrado);

    public static Error Conflicto(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Conflicto);

    public static Error NoAutenticado(string codigo, string mensaje) => new(codigo, mensaje, TipoError.NoAutenticado);

    public static Error Prohibido(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Prohibido);

    public override string ToString() => $"{Codigo}: {Mensaje}";
}

namespace AlxorCore.Nucleo.Resultados;

/// <summary>
/// Resultado de una operación que puede tener éxito o fallar con un <see cref="Error"/>.
/// Es el mecanismo preferido para comunicar fallos esperados a través de las capas, en
/// lugar de lanzar excepciones.
/// </summary>
public class Resultado
{
    protected Resultado(bool esCorrecto, Error error)
    {
        if (esCorrecto && error != Error.Ninguno)
        {
            throw new InvalidOperationException("Un resultado correcto no puede llevar un error.");
        }

        if (!esCorrecto && error == Error.Ninguno)
        {
            throw new InvalidOperationException("Un resultado fallido requiere un error.");
        }

        EsCorrecto = esCorrecto;
        Error = error;
    }

    /// <summary>Indica si la operación tuvo éxito.</summary>
    public bool EsCorrecto { get; }

    /// <summary>Indica si la operación falló.</summary>
    public bool EsFallo => !EsCorrecto;

    /// <summary>Error asociado cuando la operación falla; <see cref="Error.Ninguno"/> si tuvo éxito.</summary>
    public Error Error { get; }

    public static Resultado Ok() => new(true, Error.Ninguno);

    public static Resultado Fallo(Error error) => new(false, error);

    public static Resultado<T> Ok<T>(T valor) => new(valor, true, Error.Ninguno);

    public static Resultado<T> Fallo<T>(Error error) => new(default, false, error);
}

/// <summary>
/// Resultado de una operación que, en caso de éxito, produce un valor de tipo <typeparamref name="T"/>.
/// </summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    internal Resultado(T? valor, bool esCorrecto, Error error)
        : base(esCorrecto, error)
    {
        _valor = valor;
    }

    /// <summary>Valor producido. Acceder a él en un resultado fallido lanza una excepción.</summary>
    public T Valor => EsCorrecto
        ? _valor!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    public static implicit operator Resultado<T>(T valor) => Ok(valor);
}

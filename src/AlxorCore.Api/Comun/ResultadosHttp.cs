using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Traduce los <see cref="Resultado"/> del dominio/aplicación a respuestas HTTP uniformes,
/// usando <c>ProblemDetails</c> para los fallos (RFC 7807). Mantiene la API coherente y evita
/// repetir el mapeo en cada endpoint.
/// </summary>
public static class ResultadosHttp
{
    /// <summary>Devuelve 200 con el valor, o el problema correspondiente.</summary>
    public static IResult AOk<T>(this Resultado<T> resultado) =>
        resultado.EsCorrecto ? Results.Ok(resultado.Valor) : AProblema(resultado.Error);

    /// <summary>Devuelve 201 (con cabecera Location) con el valor, o el problema correspondiente.</summary>
    public static IResult ACreado<T>(this Resultado<T> resultado, string ubicacion) =>
        resultado.EsCorrecto ? Results.Created(ubicacion, resultado.Valor) : AProblema(resultado.Error);

    /// <summary>Devuelve 204 si la operación fue correcta, o el problema correspondiente.</summary>
    public static IResult ASinContenido(this Resultado resultado) =>
        resultado.EsCorrecto ? Results.NoContent() : AProblema(resultado.Error);

    /// <summary>Construye una respuesta de problema (ProblemDetails) a partir de un <see cref="Error"/>.</summary>
    public static IResult AProblema(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var estado = error.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoError.Prohibido => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Mensaje,
            statusCode: estado,
            extensions: new Dictionary<string, object?> { ["codigo"] = error.Codigo });
    }
}

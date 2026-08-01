namespace AlxorCore.Nucleo.Dominio;

/// <summary>
/// Excepción que señala la violación de una invariante del dominio que nunca debería
/// alcanzarse a través de un flujo válido (error de programación, no de datos de usuario).
/// Los fallos esperados de negocio se comunican con <c>Resultado</c>, no con excepciones.
/// </summary>
public sealed class ExcepcionDominio : Exception
{
    public ExcepcionDominio(string mensaje)
        : base(mensaje)
    {
    }
}

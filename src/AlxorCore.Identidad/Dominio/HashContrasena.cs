using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Contraseña ya cifrada (hash). El dominio nunca maneja la contraseña en claro: la función
/// de hashing vive en la infraestructura (puerto <c>IHasherContrasena</c>) y aquí solo se
/// guarda el resultado opaco. Es un value object.
/// </summary>
public sealed record HashContrasena
{
    private HashContrasena(string valor) => Valor = valor;

    /// <summary>Representación opaca del hash (algoritmo + sal + hash), tal como la produce el hasher.</summary>
    public string Valor { get; }

    /// <summary>Envuelve un hash ya calculado por la infraestructura.</summary>
    public static HashContrasena DesdeHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ExcepcionDominio("El hash de contraseña no puede estar vacío.");
        }

        return new HashContrasena(hash);
    }

    public override string ToString() => "«hash oculto»";
}

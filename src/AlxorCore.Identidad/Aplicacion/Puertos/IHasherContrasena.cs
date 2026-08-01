namespace AlxorCore.Identidad.Aplicacion.Puertos;

/// <summary>
/// Puerto de cifrado de contraseñas. Aísla el algoritmo de hashing (implementado en la
/// infraestructura) del resto de la aplicación, permitiendo cambiarlo sin tocar el dominio.
/// </summary>
public interface IHasherContrasena
{
    /// <summary>Calcula el hash de una contraseña en claro.</summary>
    string Hash(string contrasena);

    /// <summary>Verifica una contraseña en claro contra un hash previamente calculado.</summary>
    bool Verificar(string hash, string contrasena);
}

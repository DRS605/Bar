using AlxorCore.Identidad.Aplicacion.Puertos;
using Microsoft.AspNetCore.Identity;

namespace AlxorCore.Identidad.Infraestructura.Seguridad;

/// <summary>
/// Implementación de <see cref="IHasherContrasena"/> basada en el <see cref="PasswordHasher{TUser}"/>
/// de ASP.NET Core Identity (PBKDF2 con sal por contraseña y factor de trabajo configurable).
/// </summary>
internal sealed class HasherContrasenaIdentity : IHasherContrasena
{
    // El PasswordHasher exige una instancia de "usuario" que en realidad no utiliza para el
    // algoritmo; usamos un marcador compartido.
    private static readonly object Marcador = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string contrasena) => _hasher.HashPassword(Marcador, contrasena);

    public bool Verificar(string hash, string contrasena)
    {
        var resultado = _hasher.VerifyHashedPassword(Marcador, hash, contrasena);
        return resultado is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

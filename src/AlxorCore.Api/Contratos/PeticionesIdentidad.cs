namespace AlxorCore.Api.Contratos;

/// <summary>Cuerpo de la petición de registro.</summary>
public sealed record RegistroPeticion(string Email, string Nombre, string Contrasena);

/// <summary>Cuerpo de la petición de inicio de sesión.</summary>
public sealed record LoginPeticion(string Email, string Contrasena);

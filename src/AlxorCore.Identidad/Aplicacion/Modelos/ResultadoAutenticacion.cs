namespace AlxorCore.Identidad.Aplicacion.Modelos;

/// <summary>Resultado de un inicio de sesión correcto: el token y el perfil del usuario.</summary>
public sealed record ResultadoAutenticacion(string Token, DateTimeOffset ExpiraEn, PerfilUsuario Usuario);

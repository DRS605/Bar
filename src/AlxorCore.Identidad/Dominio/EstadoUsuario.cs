namespace AlxorCore.Identidad.Dominio;

/// <summary>Estado de la cuenta de un usuario.</summary>
public enum EstadoUsuario
{
    /// <summary>Cuenta operativa: puede autenticarse.</summary>
    Activo = 1,

    /// <summary>Cuenta suspendida: no puede autenticarse.</summary>
    Suspendido = 2,
}

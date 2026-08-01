namespace AlxorCore.Nucleo.Seguridad;

/// <summary>Nombres de los claims propios de ALXOR Core incrustados en el token de acceso.</summary>
public static class ClaimsAlxor
{
    /// <summary>Empresa (tenant) activa del usuario.</summary>
    public const string EmpresaId = "empresa_id";

    /// <summary>Código del rol del usuario en la empresa activa.</summary>
    public const string Rol = "rol";

    /// <summary>Permiso concedido (puede aparecer varias veces, uno por permiso).</summary>
    public const string Permiso = "permiso";
}

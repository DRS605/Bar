using AlxorCore.Nucleo.Seguridad;

namespace AlxorCore.Api.Comun;

/// <summary>Extensiones para exigir permisos concretos en los endpoints.</summary>
public static class AutorizacionPermisos
{
    /// <summary>
    /// Exige que el usuario tenga el permiso indicado (un claim <c>permiso</c> con ese valor,
    /// presente en el token con alcance de empresa). Implica autenticación.
    /// </summary>
    public static TBuilder RequierePermiso<TBuilder>(this TBuilder builder, string permiso)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(politica => politica.RequireClaim(ClaimsAlxor.Permiso, permiso));
        return builder;
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AlxorCore.Auditoria;
using AlxorCore.Nucleo.Multiempresa;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Registra en la auditoría cada petición que <b>modifica datos</b> (POST/PUT/PATCH/DELETE) de un
/// usuario autenticado con empresa activa: quién, qué acción, sobre qué ruta, con qué resultado y
/// cuándo. Nunca interrumpe la petición: si la auditoría falla, la operación sigue su curso.
/// </summary>
public sealed class MiddlewareAuditoria
{
    private static readonly HashSet<string> MetodosMutantes = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _siguiente;

    public MiddlewareAuditoria(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        await _siguiente(contexto).ConfigureAwait(false);

        var metodo = contexto.Request.Method;
        var ruta = contexto.Request.Path.Value ?? string.Empty;
        if (!MetodosMutantes.Contains(metodo) || ruta.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var contextoEmpresa = contexto.RequestServices.GetService<IContextoEmpresa>();
        if (contextoEmpresa?.EmpresaId is not { } empresaId)
        {
            return;
        }

        try
        {
            var usuario = contexto.User;
            var usuarioId = usuario.ObtenerUsuarioId();
            var nombre = usuario.FindFirstValue("nombre")
                ?? usuario.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? "—";

            var registro = RegistroAuditoria.Crear(
                empresaId, usuarioId, nombre, DescribirAccion(metodo, ruta), metodo, ruta, contexto.Response.StatusCode, DateTimeOffset.UtcNow);

            var repositorio = contexto.RequestServices.GetRequiredService<IRepositorioAuditoria>();
            await repositorio.RegistrarAsync(registro, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // La auditoría es accesoria: un fallo suyo nunca debe tumbar la petición del usuario.
        catch (Exception)
        {
            // Silenciado a propósito.
        }
#pragma warning restore CA1031
    }

    private static string DescribirAccion(string metodo, string ruta)
    {
        var recurso = ruta.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } partes ? partes[0] : "recurso";
        var verbo = metodo.ToUpperInvariant() switch
        {
            "POST" => "Alta",
            "PUT" => "Modificación",
            "PATCH" => "Cambio",
            "DELETE" => "Baja",
            _ => metodo,
        };
        return $"{verbo} en {recurso}";
    }
}

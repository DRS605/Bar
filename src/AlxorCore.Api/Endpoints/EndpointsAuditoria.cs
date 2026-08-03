using AlxorCore.Api.Comun;
using AlxorCore.Auditoria;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Auditoría.</summary>
public static class EndpointsAuditoria
{
    public static IEndpointRouteBuilder MapearAuditoria(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapGet("/auditoria", RecientesAsync)
            .WithTags("Auditoría")
            .WithSummary("Actividad reciente de la empresa (quién hizo qué y cuándo).")
            .RequierePermiso(Permisos.InformeLeer);

        return rutas;
    }

    private static async Task<IResult> RecientesAsync(IContextoEmpresa contexto, IConsultaAuditoria consulta, CancellationToken ct, int limite = 100)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        limite = Math.Clamp(limite, 1, 500);
        return Results.Ok(await consulta.RecientesAsync(contexto.EmpresaId.Value, limite, ct).ConfigureAwait(false));
    }
}

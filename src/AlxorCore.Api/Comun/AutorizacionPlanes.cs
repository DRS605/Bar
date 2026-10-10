using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Comun;

/// <summary>Extensiones para exigir un plan (tarifa) concreto en los endpoints.</summary>
public static class AutorizacionPlanes
{
    /// <summary>
    /// Exige que el local esté en un plan con funciones «Pro» (carta QR con autopedido, comandas a
    /// cocina y reservas). Si está en el plan Essential, responde <c>403</c> con el código
    /// <c>plan.requiere_pro</c>, que la interfaz usa para ofrecer la mejora de plan. Debe combinarse
    /// con la autorización del endpoint (requiere empresa seleccionada).
    /// </summary>
    public static TBuilder RequierePlanPro<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (contexto, siguiente) =>
        {
            var servicios = contexto.HttpContext.RequestServices;
            var empresa = servicios.GetRequiredService<IContextoEmpresa>();
            if (empresa.EmpresaId is null)
            {
                return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
            }

            var planes = servicios.GetRequiredService<IConsultaPlanBar>();
            var plan = await planes.ObtenerPlanAsync(empresa.EmpresaId.Value, contexto.HttpContext.RequestAborted).ConfigureAwait(false);
            if (!PlanesBar.IncluyeFuncionesPro(plan))
            {
                return ResultadosHttp.AProblema(Error.Prohibido("plan.requiere_pro",
                    "Esta función está incluida en el plan Pro. Mejora el plan del local para activarla."));
            }

            return await siguiente(contexto).ConfigureAwait(false);
        });
        return builder;
    }
}

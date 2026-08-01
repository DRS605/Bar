using AlxorCore.Api.Comun;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Gastos.</summary>
public static class EndpointsGastos
{
    public static IEndpointRouteBuilder MapearGastos(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var gastos = rutas.MapGroup("/gastos").WithTags("Gastos");

        gastos.MapGet("", ListarAsync)
            .WithSummary("Lista los gastos de la empresa activa.")
            .RequierePermiso(Permisos.GastoLeer);

        gastos.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un gasto.")
            .RequierePermiso(Permisos.GastoLeer);

        gastos.MapPost("", RegistrarAsync)
            .WithSummary("Registra un gasto.")
            .RequierePermiso(Permisos.GastoGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarGastos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerGasto caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> RegistrarAsync(RegistrarGastoComando comando, IContextoEmpresa contexto, RegistrarGasto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/gastos/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }
}

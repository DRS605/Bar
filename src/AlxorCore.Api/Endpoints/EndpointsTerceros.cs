using AlxorCore.Api.Comun;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Terceros (clientes).</summary>
public static class EndpointsTerceros
{
    public static IEndpointRouteBuilder MapearTerceros(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var clientes = rutas.MapGroup("/clientes").WithTags("Clientes");

        clientes.MapGet("", ListarAsync)
            .WithSummary("Lista los clientes de la empresa activa.")
            .RequireAuthorization();

        clientes.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene un cliente.")
            .RequireAuthorization();

        clientes.MapPost("", CrearAsync)
            .WithSummary("Crea un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        clientes.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza un cliente.")
            .RequierePermiso(Permisos.ClienteGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarClientes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerCliente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosCliente datos, IContextoEmpresa contexto, CrearCliente caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/clientes/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosCliente datos, ActualizarCliente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();
}

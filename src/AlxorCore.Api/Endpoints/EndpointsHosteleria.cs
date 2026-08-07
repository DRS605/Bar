using AlxorCore.Api.Comun;
using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Hostelería (mesas y comandas del TPV de barra/salón).</summary>
public static class EndpointsHosteleria
{
    public static IEndpointRouteBuilder MapearHosteleria(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var mesas = rutas.MapGroup("/mesas").WithTags("Mesas");

        mesas.MapGet("", ListarMesasAsync)
            .WithSummary("Lista las mesas de la empresa activa con su ocupación.")
            .RequireAuthorization();

        mesas.MapPost("", CrearMesaAsync)
            .WithSummary("Crea una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapPut("/{id:guid}", ActualizarMesaAsync)
            .WithSummary("Actualiza una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapDelete("/{id:guid}", DesactivarMesaAsync)
            .WithSummary("Retira (desactiva) una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var comandas = rutas.MapGroup("/comandas").WithTags("Comandas");

        comandas.MapGet("", ListarComandasAsync)
            .WithSummary("Lista las comandas abiertas de la empresa activa.")
            .RequireAuthorization();

        comandas.MapGet("/{id:guid}", ObtenerComandaAsync)
            .WithSummary("Obtiene una comanda con sus líneas.")
            .RequireAuthorization();

        comandas.MapPost("", AbrirComandaAsync)
            .WithSummary("Abre una comanda en una mesa libre.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/lineas", AgregarLineaAsync)
            .WithSummary("Añade un producto a la comanda.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapDelete("/{id:guid}/lineas/{lineaId:guid}", QuitarLineaAsync)
            .WithSummary("Quita una línea de la comanda.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/cobrar", CobrarComandaAsync)
            .WithSummary("Cobra la comanda emitiendo un ticket y libera la mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/anular", AnularComandaAsync)
            .WithSummary("Anula la comanda sin cobrarla.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarMesasAsync(IContextoEmpresa contexto, ListarMesas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearMesaAsync(DatosMesa datos, IContextoEmpresa contexto, CrearMesa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/mesas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarMesaAsync(Guid id, DatosMesa datos, ActualizarMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DesactivarMesaAsync(Guid id, DesactivarMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarComandasAsync(IContextoEmpresa contexto, ListarComandasAbiertas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerComandaAsync(Guid id, ObtenerComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AbrirComandaAsync(DatosAbrirComanda datos, IContextoEmpresa contexto, AbrirComanda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/comandas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> AgregarLineaAsync(Guid id, DatosLineaComanda datos, AgregarLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> QuitarLineaAsync(Guid id, Guid lineaId, QuitarLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, lineaId, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CobrarComandaAsync(Guid id, DatosCobro datos, IContextoEmpresa contexto, CobrarComanda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, id, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> AnularComandaAsync(Guid id, AnularComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();
}

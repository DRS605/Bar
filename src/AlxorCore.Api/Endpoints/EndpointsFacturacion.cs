using AlxorCore.Api.Comun;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Facturación.</summary>
public static class EndpointsFacturacion
{
    public static IEndpointRouteBuilder MapearFacturacion(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var facturas = rutas.MapGroup("/facturas").WithTags("Facturas");

        facturas.MapPost("", EmitirAsync)
            .WithSummary("Emite una factura.")
            .RequierePermiso(Permisos.FacturaEmitir);

        facturas.MapGet("", ListarAsync)
            .WithSummary("Lista las facturas de la empresa activa.")
            .RequierePermiso(Permisos.FacturaLeer);

        facturas.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene una factura con sus líneas.")
            .RequierePermiso(Permisos.FacturaLeer);

        var recurrentes = rutas.MapGroup("/facturas-recurrentes").WithTags("Facturación periódica");

        recurrentes.MapGet("", ListarRecurrentesAsync)
            .WithSummary("Lista las facturas recurrentes (suscripciones) de la empresa activa.")
            .RequierePermiso(Permisos.FacturaLeer);

        recurrentes.MapGet("/{id:guid}", ObtenerRecurrenteAsync)
            .WithSummary("Obtiene una factura recurrente con su plantilla de líneas.")
            .RequierePermiso(Permisos.FacturaLeer);

        recurrentes.MapPost("", CrearRecurrenteAsync)
            .WithSummary("Crea una factura recurrente (facturación automática periódica).")
            .RequierePermiso(Permisos.FacturaEmitir);

        recurrentes.MapPut("/{id:guid}", ActualizarRecurrenteAsync)
            .WithSummary("Actualiza una factura recurrente.")
            .RequierePermiso(Permisos.FacturaEmitir);

        recurrentes.MapPost("/{id:guid}/estado", CambiarEstadoRecurrenteAsync)
            .WithSummary("Activa o pausa una factura recurrente.")
            .RequierePermiso(Permisos.FacturaEmitir);

        recurrentes.MapPost("/procesar", ProcesarRecurrentesAsync)
            .WithSummary("Emite ahora todas las facturas recurrentes vencidas de la empresa activa.")
            .RequierePermiso(Permisos.FacturaEmitir);

        return rutas;
    }

    private static async Task<IResult> ListarRecurrentesAsync(IContextoEmpresa contexto, ListarFacturasRecurrentes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerRecurrenteAsync(Guid id, ObtenerFacturaRecurrente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearRecurrenteAsync(DatosFacturaRecurrente datos, IContextoEmpresa contexto, CrearFacturaRecurrente caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas-recurrentes/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarRecurrenteAsync(Guid id, DatosFacturaRecurrente datos, ActualizarFacturaRecurrente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CambiarEstadoRecurrenteAsync(Guid id, CambioEstadoRecurrente cuerpo, CambiarEstadoFacturaRecurrente caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, cuerpo.Activa, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> ProcesarRecurrentesAsync(IContextoEmpresa contexto, EmitirFacturasRecurrentesVencidas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> EmitirAsync(EmitirFacturaComando comando, IContextoEmpresa contexto, EmitirFactura caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarFacturas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerFactura caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();
}

/// <summary>Cuerpo para activar/pausar una factura recurrente.</summary>
public sealed record CambioEstadoRecurrente(bool Activa);

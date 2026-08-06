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

        facturas.MapGet("/{id:guid}/verifactu.xml", VerifactuXmlAsync)
            .WithSummary("Descarga el registro de alta VeriFactu (XML) de la factura.")
            .RequierePermiso(Permisos.FacturaLeer);

        rutas.MapPost("/tickets", EmitirTicketAsync)
            .WithTags("TPV / Tickets")
            .WithSummary("Emite un ticket (factura simplificada) desde el TPV.")
            .RequierePermiso(Permisos.FacturaEmitir);

        facturas.MapPost("/{id:guid}/rectificar", RectificarAsync)
            .WithSummary("Emite una factura rectificativa que corrige a esta factura.")
            .RequierePermiso(Permisos.FacturaEmitir);

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

        var presupuestos = rutas.MapGroup("/presupuestos").WithTags("Presupuestos");

        presupuestos.MapGet("", ListarPresupuestosAsync)
            .WithSummary("Lista los presupuestos de la empresa activa.")
            .RequierePermiso(Permisos.FacturaLeer);

        presupuestos.MapGet("/{id:guid}", ObtenerPresupuestoAsync)
            .WithSummary("Obtiene un presupuesto con sus líneas.")
            .RequierePermiso(Permisos.FacturaLeer);

        presupuestos.MapPost("", CrearPresupuestoAsync)
            .WithSummary("Crea un presupuesto.")
            .RequierePermiso(Permisos.FacturaEmitir);

        presupuestos.MapPut("/{id:guid}", ActualizarPresupuestoAsync)
            .WithSummary("Actualiza un presupuesto en borrador.")
            .RequierePermiso(Permisos.FacturaEmitir);

        presupuestos.MapPost("/{id:guid}/aceptar", AceptarPresupuestoAsync)
            .WithSummary("Acepta el presupuesto y lo convierte en factura.")
            .RequierePermiso(Permisos.FacturaEmitir);

        presupuestos.MapPost("/{id:guid}/rechazar", RechazarPresupuestoAsync)
            .WithSummary("Marca el presupuesto como rechazado.")
            .RequierePermiso(Permisos.FacturaEmitir);

        return rutas;
    }

    private static async Task<IResult> RectificarAsync(Guid id, EmitirRectificativaComando comando, IContextoEmpresa contexto, EmitirRectificativa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, id, comando, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ListarPresupuestosAsync(IContextoEmpresa contexto, ListarPresupuestos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerPresupuestoAsync(Guid id, ObtenerPresupuesto caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearPresupuestoAsync(DatosPresupuesto datos, IContextoEmpresa contexto, CrearPresupuesto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/presupuestos/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarPresupuestoAsync(Guid id, DatosPresupuesto datos, ActualizarPresupuesto caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AceptarPresupuestoAsync(Guid id, AceptarPresupuestoCuerpo? cuerpo, IContextoEmpresa contexto, AceptarPresupuesto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, id, cuerpo?.Serie, cuerpo?.DiasVencimiento ?? 0, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> RechazarPresupuestoAsync(Guid id, RechazarPresupuesto caso, CancellationToken ct)
    {
        var r = await caso.EjecutarAsync(id, ct).ConfigureAwait(false);
        return r.EsCorrecto ? Results.Ok() : ResultadosHttp.AProblema(r.Error);
    }

    private static async Task<IResult> EmitirTicketAsync(EmitirTicketComando comando, IContextoEmpresa contexto, EmitirTicket caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, comando, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/facturas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
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

    private static async Task<IResult> VerifactuXmlAsync(
        Guid id, IContextoEmpresa contexto, IConsultaFacturas facturas,
        AlxorCore.Organizacion.Aplicacion.Puertos.IConsultaEmpresas empresas, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var factura = await facturas.ObtenerAsync(id, ct).ConfigureAwait(false);
        if (factura is null)
        {
            return ResultadosHttp.AProblema(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."));
        }

        var emisor = await empresas.ObtenerAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false);
        if (emisor is null)
        {
            return ResultadosHttp.AProblema(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        var xml = AlxorCore.Api.Comun.GeneradorXmlVerifactu.Generar(factura, emisor);
        return Results.Text(xml, "application/xml");
    }
}

/// <summary>Cuerpo para activar/pausar una factura recurrente.</summary>
public sealed record CambioEstadoRecurrente(bool Activa);

/// <summary>Opciones al aceptar un presupuesto (serie y vencimiento de la factura resultante).</summary>
public sealed record AceptarPresupuestoCuerpo(string? Serie = null, int DiasVencimiento = 0);

using System.Text;
using AlxorCore.Api.Comun;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Reservas.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Reservas (agenda de reservas y calendario iCalendar).</summary>
public static class EndpointsReservas
{
    private const string TipoICal = "text/calendar; charset=utf-8";

    public static IEndpointRouteBuilder MapearReservas(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var reservas = rutas.MapGroup("/reservas").WithTags("Reservas");

        reservas.MapGet("", ListarAsync)
            .WithSummary("Lista las reservas de la empresa (opcionalmente por rango de fechas).")
            .RequireAuthorization();

        reservas.MapGet("/{id:guid}", ObtenerAsync)
            .WithSummary("Obtiene una reserva.")
            .RequireAuthorization();

        reservas.MapGet("/{id:guid}/ical", DescargarICalAsync)
            .WithSummary("Descarga la reserva como archivo iCalendar (.ics) para el calendario.")
            .RequireAuthorization();

        reservas.MapGet("/agenda", ObtenerAgendaAsync)
            .WithSummary("Devuelve el enlace suscribible (iCal) de la agenda de la empresa.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/agenda/regenerar", RegenerarAgendaAsync)
            .WithSummary("Regenera el enlace de calendario (invalida el anterior).")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("", CrearAsync)
            .WithSummary("Crea una reserva.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPut("/{id:guid}", ActualizarAsync)
            .WithSummary("Actualiza una reserva pendiente o confirmada.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/{id:guid}/confirmar", (Guid id, CambiarEstadoReserva caso, CancellationToken ct) => CambiarAsync(id, TransicionReserva.Confirmar, caso, ct))
            .WithSummary("Confirma una reserva.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/{id:guid}/cancelar", (Guid id, CambiarEstadoReserva caso, CancellationToken ct) => CambiarAsync(id, TransicionReserva.Cancelar, caso, ct))
            .WithSummary("Cancela una reserva.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/{id:guid}/no-show", (Guid id, CambiarEstadoReserva caso, CancellationToken ct) => CambiarAsync(id, TransicionReserva.NoShow, caso, ct))
            .WithSummary("Marca una reserva como no presentada.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/{id:guid}/sentar", SentarAsync)
            .WithSummary("Sienta la reserva y, si tiene mesa, abre su comanda.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapPost("/recordatorios/procesar", ProcesarRecordatoriosAsync)
            .WithSummary("Envía ahora los recordatorios de las reservas próximas de la empresa.")
            .RequierePermiso(Permisos.ReservaGestionar);

        reservas.MapGet("/disponibilidad", DisponibilidadAsync)
            .WithSummary("Ocupación (aforo usado/libre) de los turnos en una fecha.")
            .RequireAuthorization();

        var turnos = rutas.MapGroup("/turnos").WithTags("Turnos");

        turnos.MapGet("", ListarTurnosAsync)
            .WithSummary("Lista los turnos (horarios) de la empresa.")
            .RequireAuthorization();

        turnos.MapPost("", CrearTurnoAsync)
            .WithSummary("Crea un turno de servicio.")
            .RequierePermiso(Permisos.ReservaGestionar);

        turnos.MapPut("/{id:guid}", ActualizarTurnoAsync)
            .WithSummary("Actualiza un turno.")
            .RequierePermiso(Permisos.ReservaGestionar);

        turnos.MapDelete("/{id:guid}", DesactivarTurnoAsync)
            .WithSummary("Retira (desactiva) un turno.")
            .RequierePermiso(Permisos.ReservaGestionar);

        // Feed público suscribible: la credencial es el token del enlace (sin sesión).
        rutas.MapGet("/agenda/{token}.ics", FeedAsync)
            .WithTags("Reservas")
            .WithSummary("Calendario iCalendar suscribible de la agenda (Google, Apple, Outlook).")
            .AllowAnonymous();

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IContextoEmpresa contexto, ListarReservas caso, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, desde, hasta, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ObtenerReserva caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CrearAsync(DatosReserva datos, IContextoEmpresa contexto, CrearReserva caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/reservas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(Guid id, DatosReserva datos, ActualizarReserva caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CambiarAsync(Guid id, TransicionReserva transicion, CambiarEstadoReserva caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, transicion, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> SentarAsync(Guid id, IContextoEmpresa contexto, SentarReserva caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, id, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> DescargarICalAsync(Guid id, ObtenerReserva caso, IReloj reloj, CancellationToken ct)
    {
        var reserva = await caso.EjecutarAsync(id, ct).ConfigureAwait(false);
        if (reserva.EsFallo)
        {
            return ResultadosHttp.AProblema(reserva.Error);
        }

        var ical = GeneradorICal.Generar(new[] { reserva.Valor }, "Reserva", reloj.AhoraUtc);
        return Results.File(Encoding.UTF8.GetBytes(ical), TipoICal, $"reserva-{id}.ics");
    }

    private static async Task<IResult> ObtenerAgendaAsync(HttpContext http, IContextoEmpresa contexto, ObtenerAgenda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var token = await caso.EjecutarAsync(contexto.EmpresaId.Value, regenerar: false, ct).ConfigureAwait(false);
        return Results.Ok(EnlaceAgenda(http, token));
    }

    private static async Task<IResult> RegenerarAgendaAsync(HttpContext http, IContextoEmpresa contexto, ObtenerAgenda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var token = await caso.EjecutarAsync(contexto.EmpresaId.Value, regenerar: true, ct).ConfigureAwait(false);
        return Results.Ok(EnlaceAgenda(http, token));
    }

    private static async Task<IResult> ProcesarRecordatoriosAsync(IContextoEmpresa contexto, EnviarRecordatoriosReservas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var enviados = await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false);
        return Results.Ok(new { enviados });
    }

    private static async Task<IResult> DisponibilidadAsync(IContextoEmpresa contexto, ObtenerDisponibilidad caso, IReloj reloj, DateOnly? dia, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var fecha = dia ?? DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime);
        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, fecha, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ListarTurnosAsync(IContextoEmpresa contexto, ListarTurnos caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearTurnoAsync(DatosTurno datos, IContextoEmpresa contexto, CrearTurno caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/turnos/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarTurnoAsync(Guid id, DatosTurno datos, ActualizarTurno caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DesactivarTurnoAsync(Guid id, DesactivarTurno caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> FeedAsync(string token, FeedCalendario caso, CancellationToken ct)
    {
        var ical = await caso.EjecutarAsync(token, ct).ConfigureAwait(false);
        return ical.EsCorrecto ? Results.Text(ical.Valor, TipoICal) : ResultadosHttp.AProblema(ical.Error);
    }

    private static object EnlaceAgenda(HttpContext http, string token)
    {
        var ruta = $"/agenda/{token}.ics";
        var absoluta = $"{http.Request.Scheme}://{http.Request.Host}{ruta}";
        return new { token, ruta, url = absoluta };
    }
}

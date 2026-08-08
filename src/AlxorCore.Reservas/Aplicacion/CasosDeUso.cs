using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Reservas.Dominio;

namespace AlxorCore.Reservas.Aplicacion;

/// <summary>Datos de una reserva para crear o actualizar.</summary>
public sealed record DatosReserva(
    string NombreCliente,
    DateTimeOffset FechaHora,
    int Comensales,
    string? Telefono = null,
    string? Email = null,
    int DuracionMinutos = 120,
    Guid? MesaId = null,
    string? Notas = null);

/// <summary>Localiza el rango [00:00, 24:00) del día de un momento, en su misma zona horaria.</summary>
internal static class RangoDia
{
    public static (DateTimeOffset Desde, DateTimeOffset Hasta) De(DateTimeOffset cuando)
    {
        var inicio = new DateTimeOffset(cuando.Year, cuando.Month, cuando.Day, 0, 0, 0, cuando.Offset);
        return (inicio, inicio.AddDays(1));
    }
}

/// <summary>Caso de uso: crear una reserva (validando turnos y aforo si están definidos).</summary>
public sealed class CrearReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly IRepositorioTurnos _turnos;
    private readonly IConsultaReservas _consulta;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearReserva(IRepositorioReservas reservas, IRepositorioTurnos turnos, IConsultaReservas consulta, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
        _turnos = turnos;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid empresaId, DatosReserva datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var disponible = await ValidacionAgenda.ComprobarAsync(_turnos, _consulta, empresaId, datos.FechaHora, datos.Comensales, null, ct).ConfigureAwait(false);
        if (disponible.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(disponible.Error);
        }

        var reserva = Reserva.Crear(empresaId, datos.NombreCliente, datos.Telefono, datos.Email, datos.FechaHora, datos.DuracionMinutos, datos.Comensales, datos.MesaId, datos.Notas, _reloj);
        if (reserva.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(reserva.Error);
        }

        _reservas.Agregar(reserva.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ReservaDto.Desde(reserva.Valor));
    }
}

/// <summary>Comprobación de disponibilidad reutilizada al crear y editar reservas.</summary>
internal static class ValidacionAgenda
{
    public static async Task<Resultado> ComprobarAsync(IRepositorioTurnos turnos, IConsultaReservas consulta, Guid empresaId, DateTimeOffset cuando, int comensales, Guid? excluir, CancellationToken ct)
    {
        var activos = await turnos.ListarActivosAsync(empresaId, ct).ConfigureAwait(false);
        if (activos.Count == 0)
        {
            return Resultado.Ok();
        }

        var (desde, hasta) = RangoDia.De(cuando);
        var delDia = await consulta.ListarAsync(empresaId, desde, hasta, ct).ConfigureAwait(false);
        return DisponibilidadTurnos.Validar(activos, delDia, cuando, comensales, excluir);
    }
}

/// <summary>Caso de uso: actualizar una reserva pendiente o confirmada.</summary>
public sealed class ActualizarReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly IRepositorioTurnos _turnos;
    private readonly IConsultaReservas _consulta;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarReserva(IRepositorioReservas reservas, IRepositorioTurnos turnos, IConsultaReservas consulta, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
        _turnos = turnos;
        _consulta = consulta;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid reservaId, DatosReserva datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, ct).ConfigureAwait(false);
        if (reserva is null)
        {
            return Resultado.Fallo<ReservaDto>(Error.NoEncontrado("reserva.no_encontrada", "La reserva no existe."));
        }

        var disponible = await ValidacionAgenda.ComprobarAsync(_turnos, _consulta, reserva.EmpresaId, datos.FechaHora, datos.Comensales, reserva.Id, ct).ConfigureAwait(false);
        if (disponible.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(disponible.Error);
        }

        var r = reserva.Actualizar(datos.NombreCliente, datos.Telefono, datos.Email, datos.FechaHora, datos.DuracionMinutos, datos.Comensales, datos.MesaId, datos.Notas, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ReservaDto.Desde(reserva));
    }
}

/// <summary>Transición de estado que no abre comanda: confirmar, cancelar o marcar no-show.</summary>
public enum TransicionReserva
{
    Confirmar,
    Cancelar,
    NoShow,
}

/// <summary>Caso de uso: cambiar el estado de una reserva (sin sentarla).</summary>
public sealed class CambiarEstadoReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CambiarEstadoReserva(IRepositorioReservas reservas, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid reservaId, TransicionReserva transicion, CancellationToken ct = default)
    {
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, ct).ConfigureAwait(false);
        if (reserva is null)
        {
            return Resultado.Fallo<ReservaDto>(Error.NoEncontrado("reserva.no_encontrada", "La reserva no existe."));
        }

        var r = transicion switch
        {
            TransicionReserva.Confirmar => reserva.Confirmar(_reloj),
            TransicionReserva.Cancelar => reserva.Cancelar(_reloj),
            TransicionReserva.NoShow => reserva.MarcarNoShow(_reloj),
            _ => Resultado.Fallo(Error.Validacion("reserva.transicion_invalida", "Transición desconocida.")),
        };

        if (r.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ReservaDto.Desde(reserva));
    }
}

/// <summary>
/// Caso de uso: sentar una reserva. Si tiene mesa asignada y está libre, abre su comanda (Hostelería)
/// y la deja lista para pedir; en cualquier caso marca la reserva como sentada.
/// </summary>
public sealed class SentarReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly AbrirComanda _abrirComanda;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public SentarReserva(IRepositorioReservas reservas, AbrirComanda abrirComanda, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
        _abrirComanda = abrirComanda;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid empresaId, Guid reservaId, CancellationToken ct = default)
    {
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, ct).ConfigureAwait(false);
        if (reserva is null)
        {
            return Resultado.Fallo<ReservaDto>(Error.NoEncontrado("reserva.no_encontrada", "La reserva no existe."));
        }

        if (!reserva.EsModificable)
        {
            return Resultado.Fallo<ReservaDto>(Error.Conflicto("reserva.no_sentable", "Solo se puede sentar una reserva pendiente o confirmada."));
        }

        Guid? comandaId = null;
        if (reserva.MesaId is not null)
        {
            var comanda = await _abrirComanda.EjecutarAsync(empresaId, new DatosAbrirComanda(reserva.MesaId.Value, reserva.Notas), ct).ConfigureAwait(false);
            if (comanda.EsFallo)
            {
                return Resultado.Fallo<ReservaDto>(comanda.Error);
            }

            comandaId = comanda.Valor.Id;
        }

        var r = reserva.Sentar(comandaId, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ReservaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ReservaDto.Desde(reserva));
    }
}

/// <summary>Caso de uso: listar reservas de la empresa (opcionalmente por rango de fechas).</summary>
public sealed class ListarReservas
{
    private readonly IConsultaReservas _consulta;

    public ListarReservas(IConsultaReservas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ReservaDto>> EjecutarAsync(Guid empresaId, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, desde, hasta, ct);
}

/// <summary>Caso de uso: obtener una reserva por su identificador.</summary>
public sealed class ObtenerReserva
{
    private readonly IConsultaReservas _consulta;

    public ObtenerReserva(IConsultaReservas consulta) => _consulta = consulta;

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid reservaId, CancellationToken ct = default)
    {
        var reserva = await _consulta.ObtenerAsync(reservaId, ct).ConfigureAwait(false);
        return reserva is null
            ? Resultado.Fallo<ReservaDto>(Error.NoEncontrado("reserva.no_encontrada", "La reserva no existe."))
            : Resultado.Ok(reserva);
    }
}

/// <summary>Caso de uso: obtener (creándolo si hace falta) el token del enlace de calendario.</summary>
public sealed class ObtenerAgenda
{
    private readonly IRepositorioAgenda _agenda;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;

    public ObtenerAgenda(IRepositorioAgenda agenda, IUnidadDeTrabajoReservas unidadDeTrabajo)
    {
        _agenda = agenda;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<string> EjecutarAsync(Guid empresaId, bool regenerar = false, CancellationToken ct = default)
    {
        var agenda = await _agenda.ObtenerPorEmpresaAsync(empresaId, ct).ConfigureAwait(false);
        if (agenda is null)
        {
            agenda = AgendaCalendario.Crear(empresaId);
            _agenda.Agregar(agenda);
        }
        else if (regenerar)
        {
            agenda.Regenerar();
        }
        else
        {
            return agenda.Token;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return agenda.Token;
    }
}

/// <summary>
/// Caso de uso: generar el calendario iCalendar suscribible a partir de un token público. Resuelve la
/// empresa del token y fija el contexto para poder leer sus reservas con el aislamiento habitual.
/// </summary>
public sealed class FeedCalendario
{
    private readonly IRepositorioAgenda _agenda;
    private readonly IConsultaReservas _consulta;
    private readonly IContextoEmpresaMutable _contexto;
    private readonly IReloj _reloj;

    public FeedCalendario(IRepositorioAgenda agenda, IConsultaReservas consulta, IContextoEmpresaMutable contexto, IReloj reloj)
    {
        _agenda = agenda;
        _consulta = consulta;
        _contexto = contexto;
        _reloj = reloj;
    }

    public async Task<Resultado<string>> EjecutarAsync(string token, CancellationToken ct = default)
    {
        var agenda = await _agenda.ObtenerPorTokenAsync(token, ct).ConfigureAwait(false);
        if (agenda is null)
        {
            return Resultado.Fallo<string>(Error.NoEncontrado("agenda.no_encontrada", "El enlace de calendario no es válido."));
        }

        _contexto.Fijar(agenda.EmpresaId);
        var reservas = await _consulta.ListarAsync(agenda.EmpresaId, ct: ct).ConfigureAwait(false);
        return Resultado.Ok(GeneradorICal.Generar(reservas, "Reservas", _reloj.AhoraUtc));
    }
}

/// <summary>Datos de un turno para crear o actualizar (horas en formato «HH:mm», días como banderas).</summary>
public sealed record DatosTurno(string Nombre, int Dias, string HoraInicio, string HoraFin, int AforoComensales = 0);

/// <summary>Caso de uso: crear un turno de servicio.</summary>
public sealed class CrearTurno
{
    private readonly IRepositorioTurnos _turnos;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearTurno(IRepositorioTurnos turnos, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _turnos = turnos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<TurnoDto>> EjecutarAsync(Guid empresaId, DatosTurno datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (!Horas.Parsear(datos.HoraInicio, out var inicio) || !Horas.Parsear(datos.HoraFin, out var fin))
        {
            return Resultado.Fallo<TurnoDto>(Error.Validacion("turno.horas_invalidas", "Las horas deben tener el formato HH:mm."));
        }

        var turno = Turno.Crear(empresaId, datos.Nombre, (DiasSemana)datos.Dias, inicio, fin, datos.AforoComensales, _reloj);
        if (turno.EsFallo)
        {
            return Resultado.Fallo<TurnoDto>(turno.Error);
        }

        _turnos.Agregar(turno.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(TurnoDto.Desde(turno.Valor));
    }
}

/// <summary>Caso de uso: actualizar un turno.</summary>
public sealed class ActualizarTurno
{
    private readonly IRepositorioTurnos _turnos;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarTurno(IRepositorioTurnos turnos, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _turnos = turnos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<TurnoDto>> EjecutarAsync(Guid turnoId, DatosTurno datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var turno = await _turnos.ObtenerPorIdAsync(turnoId, ct).ConfigureAwait(false);
        if (turno is null)
        {
            return Resultado.Fallo<TurnoDto>(Error.NoEncontrado("turno.no_encontrado", "El turno no existe."));
        }

        if (!Horas.Parsear(datos.HoraInicio, out var inicio) || !Horas.Parsear(datos.HoraFin, out var fin))
        {
            return Resultado.Fallo<TurnoDto>(Error.Validacion("turno.horas_invalidas", "Las horas deben tener el formato HH:mm."));
        }

        var r = turno.Actualizar(datos.Nombre, (DiasSemana)datos.Dias, inicio, fin, datos.AforoComensales, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<TurnoDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(TurnoDto.Desde(turno));
    }
}

/// <summary>Caso de uso: desactivar (retirar) un turno.</summary>
public sealed class DesactivarTurno
{
    private readonly IRepositorioTurnos _turnos;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarTurno(IRepositorioTurnos turnos, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _turnos = turnos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid turnoId, CancellationToken ct = default)
    {
        var turno = await _turnos.ObtenerPorIdAsync(turnoId, ct).ConfigureAwait(false);
        if (turno is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("turno.no_encontrado", "El turno no existe."));
        }

        turno.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar los turnos de la empresa (activos primero).</summary>
public sealed class ListarTurnos
{
    private readonly IRepositorioTurnos _turnos;

    public ListarTurnos(IRepositorioTurnos turnos) => _turnos = turnos;

    public async Task<IReadOnlyList<TurnoDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var turnos = await _turnos.ListarTodosAsync(empresaId, ct).ConfigureAwait(false);
        return turnos.Select(TurnoDto.Desde).ToList();
    }
}

/// <summary>Caso de uso: ocupación (aforo usado/libre) de los turnos en una fecha.</summary>
public sealed class ObtenerDisponibilidad
{
    private readonly IRepositorioTurnos _turnos;
    private readonly IConsultaReservas _consulta;

    public ObtenerDisponibilidad(IRepositorioTurnos turnos, IConsultaReservas consulta)
    {
        _turnos = turnos;
        _consulta = consulta;
    }

    private static readonly HashSet<string> Activas = new(StringComparer.Ordinal)
    {
        nameof(EstadoReserva.Pendiente), nameof(EstadoReserva.Confirmada), nameof(EstadoReserva.Sentada),
    };

    public async Task<IReadOnlyList<DisponibilidadDto>> EjecutarAsync(Guid empresaId, DateOnly dia, CancellationToken ct = default)
    {
        var turnos = await _turnos.ListarActivosAsync(empresaId, ct).ConfigureAwait(false);
        if (turnos.Count == 0)
        {
            return Array.Empty<DisponibilidadDto>();
        }

        // Ventana amplia (±1 día) para no perder reservas de madrugada; se filtra por fecha local en memoria.
        var centro = new DateTimeOffset(dia.Year, dia.Month, dia.Day, 0, 0, 0, TimeSpan.Zero);
        var reservas = await _consulta.ListarAsync(empresaId, centro.AddDays(-1), centro.AddDays(2), ct).ConfigureAwait(false);
        var activas = reservas.Where(r => Activas.Contains(r.Estado) && DateOnly.FromDateTime(r.FechaHora.Date) == dia).ToList();

        return turnos
            .OrderBy(t => t.HoraInicio)
            .Select(t =>
            {
                var reservado = activas.Where(r => t.Aplica(r.FechaHora)).Sum(r => r.Comensales);
                var libre = t.AforoComensales > 0 ? Math.Max(0, t.AforoComensales - reservado) : 0;
                return new DisponibilidadDto(t.Id, t.Nombre, t.HoraInicio.ToString("HH\\:mm"), t.HoraFin.ToString("HH\\:mm"), t.AforoComensales, reservado, libre);
            })
            .ToList();
    }
}

/// <summary>Utilidades de parseo de horas «HH:mm».</summary>
internal static class Horas
{
    public static bool Parsear(string? texto, out TimeOnly hora) =>
        TimeOnly.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out hora);
}

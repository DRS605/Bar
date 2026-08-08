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

/// <summary>Caso de uso: crear una reserva.</summary>
public sealed class CrearReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearReserva(IRepositorioReservas reservas, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ReservaDto>> EjecutarAsync(Guid empresaId, DatosReserva datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

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

/// <summary>Caso de uso: actualizar una reserva pendiente o confirmada.</summary>
public sealed class ActualizarReserva
{
    private readonly IRepositorioReservas _reservas;
    private readonly IUnidadDeTrabajoReservas _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarReserva(IRepositorioReservas reservas, IUnidadDeTrabajoReservas unidadDeTrabajo, IReloj reloj)
    {
        _reservas = reservas;
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

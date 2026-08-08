using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Reservas.Dominio;

namespace AlxorCore.Reservas.Aplicacion;

/// <summary>Vista de una reserva.</summary>
public sealed record ReservaDto(
    Guid Id,
    string NombreCliente,
    string? Telefono,
    string? Email,
    DateTimeOffset FechaHora,
    DateTimeOffset FechaHoraFin,
    int DuracionMinutos,
    int Comensales,
    Guid? MesaId,
    string? Notas,
    string Estado,
    Guid? ComandaId,
    DateTimeOffset CreadaEn)
{
    public static ReservaDto Desde(Reserva r) => new(
        r.Id, r.NombreCliente, r.Telefono, r.Email, r.FechaHora, r.FechaHoraFin, r.DuracionMinutos,
        r.Comensales, r.MesaId, r.Notas, r.Estado.ToString(), r.ComandaId, r.CreadaEn);
}

/// <summary>Repositorio de reservas (escritura).</summary>
public interface IRepositorioReservas
{
    Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Reserva reserva);
}

/// <summary>Consultas de lectura de reservas.</summary>
public interface IConsultaReservas
{
    Task<ReservaDto?> ObtenerAsync(Guid reservaId, CancellationToken ct = default);

    /// <summary>Reservas de la empresa, opcionalmente acotadas por rango de fechas, ordenadas por fecha.</summary>
    Task<IReadOnlyList<ReservaDto>> ListarAsync(Guid empresaId, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken ct = default);
}

/// <summary>Repositorio del enlace de calendario (agenda) de cada empresa.</summary>
public interface IRepositorioAgenda
{
    Task<AgendaCalendario?> ObtenerPorEmpresaAsync(Guid empresaId, CancellationToken ct = default);

    Task<AgendaCalendario?> ObtenerPorTokenAsync(string token, CancellationToken ct = default);

    void Agregar(AgendaCalendario agenda);
}

/// <summary>Vista de un turno de servicio.</summary>
public sealed record TurnoDto(
    Guid Id,
    string Nombre,
    int Dias,
    IReadOnlyList<string> DiasNombres,
    string HoraInicio,
    string HoraFin,
    int AforoComensales,
    bool Activo)
{
    public static TurnoDto Desde(Turno t) => new(
        t.Id, t.Nombre, (int)t.Dias, NombresDias(t.Dias), t.HoraInicio.ToString("HH\\:mm"), t.HoraFin.ToString("HH\\:mm"), t.AforoComensales, t.Activo);

    private static readonly (DiasSemana Bandera, string Nombre)[] Semana =
    {
        (DiasSemana.Lunes, "L"), (DiasSemana.Martes, "M"), (DiasSemana.Miercoles, "X"),
        (DiasSemana.Jueves, "J"), (DiasSemana.Viernes, "V"), (DiasSemana.Sabado, "S"), (DiasSemana.Domingo, "D"),
    };

    private static List<string> NombresDias(DiasSemana dias) =>
        Semana.Where(d => dias.HasFlag(d.Bandera)).Select(d => d.Nombre).ToList();
}

/// <summary>Ocupación de un turno en una fecha concreta.</summary>
public sealed record DisponibilidadDto(
    Guid TurnoId,
    string Nombre,
    string HoraInicio,
    string HoraFin,
    int Aforo,
    int Reservado,
    int Libre);

/// <summary>Repositorio de turnos (escritura y lecturas de dominio para validar).</summary>
public interface IRepositorioTurnos
{
    Task<Turno?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Turno>> ListarActivosAsync(Guid empresaId, CancellationToken ct = default);

    Task<IReadOnlyList<Turno>> ListarTodosAsync(Guid empresaId, CancellationToken ct = default);

    void Agregar(Turno turno);
}

/// <summary>Unidad de trabajo del módulo Reservas.</summary>
public interface IUnidadDeTrabajoReservas : IUnidadDeTrabajo;

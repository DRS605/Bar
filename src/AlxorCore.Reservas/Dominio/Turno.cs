using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Reservas.Dominio;

/// <summary>Días de la semana en los que se presta un <see cref="Turno"/> (combinables).</summary>
[Flags]
public enum DiasSemana
{
    Ninguno = 0,
    Lunes = 1,
    Martes = 2,
    Miercoles = 4,
    Jueves = 8,
    Viernes = 16,
    Sabado = 32,
    Domingo = 64,
    Todos = Lunes | Martes | Miercoles | Jueves | Viernes | Sabado | Domingo,
}

/// <summary>
/// Turno de servicio (p. ej. «Comida» 13:00–16:00 o «Cena» 20:00–23:30) en determinados días de la
/// semana, con un <b>aforo</b> opcional de comensales. Define a la vez el <b>horario</b> en el que el
/// local acepta reservas: si ningún turno cubre un momento, se considera cerrado. Multiempresa.
/// </summary>
public sealed class Turno : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 60;

    private Turno(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Turno(Guid id, Guid empresaId, string nombre, DiasSemana dias, TimeOnly horaInicio, TimeOnly horaFin, int aforoComensales, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        Dias = dias;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        AforoComensales = aforoComensales;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public DiasSemana Dias { get; private set; }

    public TimeOnly HoraInicio { get; private set; }

    public TimeOnly HoraFin { get; private set; }

    /// <summary>Aforo máximo de comensales del turno en un día; 0 = sin límite.</summary>
    public int AforoComensales { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Turno> Crear(Guid empresaId, string? nombre, DiasSemana dias, TimeOnly horaInicio, TimeOnly horaFin, int aforoComensales, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, dias, horaInicio, horaFin, aforoComensales);
        if (error is not null)
        {
            return Resultado.Fallo<Turno>(error);
        }

        return Resultado.Ok(new Turno(Guid.NewGuid(), empresaId, nombre!.Trim(), dias, horaInicio, horaFin, aforoComensales, reloj.AhoraUtc));
    }

    public Resultado Actualizar(string? nombre, DiasSemana dias, TimeOnly horaInicio, TimeOnly horaFin, int aforoComensales, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, dias, horaInicio, horaFin, aforoComensales);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        Dias = dias;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        AforoComensales = aforoComensales;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    /// <summary>
    /// Indica si el turno cubre el momento indicado (día de la semana y hora, en la zona del propio
    /// valor). Admite turnos que cruzan la medianoche (p. ej. 20:00–00:30).
    /// </summary>
    public bool Aplica(DateTimeOffset cuando)
    {
        if (!Dias.HasFlag(Dia(cuando.DayOfWeek)))
        {
            return false;
        }

        var hora = TimeOnly.FromTimeSpan(cuando.TimeOfDay);
        return HoraFin > HoraInicio
            ? hora >= HoraInicio && hora < HoraFin
            : hora >= HoraInicio || hora < HoraFin;
    }

    /// <summary>Traduce un <see cref="DayOfWeek"/> a su bandera de <see cref="DiasSemana"/>.</summary>
    public static DiasSemana Dia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiasSemana.Lunes,
        DayOfWeek.Tuesday => DiasSemana.Martes,
        DayOfWeek.Wednesday => DiasSemana.Miercoles,
        DayOfWeek.Thursday => DiasSemana.Jueves,
        DayOfWeek.Friday => DiasSemana.Viernes,
        DayOfWeek.Saturday => DiasSemana.Sabado,
        _ => DiasSemana.Domingo,
    };

    private static Error? Validar(string? nombre, DiasSemana dias, TimeOnly horaInicio, TimeOnly horaFin, int aforoComensales)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("turno.nombre_vacio", "El nombre del turno es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("turno.nombre_largo", "El nombre del turno es demasiado largo.");
        }

        if (dias == DiasSemana.Ninguno)
        {
            return Error.Validacion("turno.sin_dias", "El turno debe aplicarse al menos a un día.");
        }

        if (horaInicio == horaFin)
        {
            return Error.Validacion("turno.horas_iguales", "La hora de inicio y de fin no pueden ser iguales.");
        }

        if (aforoComensales < 0)
        {
            return Error.Validacion("turno.aforo_negativo", "El aforo no puede ser negativo.");
        }

        return null;
    }
}

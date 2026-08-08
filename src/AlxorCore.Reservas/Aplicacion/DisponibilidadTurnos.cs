using AlxorCore.Nucleo.Resultados;
using AlxorCore.Reservas.Dominio;

namespace AlxorCore.Reservas.Aplicacion;

/// <summary>
/// Comprueba una reserva contra los turnos definidos: que caiga dentro de un turno abierto (horario)
/// y que no supere su aforo (contando el resto de reservas activas de ese turno y día). Es una función
/// pura, así que se puede probar sin base de datos. Si no hay turnos definidos, no se restringe nada
/// (reservas libres).
/// </summary>
public static class DisponibilidadTurnos
{
    private static readonly HashSet<string> Activas = new(StringComparer.Ordinal)
    {
        nameof(EstadoReserva.Pendiente), nameof(EstadoReserva.Confirmada), nameof(EstadoReserva.Sentada),
    };

    /// <summary>Valida el hueco. <paramref name="reservasDelDia"/> son reservas ya existentes a considerar para el aforo.</summary>
    public static Resultado Validar(
        IReadOnlyList<Turno> turnos,
        IReadOnlyList<ReservaDto> reservasDelDia,
        DateTimeOffset cuando,
        int comensales,
        Guid? excluir = null)
    {
        ArgumentNullException.ThrowIfNull(turnos);
        ArgumentNullException.ThrowIfNull(reservasDelDia);

        var activos = turnos.Where(t => t.Activo).ToList();
        if (activos.Count == 0)
        {
            return Resultado.Ok();
        }

        var turno = activos.FirstOrDefault(t => t.Aplica(cuando));
        if (turno is null)
        {
            return Resultado.Fallo(Error.Validacion("reserva.fuera_de_horario", "No hay ningún turno abierto a esa hora."));
        }

        if (turno.AforoComensales > 0)
        {
            var reservado = reservasDelDia
                .Where(r => r.Id != excluir
                    && Activas.Contains(r.Estado)
                    && r.FechaHora.Date == cuando.Date
                    && turno.Aplica(r.FechaHora))
                .Sum(r => r.Comensales);

            if (reservado + comensales > turno.AforoComensales)
            {
                return Resultado.Fallo(Error.Conflicto("reserva.aforo_completo", $"El turno «{turno.Nombre}» está completo para esa fecha."));
            }
        }

        return Resultado.Ok();
    }
}

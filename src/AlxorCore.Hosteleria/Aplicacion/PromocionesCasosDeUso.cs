using System.Globalization;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Datos para crear una promoción.</summary>
public sealed record DatosPromocion(
    string Nombre, decimal Porcentaje, string Ambito, string? Categoria = null, Guid? ProductoId = null,
    string? Dias = null, string? HoraInicio = null, string? HoraFin = null);

/// <summary>Utilidades para aplicar las promociones a un producto en el momento del pedido.</summary>
public static class PromocionesAplicables
{
    private static readonly TimeZoneInfo ZonaEspana = ResolverZona();

    private static TimeZoneInfo ResolverZona()
    {
        foreach (var id in new[] { "Europe/Madrid", "Romance Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>Mejor descuento (porcentaje) aplicable a un producto ahora; 0 si ninguna promoción aplica.</summary>
    public static decimal MejorDescuento(IEnumerable<Promocion> promociones, string? categoria, Guid productoId, DateTimeOffset ahoraUtc)
    {
        var local = TimeZoneInfo.ConvertTime(ahoraUtc, ZonaEspana);
        var dia = local.DayOfWeek;
        var hora = TimeOnly.FromTimeSpan(local.TimeOfDay);

        decimal mejor = 0m;
        foreach (var p in promociones)
        {
            if (p.Aplica(categoria, productoId, dia, hora) && p.Porcentaje > mejor)
            {
                mejor = p.Porcentaje;
            }
        }

        return mejor;
    }

    /// <summary>Convierte "HH:mm" en <see cref="TimeOnly"/>, o nulo si está vacío.</summary>
    public static TimeOnly? Hora(string? texto) =>
        TimeOnly.TryParse((texto ?? string.Empty).Trim(), CultureInfo.InvariantCulture, out var t) ? t : null;
}

/// <summary>Caso de uso: lista las promociones del local.</summary>
public sealed class ListarPromociones
{
    private readonly IRepositorioPromociones _repositorio;

    public ListarPromociones(IRepositorioPromociones repositorio) => _repositorio = repositorio;

    public async Task<IReadOnlyList<PromocionDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var lista = await _repositorio.ListarPorEmpresaAsync(empresaId, ct).ConfigureAwait(false);
        return lista.OrderByDescending(p => p.CreadaEn).Select(PromocionDto.Desde).ToList();
    }
}

/// <summary>Caso de uso (personal): crea una promoción.</summary>
public sealed class CrearPromocion
{
    private readonly IRepositorioPromociones _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearPromocion(IRepositorioPromociones repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PromocionDto>> EjecutarAsync(Guid empresaId, DatosPromocion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var ambito = Enum.TryParse<AmbitoPromocion>(datos.Ambito, ignoreCase: true, out var a) && Enum.IsDefined(a) ? a : AmbitoPromocion.Todo;
        var creada = Promocion.Crear(empresaId, datos.Nombre, datos.Porcentaje, ambito, datos.Categoria, datos.ProductoId,
            datos.Dias, PromocionesAplicables.Hora(datos.HoraInicio), PromocionesAplicables.Hora(datos.HoraFin), _reloj);
        if (creada.EsFallo)
        {
            return Resultado.Fallo<PromocionDto>(creada.Error);
        }

        _repositorio.Agregar(creada.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PromocionDto.Desde(creada.Valor));
    }
}

/// <summary>Caso de uso (personal): activa o desactiva una promoción.</summary>
public sealed class CambiarActivaPromocion
{
    private readonly IRepositorioPromociones _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;

    public CambiarActivaPromocion(IRepositorioPromociones repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid id, bool activa, CancellationToken ct = default)
    {
        var p = await _repositorio.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (p is null || p.EmpresaId != empresaId)
        {
            return Resultado.Fallo(Error.NoEncontrado("promocion.no_encontrada", "La promoción no existe."));
        }

        p.CambiarActiva(activa);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso (personal): elimina una promoción.</summary>
public sealed class EliminarPromocion
{
    private readonly IRepositorioPromociones _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;

    public EliminarPromocion(IRepositorioPromociones repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var p = await _repositorio.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (p is null || p.EmpresaId != empresaId)
        {
            return Resultado.Fallo(Error.NoEncontrado("promocion.no_encontrada", "La promoción no existe."));
        }

        _repositorio.Quitar(p);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

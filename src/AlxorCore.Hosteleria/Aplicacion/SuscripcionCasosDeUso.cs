using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Datos para cambiar el plan contratado de un local.</summary>
public sealed record DatosSuscripcion(string Plan);

/// <summary>Caso de uso (personal): obtiene el plan contratado del local (el de por defecto si aún no hay suscripción).</summary>
public sealed class ObtenerSuscripcion
{
    private readonly IRepositorioSuscripcion _repositorio;

    public ObtenerSuscripcion(IRepositorioSuscripcion repositorio) => _repositorio = repositorio;

    public async Task<SuscripcionDto> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var s = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        return new SuscripcionDto((s?.Plan ?? PlanesBar.PorDefecto).ToString());
    }
}

/// <summary>Caso de uso (personal): fija el plan contratado del local.</summary>
public sealed class CambiarPlanBar
{
    private readonly IRepositorioSuscripcion _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CambiarPlanBar(IRepositorioSuscripcion repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<SuscripcionDto>> EjecutarAsync(Guid empresaId, DatosSuscripcion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var plan = PlanesBar.Normalizar(datos.Plan);

        var s = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (s is null)
        {
            s = SuscripcionBar.Crear(empresaId, plan, _reloj);
            _repositorio.Agregar(s);
        }
        else
        {
            s.CambiarPlan(plan, _reloj);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(SuscripcionDto.Desde(s));
    }
}

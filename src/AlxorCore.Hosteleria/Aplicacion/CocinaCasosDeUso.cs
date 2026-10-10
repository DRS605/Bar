using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Datos para fijar la zona de preparación de un producto.</summary>
public sealed record DatosZonaProducto(string Zona);

/// <summary>Caso de uso: zonas de preparación de todos los productos del local.</summary>
public sealed class ObtenerZonasEmpresa
{
    private readonly IRepositorioZonasProducto _repositorio;

    public ObtenerZonasEmpresa(IRepositorioZonasProducto repositorio) => _repositorio = repositorio;

    public async Task<IReadOnlyList<ZonaProductoDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var zonas = await _repositorio.ListarPorEmpresaAsync(empresaId, ct).ConfigureAwait(false);
        return zonas.Select(ZonaProductoDto.Desde).ToList();
    }
}

/// <summary>Caso de uso (personal): fija la zona de preparación de un producto.</summary>
public sealed class GuardarZonaProducto
{
    private readonly IRepositorioZonasProducto _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarZonaProducto(IRepositorioZonasProducto repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid productoId, DatosZonaProducto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var zona = ZonasPreparacion.Normalizar(datos.Zona);

        var actual = await _repositorio.ObtenerAsync(productoId, ct).ConfigureAwait(false);
        if (actual is null)
        {
            _repositorio.Agregar(ZonaProducto.Crear(empresaId, productoId, zona, _reloj));
        }
        else
        {
            actual.Fijar(zona, _reloj);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: artículos pendientes de servir en la pantalla de cocina.</summary>
public sealed class ListarPendientesCocina
{
    private readonly IConsultaCocina _cocina;

    public ListarPendientesCocina(IConsultaCocina cocina) => _cocina = cocina;

    public Task<IReadOnlyList<ItemCocinaDto>> EjecutarAsync(Guid empresaId, string? zona = null, CancellationToken ct = default)
    {
        ZonaPreparacion? z = string.IsNullOrWhiteSpace(zona) ? null : ZonasPreparacion.Normalizar(zona);
        return _cocina.PendientesAsync(empresaId, z, ct);
    }
}

/// <summary>Caso de uso: marca un artículo como servido desde la pantalla de cocina.</summary>
public sealed class ServirItemCocina
{
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;

    public ServirItemCocina(IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo)
    {
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid comandaId, Guid lineaId, CancellationToken ct = default)
    {
        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var r = comanda.ServirLinea(lineaId);
        if (r.EsFallo)
        {
            return r;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

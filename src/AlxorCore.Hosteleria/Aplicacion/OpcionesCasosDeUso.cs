using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Una opción tal como llega de la interfaz.</summary>
public sealed record DatosOpcion(string Nombre, decimal PrecioDelta);

/// <summary>Un grupo de opciones tal como llega de la interfaz.</summary>
public sealed record DatosGrupoOpcion(string Nombre, string Seleccion, bool Obligatorio, List<DatosOpcion> Opciones);

/// <summary>Conjunto de grupos de opciones de un producto.</summary>
public sealed record DatosOpcionesProducto(List<DatosGrupoOpcion> Grupos);

/// <summary>Caso de uso: grupos de opciones de un producto.</summary>
public sealed class ObtenerOpcionesProducto
{
    private readonly IRepositorioGruposOpcion _repositorio;

    public ObtenerOpcionesProducto(IRepositorioGruposOpcion repositorio) => _repositorio = repositorio;

    public async Task<IReadOnlyList<GrupoOpcionDto>> EjecutarAsync(Guid productoId, CancellationToken ct = default)
    {
        var grupos = await _repositorio.ListarPorProductoAsync(productoId, ct).ConfigureAwait(false);
        return grupos.OrderBy(g => g.Orden).Select(GrupoOpcionDto.Desde).ToList();
    }
}

/// <summary>Caso de uso: todas las opciones del local, agrupadas por producto (para el TPV).</summary>
public sealed class ListarOpcionesEmpresa
{
    private readonly IRepositorioGruposOpcion _repositorio;

    public ListarOpcionesEmpresa(IRepositorioGruposOpcion repositorio) => _repositorio = repositorio;

    public async Task<IReadOnlyList<ProductoConOpcionesDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var grupos = await _repositorio.ListarPorEmpresaAsync(empresaId, ct).ConfigureAwait(false);
        return grupos
            .GroupBy(g => g.ProductoId)
            .Select(g => new ProductoConOpcionesDto(g.Key, g.OrderBy(x => x.Orden).Select(GrupoOpcionDto.Desde).ToList()))
            .ToList();
    }
}

/// <summary>Caso de uso (personal): reemplaza los grupos de opciones de un producto.</summary>
public sealed class GuardarOpcionesProducto
{
    private readonly IRepositorioGruposOpcion _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;

    public GuardarOpcionesProducto(IRepositorioGruposOpcion repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid productoId, DatosOpcionesProducto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // Reemplazo completo: se borran los grupos actuales del producto y se crean los nuevos.
        var actuales = await _repositorio.ListarPorProductoAsync(productoId, ct).ConfigureAwait(false);
        foreach (var g in actuales)
        {
            _repositorio.Quitar(g);
        }

        var ordenG = 0;
        foreach (var dg in datos.Grupos ?? new List<DatosGrupoOpcion>())
        {
            if (string.IsNullOrWhiteSpace(dg.Nombre) || dg.Opciones is null || dg.Opciones.Count == 0)
            {
                continue;
            }

            var seleccion = Enum.TryParse<SeleccionOpcion>(dg.Seleccion, ignoreCase: true, out var s) && Enum.IsDefined(s) ? s : SeleccionOpcion.Unica;
            var grupo = GrupoOpcion.Crear(empresaId, productoId, dg.Nombre, seleccion, dg.Obligatorio && seleccion == SeleccionOpcion.Unica, ordenG++);
            var ordenO = 0;
            foreach (var op in dg.Opciones)
            {
                grupo.AgregarOpcion(op.Nombre, op.PrecioDelta, ordenO++);
            }

            if (grupo.Opciones.Count > 0)
            {
                _repositorio.Agregar(grupo);
            }
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Resuelve una selección de opciones de un producto en precio e descripción compuestos.</summary>
public static class ResolverOpciones
{
    public sealed record Resultado(decimal PrecioDelta, string? Texto, Error? Error);

    public static Resultado Resolver(IReadOnlyList<GrupoOpcion> grupos, IReadOnlyCollection<Guid> opcionIds)
    {
        var elegidas = new List<OpcionProducto>();
        foreach (var grupo in grupos.OrderBy(g => g.Orden))
        {
            var deEsteGrupo = grupo.Opciones.Where(o => opcionIds.Contains(o.Id)).OrderBy(o => o.Orden).ToList();

            if (grupo.Seleccion == SeleccionOpcion.Unica && deEsteGrupo.Count > 1)
            {
                return new Resultado(0m, null, Error.Validacion("opcion.seleccion_unica", $"En «{grupo.Nombre}» solo se puede elegir una opción."));
            }

            if (grupo.Obligatorio && deEsteGrupo.Count == 0)
            {
                return new Resultado(0m, null, Error.Validacion("opcion.obligatoria", $"Elige una opción en «{grupo.Nombre}»."));
            }

            elegidas.AddRange(deEsteGrupo);
        }

        var delta = elegidas.Sum(o => o.PrecioDelta);
        var texto = elegidas.Count == 0 ? null : string.Join(", ", elegidas.Select(o => o.Nombre));
        return new Resultado(delta, texto, null);
    }
}

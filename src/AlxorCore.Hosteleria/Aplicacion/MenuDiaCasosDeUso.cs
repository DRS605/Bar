using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Una sección del menú del día tal como llega de la interfaz.</summary>
public sealed record DatosSeccionMenu(string Titulo, List<string> Platos);

/// <summary>Datos para fijar el menú del día de un local.</summary>
public sealed record DatosMenuDia(decimal Precio, bool Activo, string? Incluye, List<DatosSeccionMenu> Secciones);

/// <summary>Caso de uso: obtiene el menú del día del local (vacío si aún no hay).</summary>
public sealed class ObtenerMenuDia
{
    private readonly IRepositorioMenuDia _repositorio;

    public ObtenerMenuDia(IRepositorioMenuDia repositorio) => _repositorio = repositorio;

    public async Task<MenuDiaDto> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var menu = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        return menu is null ? MenuDiaDto.Vacio : MenuDiaDto.Desde(menu);
    }
}

/// <summary>Caso de uso (personal): fija el menú del día del local.</summary>
public sealed class GuardarMenuDia
{
    private readonly IRepositorioMenuDia _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarMenuDia(IRepositorioMenuDia repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MenuDiaDto>> EjecutarAsync(Guid empresaId, DatosMenuDia datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // Aplanamos las secciones en pares (sección, plato) conservando el orden.
        var platos = (datos.Secciones ?? new List<DatosSeccionMenu>())
            .SelectMany(s => (s.Platos ?? new List<string>()).Select(p => (Seccion: s.Titulo ?? string.Empty, Nombre: p ?? string.Empty)))
            .ToList();

        var menu = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (menu is null)
        {
            menu = MenuDia.Crear(empresaId, _reloj);
            _repositorio.Agregar(menu);
        }

        var resultado = menu.Fijar(datos.Precio, datos.Activo, datos.Incluye, platos, _reloj);
        if (resultado.EsFallo)
        {
            return Resultado.Fallo<MenuDiaDto>(resultado.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(MenuDiaDto.Desde(menu));
    }
}

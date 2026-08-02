using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Dominio;

namespace AlxorCore.Terceros.Aplicacion;

/// <summary>Vista de un proveedor.</summary>
public sealed record ProveedorDto(
    Guid Id, string Nombre, string? NifFiscal, string? Email,
    string Calle, string CodigoPostal, string Poblacion, string Provincia, string Pais,
    decimal PorcentajeIrpfDefecto, bool Activo)
{
    public static ProveedorDto Desde(Proveedor p) => new(
        p.Id, p.Nombre, p.NifFiscal, p.Email,
        p.Direccion.Calle, p.Direccion.CodigoPostal, p.Direccion.Poblacion, p.Direccion.Provincia, p.Direccion.Pais,
        p.PorcentajeIrpfDefecto, p.Activo);
}

/// <summary>Repositorio de proveedores (escritura).</summary>
public interface IRepositorioProveedores
{
    Task<Proveedor?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Proveedor proveedor);
}

/// <summary>Consultas de lectura de proveedores (las usan la API y el módulo Gastos).</summary>
public interface IConsultaProveedores
{
    Task<ProveedorDto?> ObtenerAsync(Guid proveedorId, CancellationToken ct = default);

    Task<IReadOnlyList<ProveedorDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default);
}

/// <summary>Datos de un proveedor para crear o actualizar.</summary>
public sealed record DatosProveedor(
    string Nombre,
    string? NifFiscal = null,
    string? Email = null,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    string? Pais = null,
    decimal PorcentajeIrpfDefecto = 0m);

/// <summary>Caso de uso: crear un proveedor.</summary>
public sealed class CrearProveedor
{
    private readonly IRepositorioProveedores _proveedores;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearProveedor(IRepositorioProveedores proveedores, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _proveedores = proveedores;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ProveedorDto>> EjecutarAsync(Guid empresaId, DatosProveedor datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var direccion = Direccion.Crear(datos.Calle, datos.CodigoPostal, datos.Poblacion, datos.Provincia, datos.Pais);
        var proveedor = Proveedor.Crear(empresaId, datos.Nombre, datos.NifFiscal, datos.Email, direccion, datos.PorcentajeIrpfDefecto, _reloj);
        if (proveedor.EsFallo)
        {
            return Resultado.Fallo<ProveedorDto>(proveedor.Error);
        }

        _proveedores.Agregar(proveedor.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProveedorDto.Desde(proveedor.Valor));
    }
}

/// <summary>Caso de uso: actualizar un proveedor.</summary>
public sealed class ActualizarProveedor
{
    private readonly IRepositorioProveedores _proveedores;
    private readonly IUnidadDeTrabajoTerceros _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarProveedor(IRepositorioProveedores proveedores, IUnidadDeTrabajoTerceros unidadDeTrabajo, IReloj reloj)
    {
        _proveedores = proveedores;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ProveedorDto>> EjecutarAsync(Guid proveedorId, DatosProveedor datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var proveedor = await _proveedores.ObtenerPorIdAsync(proveedorId, ct).ConfigureAwait(false);
        if (proveedor is null)
        {
            return Resultado.Fallo<ProveedorDto>(Error.NoEncontrado("proveedor.no_encontrado", "El proveedor no existe."));
        }

        var direccion = Direccion.Crear(datos.Calle, datos.CodigoPostal, datos.Poblacion, datos.Provincia, datos.Pais);
        var r = proveedor.Actualizar(datos.Nombre, datos.NifFiscal, datos.Email, direccion, datos.PorcentajeIrpfDefecto, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ProveedorDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ProveedorDto.Desde(proveedor));
    }
}

/// <summary>Caso de uso: listar los proveedores de la empresa activa.</summary>
public sealed class ListarProveedores
{
    private readonly IConsultaProveedores _consulta;

    public ListarProveedores(IConsultaProveedores consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ProveedorDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivos: false, ct);
}

/// <summary>Caso de uso: obtener un proveedor por su identificador.</summary>
public sealed class ObtenerProveedor
{
    private readonly IConsultaProveedores _consulta;

    public ObtenerProveedor(IConsultaProveedores consulta) => _consulta = consulta;

    public async Task<Resultado<ProveedorDto>> EjecutarAsync(Guid proveedorId, CancellationToken ct = default)
    {
        var proveedor = await _consulta.ObtenerAsync(proveedorId, ct).ConfigureAwait(false);
        return proveedor is null
            ? Resultado.Fallo<ProveedorDto>(Error.NoEncontrado("proveedor.no_encontrado", "El proveedor no existe."))
            : Resultado.Ok(proveedor);
    }
}

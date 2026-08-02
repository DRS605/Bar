using AlxorCore.Gastos.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Gastos.Aplicacion;

/// <summary>Vista de un gasto.</summary>
public sealed record GastoDto(
    Guid Id, Guid? ProveedorId, string? ProveedorTexto, string Concepto, DateOnly Fecha,
    decimal BaseImponible, string CodigoIva, decimal PorcentajeIva, decimal CuotaIva,
    decimal PorcentajeIrpf, decimal RetencionIrpf, decimal Total, string Estado)
{
    public static GastoDto Desde(Gasto g) => new(
        g.Id, g.ProveedorId, g.ProveedorTexto, g.Concepto, g.Fecha, g.BaseImponible, g.CodigoIva, g.PorcentajeIva, g.CuotaIva,
        g.PorcentajeIrpf, g.RetencionIrpf, g.Total, g.Estado.ToString());
}

/// <summary>Repositorio de gastos (escritura).</summary>
public interface IRepositorioGastos
{
    Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Gasto gasto);
}

/// <summary>Consultas de lectura de gastos (las usan la API, Tesorería e Informes).</summary>
public interface IConsultaGastos
{
    Task<GastoDto?> ObtenerAsync(Guid gastoId, CancellationToken ct = default);

    Task<IReadOnlyList<GastoDto>> ListarAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Gastos.</summary>
public interface IUnidadDeTrabajoGastos : IUnidadDeTrabajo;

/// <summary>Datos para registrar un gasto.</summary>
public sealed record RegistrarGastoComando(
    string Concepto,
    decimal BaseImponible,
    Guid? ProveedorId = null,
    string? ProveedorTexto = null,
    string? CodigoIva = null,
    decimal PorcentajeIrpf = 0m,
    DateOnly? Fecha = null);

/// <summary>Caso de uso: registrar un gasto. Si se indica un proveedor, se copia su nombre.</summary>
public sealed class RegistrarGasto
{
    private readonly IRepositorioGastos _gastos;
    private readonly IConsultaProveedores _proveedores;
    private readonly IUnidadDeTrabajoGastos _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarGasto(IRepositorioGastos gastos, IConsultaProveedores proveedores, IUnidadDeTrabajoGastos unidadDeTrabajo, IReloj reloj)
    {
        _gastos = gastos;
        _proveedores = proveedores;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<GastoDto>> EjecutarAsync(Guid empresaId, RegistrarGastoComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var proveedorTexto = comando.ProveedorTexto;
        if (comando.ProveedorId is { } provId)
        {
            var proveedor = await _proveedores.ObtenerAsync(provId, ct).ConfigureAwait(false);
            if (proveedor is null)
            {
                return Resultado.Fallo<GastoDto>(Error.NoEncontrado("proveedor.no_encontrado", "El proveedor no existe."));
            }

            proveedorTexto = proveedor.Nombre;
        }

        var fecha = comando.Fecha ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var gasto = Gasto.Registrar(empresaId, comando.ProveedorId, proveedorTexto, comando.Concepto, fecha, comando.BaseImponible, comando.CodigoIva, comando.PorcentajeIrpf, _reloj);
        if (gasto.EsFallo)
        {
            return Resultado.Fallo<GastoDto>(gasto.Error);
        }

        _gastos.Agregar(gasto.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(GastoDto.Desde(gasto.Valor));
    }
}

/// <summary>Caso de uso: listar los gastos de la empresa activa.</summary>
public sealed class ListarGastos
{
    private readonly IConsultaGastos _consulta;

    public ListarGastos(IConsultaGastos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<GastoDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener un gasto.</summary>
public sealed class ObtenerGasto
{
    private readonly IConsultaGastos _consulta;

    public ObtenerGasto(IConsultaGastos consulta) => _consulta = consulta;

    public async Task<Resultado<GastoDto>> EjecutarAsync(Guid gastoId, CancellationToken ct = default)
    {
        var gasto = await _consulta.ObtenerAsync(gastoId, ct).ConfigureAwait(false);
        return gasto is null
            ? Resultado.Fallo<GastoDto>(Error.NoEncontrado("gasto.no_encontrado", "El gasto no existe."))
            : Resultado.Ok(gasto);
    }
}

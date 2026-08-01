using AlxorCore.Gastos.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Gastos.Aplicacion;

/// <summary>Vista de un gasto.</summary>
public sealed record GastoDto(
    Guid Id, string? ProveedorTexto, string Concepto, DateOnly Fecha,
    decimal BaseImponible, string CodigoIva, decimal PorcentajeIva, decimal CuotaIva,
    decimal PorcentajeIrpf, decimal RetencionIrpf, decimal Total, string Estado)
{
    public static GastoDto Desde(Gasto g) => new(
        g.Id, g.ProveedorTexto, g.Concepto, g.Fecha, g.BaseImponible, g.CodigoIva, g.PorcentajeIva, g.CuotaIva,
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
    string? ProveedorTexto = null,
    string? CodigoIva = null,
    decimal PorcentajeIrpf = 0m,
    DateOnly? Fecha = null);

/// <summary>Caso de uso: registrar un gasto.</summary>
public sealed class RegistrarGasto
{
    private readonly IRepositorioGastos _gastos;
    private readonly IUnidadDeTrabajoGastos _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarGasto(IRepositorioGastos gastos, IUnidadDeTrabajoGastos unidadDeTrabajo, IReloj reloj)
    {
        _gastos = gastos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<GastoDto>> EjecutarAsync(Guid empresaId, RegistrarGastoComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var fecha = comando.Fecha ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var gasto = Gasto.Registrar(empresaId, comando.ProveedorTexto, comando.Concepto, fecha, comando.BaseImponible, comando.CodigoIva, comando.PorcentajeIrpf, _reloj);
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

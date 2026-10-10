using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Datos para registrar un movimiento de caja.</summary>
public sealed record DatosMovimientoCaja(string Tipo, decimal Importe, string? Concepto);

/// <summary>Caso de uso: registra un movimiento manual de efectivo (fondo, entrada o salida).</summary>
public sealed class RegistrarMovimientoCaja
{
    private readonly IRepositorioMovimientosCaja _movimientos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarMovimientoCaja(IRepositorioMovimientosCaja movimientos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _movimientos = movimientos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MovimientoCajaDto>> EjecutarAsync(Guid empresaId, DatosMovimientoCaja datos, Guid? usuarioId = null, string? usuarioNombre = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (!Enum.TryParse<TipoMovimientoCaja>(datos.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
        {
            return Resultado.Fallo<MovimientoCajaDto>(Error.Validacion("caja.tipo_invalido", "Tipo de movimiento no válido."));
        }

        var creado = MovimientoCaja.Crear(empresaId, tipo, datos.Importe, datos.Concepto, usuarioId, usuarioNombre, _reloj);
        if (creado.EsFallo)
        {
            return Resultado.Fallo<MovimientoCajaDto>(creado.Error);
        }

        _movimientos.Agregar(creado.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(MovimientoCajaDto.Desde(creado.Valor));
    }
}

/// <summary>Caso de uso: elimina un movimiento de caja (corrige un error de registro).</summary>
public sealed class QuitarMovimientoCaja
{
    private readonly IRepositorioMovimientosCaja _movimientos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;

    public QuitarMovimientoCaja(IRepositorioMovimientosCaja movimientos, IUnidadDeTrabajoHosteleria unidadDeTrabajo)
    {
        _movimientos = movimientos;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var mov = await _movimientos.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (mov is null || mov.EmpresaId != empresaId)
        {
            return Resultado.Fallo(Error.NoEncontrado("caja.movimiento_no_encontrado", "El movimiento no existe."));
        }

        _movimientos.Quitar(mov);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: lista los movimientos de caja de un día.</summary>
public sealed class ListarMovimientosCaja
{
    private readonly IRepositorioMovimientosCaja _movimientos;
    private readonly IReloj _reloj;

    public ListarMovimientosCaja(IRepositorioMovimientosCaja movimientos, IReloj reloj)
    {
        _movimientos = movimientos;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<MovimientoCajaDto>> EjecutarAsync(Guid empresaId, DateOnly? dia = null, CancellationToken ct = default)
    {
        var d = dia ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var lista = await _movimientos.ListarPorDiaAsync(empresaId, d, ct).ConfigureAwait(false);
        return lista.Select(MovimientoCajaDto.Desde).ToList();
    }
}

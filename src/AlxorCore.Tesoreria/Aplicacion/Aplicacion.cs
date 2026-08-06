using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Tesoreria.Dominio;

namespace AlxorCore.Tesoreria.Aplicacion;

/// <summary>Vista de un movimiento de tesorería.</summary>
public sealed record MovimientoDto(Guid Id, string Sentido, decimal Importe, DateOnly Fecha, string? Metodo)
{
    public static MovimientoDto Desde(Movimiento m) => new(m.Id, m.Sentido.ToString(), m.Importe, m.Fecha, m.Metodo);
}

/// <summary>Saldo de un documento (total, liquidado, pendiente y estado derivado).</summary>
public sealed record SaldoDto(
    string TipoDocumento, Guid DocumentoId, decimal Total, decimal Liquidado, decimal Pendiente, string Estado,
    IReadOnlyList<MovimientoDto> Movimientos);

/// <summary>Repositorio y consultas de movimientos de tesorería.</summary>
public interface IRepositorioMovimientos
{
    void Agregar(Movimiento movimiento);

    Task<decimal> SumaAsync(TipoDocumentoTesoreria tipo, Guid documentoId, CancellationToken ct = default);

    Task<IReadOnlyList<Movimiento>> ListarAsync(TipoDocumentoTesoreria tipo, Guid documentoId, CancellationToken ct = default);
}

/// <summary>Unidad de trabajo del módulo Tesorería.</summary>
public interface IUnidadDeTrabajoTesoreria : IUnidadDeTrabajo;

/// <summary>Consultas agregadas de tesorería (las usa Informes).</summary>
public interface IConsultaTesoreria
{
    /// <summary>Total liquidado (cobrado o pagado) de todos los documentos de un tipo en la empresa activa.</summary>
    Task<decimal> TotalLiquidadoAsync(TipoDocumentoTesoreria tipo, CancellationToken ct = default);

    /// <summary>Movimientos (cobros y pagos) de la empresa en un rango de fechas (para el cierre de caja).</summary>
    Task<IReadOnlyList<MovimientoDto>> ListarPorPeriodoAsync(Guid empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

/// <summary>Datos para registrar un cobro contra una factura.</summary>
public sealed record RegistrarCobroComando(Guid FacturaId, decimal Importe, DateOnly? Fecha = null, string? Metodo = null);

/// <summary>Datos para registrar un pago contra un gasto.</summary>
public sealed record RegistrarPagoComando(Guid GastoId, decimal Importe, DateOnly? Fecha = null, string? Metodo = null);

/// <summary>Caso de uso: registrar un cobro contra una factura (total o parcial, sin sobrepago).</summary>
public sealed class RegistrarCobro
{
    private readonly IConsultaFacturas _facturas;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IUnidadDeTrabajoTesoreria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarCobro(IConsultaFacturas facturas, IRepositorioMovimientos movimientos, IUnidadDeTrabajoTesoreria unidadDeTrabajo, IReloj reloj)
    {
        _facturas = facturas;
        _movimientos = movimientos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<SaldoDto>> EjecutarAsync(Guid empresaId, RegistrarCobroComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var factura = await _facturas.ObtenerAsync(comando.FacturaId, ct).ConfigureAwait(false);
        if (factura is null)
        {
            return Resultado.Fallo<SaldoDto>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."));
        }

        return await RegistrarAsync(empresaId, TipoDocumentoTesoreria.Factura, comando.FacturaId, SentidoMovimiento.Cobro,
            comando.Importe, factura.Total, comando.Fecha, comando.Metodo, _movimientos, _unidadDeTrabajo, _reloj, ct).ConfigureAwait(false);
    }

    internal static async Task<Resultado<SaldoDto>> RegistrarAsync(
        Guid empresaId, TipoDocumentoTesoreria tipo, Guid documentoId, SentidoMovimiento sentido, decimal importe, decimal totalDocumento,
        DateOnly? fecha, string? metodo, IRepositorioMovimientos movimientos, IUnidadDeTrabajo unidadDeTrabajo, IReloj reloj, CancellationToken ct)
    {
        var importeRedondeado = Redondeo.Dos(importe);
        if (importeRedondeado <= 0)
        {
            return Resultado.Fallo<SaldoDto>(Error.Validacion("movimiento.importe_invalido", "El importe debe ser mayor que cero."));
        }

        var liquidado = await movimientos.SumaAsync(tipo, documentoId, ct).ConfigureAwait(false);
        if (liquidado + importeRedondeado > totalDocumento)
        {
            return Resultado.Fallo<SaldoDto>(Error.Conflicto("movimiento.sobrepago", "El importe supera el pendiente del documento."));
        }

        var fechaMovimiento = fecha ?? DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime);
        var movimiento = Movimiento.Crear(empresaId, tipo, documentoId, sentido, importeRedondeado, fechaMovimiento, metodo, reloj);
        if (movimiento.EsFallo)
        {
            return Resultado.Fallo<SaldoDto>(movimiento.Error);
        }

        movimientos.Agregar(movimiento.Valor);
        await unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);

        var nuevoLiquidado = Redondeo.Dos(liquidado + importeRedondeado);
        var pendiente = Redondeo.Dos(totalDocumento - nuevoLiquidado);
        var estado = Movimiento.DerivarEstado(totalDocumento, nuevoLiquidado);
        return Resultado.Ok(new SaldoDto(tipo.ToString(), documentoId, totalDocumento, nuevoLiquidado, pendiente, estado.ToString(), []));
    }
}

/// <summary>Caso de uso: registrar un pago contra un gasto (total o parcial, sin sobrepago).</summary>
public sealed class RegistrarPago
{
    private readonly IConsultaGastos _gastos;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IUnidadDeTrabajoTesoreria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegistrarPago(IConsultaGastos gastos, IRepositorioMovimientos movimientos, IUnidadDeTrabajoTesoreria unidadDeTrabajo, IReloj reloj)
    {
        _gastos = gastos;
        _movimientos = movimientos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<SaldoDto>> EjecutarAsync(Guid empresaId, RegistrarPagoComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var gasto = await _gastos.ObtenerAsync(comando.GastoId, ct).ConfigureAwait(false);
        if (gasto is null)
        {
            return Resultado.Fallo<SaldoDto>(Error.NoEncontrado("gasto.no_encontrado", "El gasto no existe."));
        }

        return await RegistrarCobro.RegistrarAsync(empresaId, TipoDocumentoTesoreria.Gasto, comando.GastoId, SentidoMovimiento.Pago,
            comando.Importe, gasto.Total, comando.Fecha, comando.Metodo, _movimientos, _unidadDeTrabajo, _reloj, ct).ConfigureAwait(false);
    }
}

/// <summary>Caso de uso: consultar el saldo (y los movimientos) de un documento.</summary>
public sealed class ConsultarSaldo
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;
    private readonly IRepositorioMovimientos _movimientos;

    public ConsultarSaldo(IConsultaFacturas facturas, IConsultaGastos gastos, IRepositorioMovimientos movimientos)
    {
        _facturas = facturas;
        _gastos = gastos;
        _movimientos = movimientos;
    }

    public async Task<Resultado<SaldoDto>> DeFacturaAsync(Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _facturas.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
        if (factura is null)
        {
            return Resultado.Fallo<SaldoDto>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."));
        }

        return await ConstruirAsync(TipoDocumentoTesoreria.Factura, facturaId, factura.Total, ct).ConfigureAwait(false);
    }

    public async Task<Resultado<SaldoDto>> DeGastoAsync(Guid gastoId, CancellationToken ct = default)
    {
        var gasto = await _gastos.ObtenerAsync(gastoId, ct).ConfigureAwait(false);
        if (gasto is null)
        {
            return Resultado.Fallo<SaldoDto>(Error.NoEncontrado("gasto.no_encontrado", "El gasto no existe."));
        }

        return await ConstruirAsync(TipoDocumentoTesoreria.Gasto, gastoId, gasto.Total, ct).ConfigureAwait(false);
    }

    private async Task<Resultado<SaldoDto>> ConstruirAsync(TipoDocumentoTesoreria tipo, Guid documentoId, decimal total, CancellationToken ct)
    {
        var movimientos = await _movimientos.ListarAsync(tipo, documentoId, ct).ConfigureAwait(false);
        var liquidado = Redondeo.Dos(movimientos.Sum(m => m.Importe));
        var pendiente = Redondeo.Dos(total - liquidado);
        var estado = Movimiento.DerivarEstado(total, liquidado);
        return Resultado.Ok(new SaldoDto(
            tipo.ToString(), documentoId, total, liquidado, pendiente, estado.ToString(),
            movimientos.Select(MovimientoDto.Desde).ToList()));
    }
}

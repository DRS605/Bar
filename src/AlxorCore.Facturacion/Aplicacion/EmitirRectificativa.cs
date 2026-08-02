using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Datos para emitir una factura rectificativa que corrige a otra.</summary>
public sealed record EmitirRectificativaComando(
    string Motivo,
    IReadOnlyList<LineaComando> Lineas,
    DateOnly? FechaEmision = null,
    decimal? PorcentajeIrpf = null,
    string? Serie = null);

/// <summary>
/// Caso de uso: emitir una <b>factura rectificativa</b> (por sustitución). Referencia a la factura
/// original (que debe estar emitida), congela sus datos de cliente, numera en su serie (por defecto
/// <c>R</c>), genera su registro VeriFactu (tipo R1, encadenado) y marca la original como rectificada.
/// </summary>
public sealed class EmitirRectificativa
{
    /// <summary>Serie por defecto de las rectificativas.</summary>
    public const string SeriePorDefecto = "R";

    private readonly IConsultaProductos _productos;
    private readonly IServicioNumeracion _numeracion;
    private readonly IRepositorioFacturas _facturas;
    private readonly IConsultaEmpresas _empresas;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EmitirRectificativa(
        IConsultaProductos productos,
        IServicioNumeracion numeracion,
        IRepositorioFacturas facturas,
        IConsultaEmpresas empresas,
        IUnidadDeTrabajoFacturacion unidadDeTrabajo,
        IReloj reloj)
    {
        _productos = productos;
        _numeracion = numeracion;
        _facturas = facturas;
        _empresas = empresas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, Guid facturaOriginalId, EmitirRectificativaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.Lineas is null || comando.Lineas.Count == 0)
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("factura.sin_lineas", "La rectificativa debe tener al menos una línea."));
        }

        var original = await _facturas.ObtenerPorIdAsync(facturaOriginalId, ct).ConfigureAwait(false);
        if (original is null)
        {
            return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("factura.no_encontrada", "La factura original no existe."));
        }

        // F6: solo se rectifica una factura emitida (no una ya anulada/rectificada).
        var marcado = original.MarcarRectificada();
        if (marcado.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(marcado.Error);
        }

        var resolucion = await ResolucionLineasFactura.ResolverAsync(comando.Lineas, _productos, ct).ConfigureAwait(false);
        if (resolucion.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(resolucion.Error);
        }

        var cliente = new ClienteFacturado(
            original.ClienteId, original.ClienteNombre, original.ClienteNif,
            original.ClienteCalle, original.ClienteCodigoPostal, original.ClientePoblacion, original.ClienteProvincia, original.Pais);

        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var fecha = comando.FechaEmision ?? hoy;
        var porcentajeIrpf = comando.PorcentajeIrpf ?? original.PorcentajeIrpf;
        var serie = string.IsNullOrWhiteSpace(comando.Serie) ? SeriePorDefecto : comando.Serie;

        var numero = await _numeracion.SiguienteAsync(empresaId, TipoDocumento.Factura, fecha.Year, serie, ct).ConfigureAwait(false);
        if (numero.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(numero.Error);
        }

        var numeroFactura = new NumeroFactura(numero.Valor.Prefijo, numero.Valor.Ejercicio, numero.Valor.Numero);
        var rectificativa = Factura.EmitirRectificativa(
            empresaId, numeroFactura, fecha, cliente, resolucion.Valor, porcentajeIrpf, facturaOriginalId, comando.Motivo, _reloj);
        if (rectificativa.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(rectificativa.Error);
        }

        await RegistroVerifactu.AplicarAsync(empresaId, rectificativa.Valor, _empresas, _facturas, _reloj, ct).ConfigureAwait(false);
        _facturas.Agregar(rectificativa.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaDto.Desde(rectificativa.Valor));
    }
}

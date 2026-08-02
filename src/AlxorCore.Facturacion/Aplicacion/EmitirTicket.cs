using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Datos para emitir un ticket (factura simplificada) desde el TPV.</summary>
public sealed record EmitirTicketComando(
    IReadOnlyList<LineaComando> Lineas,
    Guid? ClienteId = null,
    string? Serie = null,
    DateOnly? FechaEmision = null);

/// <summary>
/// Caso de uso del TPV: emite un <b>ticket</b> (factura simplificada). Reutiliza la resolución de
/// líneas y la numeración correlativa; el destinatario es opcional (cliente de contado) y el importe
/// no puede superar el tope de la factura simplificada. Por defecto usa la serie <c>T</c>.
/// </summary>
public sealed class EmitirTicket
{
    /// <summary>Serie por defecto de los tickets.</summary>
    public const string SeriePorDefecto = "T";

    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IServicioNumeracion _numeracion;
    private readonly IRepositorioFacturas _facturas;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EmitirTicket(
        IConsultaClientes clientes,
        IConsultaProductos productos,
        IServicioNumeracion numeracion,
        IRepositorioFacturas facturas,
        IUnidadDeTrabajoFacturacion unidadDeTrabajo,
        IReloj reloj)
    {
        _clientes = clientes;
        _productos = productos;
        _numeracion = numeracion;
        _facturas = facturas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, EmitirTicketComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.Lineas is null || comando.Lineas.Count == 0)
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("ticket.sin_lineas", "El ticket debe tener al menos una línea."));
        }

        // Destinatario opcional: si se indica cliente se congelan sus datos; si no, "cliente de contado".
        var cliente = ClienteFacturado.Contado;
        if (comando.ClienteId is not null)
        {
            var datos = await _clientes.ObtenerAsync(comando.ClienteId.Value, ct).ConfigureAwait(false);
            if (datos is null)
            {
                return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
            }

            cliente = new ClienteFacturado(
                datos.Id, datos.Nombre, datos.NifFiscal, datos.Calle, datos.CodigoPostal, datos.Poblacion, datos.Provincia, datos.Pais);
        }

        var resolucion = await ResolucionLineasFactura.ResolverAsync(comando.Lineas, _productos, ct).ConfigureAwait(false);
        if (resolucion.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(resolucion.Error);
        }

        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var fecha = comando.FechaEmision ?? hoy;
        var serie = string.IsNullOrWhiteSpace(comando.Serie) ? SeriePorDefecto : comando.Serie;

        var numero = await _numeracion.SiguienteAsync(empresaId, TipoDocumento.Factura, fecha.Year, serie, ct).ConfigureAwait(false);
        if (numero.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(numero.Error);
        }

        var numeroFactura = new NumeroFactura(numero.Valor.Prefijo, numero.Valor.Ejercicio, numero.Valor.Numero);
        var ticket = Factura.EmitirSimplificada(empresaId, numeroFactura, fecha, cliente, resolucion.Valor, _reloj);
        if (ticket.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(ticket.Error);
        }

        _facturas.Agregar(ticket.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaDto.Desde(ticket.Valor));
    }
}

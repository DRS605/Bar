using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Línea de la factura a emitir. Si se indica <see cref="ProductoId"/>, se toman sus datos por defecto.</summary>
public sealed record LineaComando(
    decimal Cantidad,
    string? Descripcion = null,
    decimal? PrecioUnitario = null,
    string? CodigoIva = null,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null);

/// <summary>Datos para emitir una factura.</summary>
public sealed record EmitirFacturaComando(
    Guid ClienteId,
    IReadOnlyList<LineaComando> Lineas,
    DateOnly? FechaEmision = null,
    DateOnly? FechaOperacion = null,
    decimal? PorcentajeIrpf = null,
    string? Serie = null);

/// <summary>
/// Caso de uso estrella: emitir una factura. Compone cliente (Terceros), productos/impuestos
/// (Catálogo) y numeración correlativa (Organización). El número se asigna de forma atómica
/// <b>después</b> de validar todo, para minimizar el riesgo de huecos (invariante F1).
/// </summary>
public sealed class EmitirFactura
{
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IServicioNumeracion _numeracion;
    private readonly IRepositorioFacturas _facturas;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EmitirFactura(
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

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, EmitirFacturaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.Lineas is null || comando.Lineas.Count == 0)
        {
            return Resultado.Fallo<FacturaDto>(Error.Validacion("factura.sin_lineas", "La factura debe tener al menos una línea."));
        }

        var cliente = await _clientes.ObtenerAsync(comando.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var lineas = new List<NuevaLinea>(comando.Lineas.Count);
        foreach (var linea in comando.Lineas)
        {
            var resuelta = await ResolverLineaAsync(linea, ct).ConfigureAwait(false);
            if (resuelta.EsFallo)
            {
                return Resultado.Fallo<FacturaDto>(resuelta.Error);
            }

            lineas.Add(resuelta.Valor);
        }

        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var fechaEmision = comando.FechaEmision ?? hoy;
        var fechaOperacion = comando.FechaOperacion ?? fechaEmision;
        var porcentajeIrpf = comando.PorcentajeIrpf ?? cliente.PorcentajeIrpfDefecto;

        var clienteFacturado = new ClienteFacturado(
            cliente.Id, cliente.Nombre, cliente.NifFiscal,
            cliente.Calle, cliente.CodigoPostal, cliente.Poblacion, cliente.Provincia, cliente.Pais);

        // La numeración es lo último antes de crear y guardar (minimiza huecos).
        var numero = await _numeracion.SiguienteAsync(empresaId, TipoDocumento.Factura, fechaEmision.Year, comando.Serie, ct).ConfigureAwait(false);
        if (numero.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(numero.Error);
        }

        var numeroFactura = new NumeroFactura(numero.Valor.Prefijo, numero.Valor.Ejercicio, numero.Valor.Numero);
        var factura = Factura.Emitir(empresaId, numeroFactura, fechaEmision, fechaOperacion, clienteFacturado, lineas, porcentajeIrpf, _reloj);
        if (factura.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(factura.Error);
        }

        _facturas.Agregar(factura.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaDto.Desde(factura.Valor));
    }

    private async Task<Resultado<NuevaLinea>> ResolverLineaAsync(LineaComando linea, CancellationToken ct)
    {
        string? descripcion = linea.Descripcion;
        decimal? precio = linea.PrecioUnitario;
        string? codigoIva = linea.CodigoIva;

        if (linea.ProductoId is not null)
        {
            var producto = await _productos.ObtenerAsync(linea.ProductoId.Value, ct).ConfigureAwait(false);
            if (producto is null)
            {
                return Resultado.Fallo<NuevaLinea>(Error.NoEncontrado("producto.no_encontrado", "El producto de una línea no existe."));
            }

            descripcion ??= producto.Nombre;
            precio ??= producto.PrecioUnitario;
            codigoIva ??= producto.CodigoIva;
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return Resultado.Fallo<NuevaLinea>(Error.Validacion("factura.linea_sin_descripcion", "Cada línea necesita una descripción."));
        }

        if (precio is null)
        {
            return Resultado.Fallo<NuevaLinea>(Error.Validacion("factura.linea_sin_precio", "Cada línea necesita un precio."));
        }

        var impuesto = Impuesto.PorCodigoImpuesto(codigoIva ?? Impuesto.IvaGeneral.Codigo);
        if (impuesto.EsFallo)
        {
            return Resultado.Fallo<NuevaLinea>(impuesto.Error);
        }

        return Resultado.Ok(new NuevaLinea(
            descripcion, linea.Cantidad, precio.Value, impuesto.Valor.Codigo, impuesto.Valor.Porcentaje, linea.PorcentajeDescuento, linea.ProductoId));
    }
}

/// <summary>Caso de uso: listar las facturas de la empresa activa.</summary>
public sealed class ListarFacturas
{
    private readonly IConsultaFacturas _consulta;

    public ListarFacturas(IConsultaFacturas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<FacturaResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener una factura por su identificador.</summary>
public sealed class ObtenerFactura
{
    private readonly IConsultaFacturas _consulta;

    public ObtenerFactura(IConsultaFacturas consulta) => _consulta = consulta;

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _consulta.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
        return factura is null
            ? Resultado.Fallo<FacturaDto>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."))
            : Resultado.Ok(factura);
    }
}

using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

/// <summary>Vista de una línea de presupuesto.</summary>
public sealed record LineaPresupuestoDto(
    string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal PorcentajeDescuento,
    string CodigoIva, decimal PorcentajeIva, decimal Base, decimal CuotaIva);

/// <summary>Vista de un presupuesto.</summary>
public sealed record PresupuestoDto(
    Guid Id, string NumeroCompleto, Guid ClienteId, string ClienteNombre, DateOnly Fecha, DateOnly Validez,
    string Estado, decimal BaseImponible, decimal CuotaIva, decimal Total, Guid? FacturaId, IReadOnlyList<LineaPresupuestoDto> Lineas)
{
    public static PresupuestoDto Desde(Presupuesto p) => new(
        p.Id, p.NumeroCompleto, p.ClienteId, p.ClienteNombre, p.Fecha, p.Validez, p.Estado.ToString(),
        p.BaseImponible, p.CuotaIva, p.Total, p.FacturaId,
        p.Lineas.Select(l => new LineaPresupuestoDto(l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeDescuento, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva)).ToList());
}

/// <summary>Resumen de presupuesto para listados.</summary>
public sealed record PresupuestoResumen(
    Guid Id, string NumeroCompleto, DateOnly Fecha, DateOnly Validez, string ClienteNombre, decimal Total, string Estado, Guid? FacturaId);

/// <summary>Repositorio de presupuestos (escritura).</summary>
public interface IRepositorioPresupuestos
{
    Task<Presupuesto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    void Agregar(Presupuesto presupuesto);

    /// <summary>Siguiente número correlativo de presupuesto de la empresa para un ejercicio.</summary>
    Task<long> SiguienteNumeroAsync(Guid empresaId, int ejercicio, CancellationToken ct = default);
}

/// <summary>Consultas de lectura de presupuestos.</summary>
public interface IConsultaPresupuestos
{
    Task<PresupuestoDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PresupuestoResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default);
}

/// <summary>Datos para crear o actualizar un presupuesto.</summary>
public sealed record DatosPresupuesto(Guid ClienteId, IReadOnlyList<LineaComando> Lineas, int DiasValidez = 30);

/// <summary>Caso de uso: crear un presupuesto.</summary>
public sealed class CrearPresupuesto
{
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IRepositorioPresupuestos _presupuestos;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearPresupuesto(IConsultaClientes clientes, IConsultaProductos productos, IRepositorioPresupuestos presupuestos, IUnidadDeTrabajoFacturacion unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _productos = productos;
        _presupuestos = presupuestos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PresupuestoDto>> EjecutarAsync(Guid empresaId, DatosPresupuesto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cliente = await _clientes.ObtenerAsync(datos.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<PresupuestoDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var resolucion = await ResolucionLineasFactura.ResolverAsync(datos.Lineas ?? [], _productos, ct).ConfigureAwait(false);
        if (resolucion.EsFallo)
        {
            return Resultado.Fallo<PresupuestoDto>(resolucion.Error);
        }

        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var validez = hoy.AddDays(Math.Max(0, datos.DiasValidez));
        var numero = await _presupuestos.SiguienteNumeroAsync(empresaId, hoy.Year, ct).ConfigureAwait(false);
        var numeroCompleto = $"P{hoy.Year}/{numero:000000}";

        var presupuesto = Presupuesto.Crear(empresaId, numeroCompleto, cliente.Id, cliente.Nombre, hoy, validez, resolucion.Valor, _reloj);
        if (presupuesto.EsFallo)
        {
            return Resultado.Fallo<PresupuestoDto>(presupuesto.Error);
        }

        _presupuestos.Agregar(presupuesto.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PresupuestoDto.Desde(presupuesto.Valor));
    }
}

/// <summary>Caso de uso: actualizar un presupuesto en borrador.</summary>
public sealed class ActualizarPresupuesto
{
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IRepositorioPresupuestos _presupuestos;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;

    public ActualizarPresupuesto(IConsultaClientes clientes, IConsultaProductos productos, IRepositorioPresupuestos presupuestos, IUnidadDeTrabajoFacturacion unidadDeTrabajo)
    {
        _clientes = clientes;
        _productos = productos;
        _presupuestos = presupuestos;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado<PresupuestoDto>> EjecutarAsync(Guid id, DatosPresupuesto datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var presupuesto = await _presupuestos.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (presupuesto is null)
        {
            return Resultado.Fallo<PresupuestoDto>(Error.NoEncontrado("presupuesto.no_encontrado", "El presupuesto no existe."));
        }

        var cliente = await _clientes.ObtenerAsync(datos.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<PresupuestoDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var resolucion = await ResolucionLineasFactura.ResolverAsync(datos.Lineas ?? [], _productos, ct).ConfigureAwait(false);
        if (resolucion.EsFallo)
        {
            return Resultado.Fallo<PresupuestoDto>(resolucion.Error);
        }

        var validez = presupuesto.Fecha.AddDays(Math.Max(0, datos.DiasValidez));
        var r = presupuesto.Actualizar(cliente.Id, cliente.Nombre, validez, resolucion.Valor);
        if (r.EsFallo)
        {
            return Resultado.Fallo<PresupuestoDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(PresupuestoDto.Desde(presupuesto));
    }
}

/// <summary>Caso de uso: listar presupuestos.</summary>
public sealed class ListarPresupuestos
{
    private readonly IConsultaPresupuestos _consulta;

    public ListarPresupuestos(IConsultaPresupuestos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<PresupuestoResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener un presupuesto.</summary>
public sealed class ObtenerPresupuesto
{
    private readonly IConsultaPresupuestos _consulta;

    public ObtenerPresupuesto(IConsultaPresupuestos consulta) => _consulta = consulta;

    public async Task<Resultado<PresupuestoDto>> EjecutarAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _consulta.ObtenerAsync(id, ct).ConfigureAwait(false);
        return p is null ? Resultado.Fallo<PresupuestoDto>(Error.NoEncontrado("presupuesto.no_encontrado", "El presupuesto no existe.")) : Resultado.Ok(p);
    }
}

/// <summary>Caso de uso: rechazar un presupuesto.</summary>
public sealed class RechazarPresupuesto
{
    private readonly IRepositorioPresupuestos _presupuestos;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;

    public RechazarPresupuesto(IRepositorioPresupuestos presupuestos, IUnidadDeTrabajoFacturacion unidadDeTrabajo)
    {
        _presupuestos = presupuestos;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken ct = default)
    {
        var presupuesto = await _presupuestos.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (presupuesto is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("presupuesto.no_encontrado", "El presupuesto no existe."));
        }

        var r = presupuesto.MarcarRechazado();
        if (r.EsFallo)
        {
            return r;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: aceptar un presupuesto convirtiéndolo en factura.</summary>
public sealed class AceptarPresupuesto
{
    private readonly IRepositorioPresupuestos _presupuestos;
    private readonly EmitirFactura _emitirFactura;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;

    public AceptarPresupuesto(IRepositorioPresupuestos presupuestos, EmitirFactura emitirFactura, IUnidadDeTrabajoFacturacion unidadDeTrabajo)
    {
        _presupuestos = presupuestos;
        _emitirFactura = emitirFactura;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado<FacturaDto>> EjecutarAsync(Guid empresaId, Guid id, string? serie, int diasVencimiento, CancellationToken ct = default)
    {
        var presupuesto = await _presupuestos.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (presupuesto is null)
        {
            return Resultado.Fallo<FacturaDto>(Error.NoEncontrado("presupuesto.no_encontrado", "El presupuesto no existe."));
        }

        if (presupuesto.Estado != EstadoPresupuesto.Borrador)
        {
            return Resultado.Fallo<FacturaDto>(Error.Conflicto("presupuesto.no_borrador", "Solo se puede aceptar un presupuesto en borrador."));
        }

        var lineas = presupuesto.Lineas
            .Select(l => new LineaComando(l.Cantidad, l.Descripcion, l.PrecioUnitario, l.CodigoIva, l.PorcentajeDescuento, l.ProductoId))
            .ToList();

        var comando = new EmitirFacturaComando(presupuesto.ClienteId, lineas, Serie: serie, DiasVencimiento: diasVencimiento);
        var factura = await _emitirFactura.EjecutarAsync(empresaId, comando, ct).ConfigureAwait(false);
        if (factura.EsFallo)
        {
            return factura;
        }

        var r = presupuesto.MarcarAceptado(factura.Valor.Id);
        if (r.EsFallo)
        {
            return Resultado.Fallo<FacturaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return factura;
    }
}

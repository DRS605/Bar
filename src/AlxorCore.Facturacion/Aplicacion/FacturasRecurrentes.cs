using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Facturacion.Aplicacion;

// ---------------------------------------------------------------------------------------------
//  Contratos (DTOs y puertos)
// ---------------------------------------------------------------------------------------------

/// <summary>Línea de una plantilla recurrente. Si se indica <see cref="ProductoId"/> se toman sus datos.</summary>
public sealed record LineaRecurrenteComando(
    decimal Cantidad,
    string? Descripcion = null,
    decimal? PrecioUnitario = null,
    string? CodigoIva = null,
    decimal PorcentajeDescuento = 0m,
    Guid? ProductoId = null);

/// <summary>Datos para crear/actualizar una factura recurrente.</summary>
public sealed record DatosFacturaRecurrente(
    string Nombre,
    Guid ClienteId,
    Periodicidad Periodicidad,
    DateOnly PrimeraEmision,
    IReadOnlyList<LineaRecurrenteComando> Lineas,
    DateOnly? FechaFin = null,
    decimal? PorcentajeIrpf = null);

/// <summary>Vista de una línea de plantilla recurrente.</summary>
public sealed record LineaRecurrenteDto(
    string Descripcion, decimal Cantidad, decimal PrecioUnitario, decimal PorcentajeDescuento,
    string CodigoIva, decimal PorcentajeIva, decimal Base, decimal CuotaIva);

/// <summary>Vista completa de una factura recurrente.</summary>
public sealed record FacturaRecurrenteDto(
    Guid Id,
    string Nombre,
    Guid ClienteId,
    string Periodicidad,
    DateOnly ProximaEmision,
    DateOnly? FechaFin,
    decimal PorcentajeIrpf,
    bool Activa,
    int FacturasGeneradas,
    DateOnly? UltimaEmision,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal RetencionIrpf,
    decimal Total,
    IReadOnlyList<LineaRecurrenteDto> Lineas)
{
    public static FacturaRecurrenteDto Desde(FacturaRecurrente r)
    {
        var baseImponible = Redondeo.Dos(r.Lineas.Sum(l => l.Base));
        var cuotaIva = Redondeo.Dos(r.Lineas.Sum(l => l.CuotaIva));
        var retencion = Redondeo.Dos(baseImponible * r.PorcentajeIrpf / 100m);
        var total = Redondeo.Dos(baseImponible + cuotaIva - retencion);

        return new FacturaRecurrenteDto(
            r.Id, r.Nombre, r.ClienteId, r.Periodicidad.ToString(), r.ProximaEmision, r.FechaFin,
            r.PorcentajeIrpf, r.Activa, r.FacturasGeneradas, r.UltimaEmision,
            baseImponible, cuotaIva, retencion, total,
            r.Lineas.Select(l => new LineaRecurrenteDto(
                l.Descripcion, l.Cantidad, l.PrecioUnitario, l.PorcentajeDescuento, l.CodigoIva, l.PorcentajeIva, l.Base, l.CuotaIva)).ToList());
    }
}

/// <summary>Resumen de factura recurrente para listados.</summary>
public sealed record FacturaRecurrenteResumen(
    Guid Id, string Nombre, string ClienteNombre, string Periodicidad,
    DateOnly ProximaEmision, bool Activa, decimal Total);

/// <summary>Repositorio de facturas recurrentes (escritura y acceso al agregado).</summary>
public interface IRepositorioFacturasRecurrentes
{
    void Agregar(FacturaRecurrente recurrente);

    Task<FacturaRecurrente?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Recurrencias vencidas (a emitir) de la empresa activa.</summary>
    Task<IReadOnlyList<FacturaRecurrente>> ListarVencidasAsync(DateOnly hoy, CancellationToken ct = default);

    /// <summary>Empresas que tienen alguna recurrencia vencida (para el proceso automático multiempresa).</summary>
    Task<IReadOnlyList<Guid>> EmpresasConVencidasAsync(DateOnly hoy, CancellationToken ct = default);
}

/// <summary>Consultas de lectura de facturas recurrentes.</summary>
public interface IConsultaFacturasRecurrentes
{
    Task<FacturaRecurrenteDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<FacturaRecurrenteResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------------------------
//  Casos de uso
// ---------------------------------------------------------------------------------------------

/// <summary>Resuelve las líneas de una plantilla recurrente (comparte lógica con la emisión).</summary>
internal static class ResolucionLineasRecurrentes
{
    public static async Task<Resultado<List<LineaPlantilla>>> ResolverAsync(
        IReadOnlyList<LineaRecurrenteComando> lineas, IConsultaProductos productos, CancellationToken ct)
    {
        var resueltas = new List<LineaPlantilla>(lineas.Count);
        foreach (var linea in lineas)
        {
            string? descripcion = linea.Descripcion;
            decimal? precio = linea.PrecioUnitario;
            string? codigoIva = linea.CodigoIva;

            if (linea.ProductoId is not null)
            {
                var producto = await productos.ObtenerAsync(linea.ProductoId.Value, ct).ConfigureAwait(false);
                if (producto is null)
                {
                    return Resultado.Fallo<List<LineaPlantilla>>(Error.NoEncontrado("producto.no_encontrado", "El producto de una línea no existe."));
                }

                descripcion ??= producto.Nombre;
                precio ??= producto.PrecioUnitario;
                codigoIva ??= producto.CodigoIva;
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return Resultado.Fallo<List<LineaPlantilla>>(Error.Validacion("recurrente.linea_sin_descripcion", "Cada línea necesita una descripción."));
            }

            if (precio is null)
            {
                return Resultado.Fallo<List<LineaPlantilla>>(Error.Validacion("recurrente.linea_sin_precio", "Cada línea necesita un precio."));
            }

            var impuesto = Impuesto.PorCodigoImpuesto(codigoIva ?? Impuesto.IvaGeneral.Codigo);
            if (impuesto.EsFallo)
            {
                return Resultado.Fallo<List<LineaPlantilla>>(impuesto.Error);
            }

            resueltas.Add(new LineaPlantilla(
                descripcion, linea.Cantidad, precio.Value, impuesto.Valor.Codigo, impuesto.Valor.Porcentaje, linea.PorcentajeDescuento, linea.ProductoId));
        }

        return Resultado.Ok(resueltas);
    }
}

/// <summary>Caso de uso: crear una factura recurrente.</summary>
public sealed class CrearFacturaRecurrente
{
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaProductos _productos;
    private readonly IRepositorioFacturasRecurrentes _repositorio;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearFacturaRecurrente(
        IConsultaClientes clientes, IConsultaProductos productos, IRepositorioFacturasRecurrentes repositorio,
        IUnidadDeTrabajoFacturacion unidadDeTrabajo, IReloj reloj)
    {
        _clientes = clientes;
        _productos = productos;
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<FacturaRecurrenteDto>> EjecutarAsync(Guid empresaId, DatosFacturaRecurrente datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (datos.Lineas is null || datos.Lineas.Count == 0)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(Error.Validacion("recurrente.sin_lineas", "La factura recurrente debe tener al menos una línea."));
        }

        var cliente = await _clientes.ObtenerAsync(datos.ClienteId, ct).ConfigureAwait(false);
        if (cliente is null)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(Error.NoEncontrado("cliente.no_encontrado", "El cliente no existe."));
        }

        var lineas = await ResolucionLineasRecurrentes.ResolverAsync(datos.Lineas, _productos, ct).ConfigureAwait(false);
        if (lineas.EsFallo)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(lineas.Error);
        }

        var irpf = datos.PorcentajeIrpf ?? cliente.PorcentajeIrpfDefecto;
        var recurrente = FacturaRecurrente.Crear(
            empresaId, datos.Nombre, datos.ClienteId, datos.Periodicidad, datos.PrimeraEmision, datos.FechaFin, irpf, lineas.Valor, _reloj);
        if (recurrente.EsFallo)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(recurrente.Error);
        }

        _repositorio.Agregar(recurrente.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaRecurrenteDto.Desde(recurrente.Valor));
    }
}

/// <summary>Caso de uso: actualizar una factura recurrente existente.</summary>
public sealed class ActualizarFacturaRecurrente
{
    private readonly IConsultaProductos _productos;
    private readonly IRepositorioFacturasRecurrentes _repositorio;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;

    public ActualizarFacturaRecurrente(
        IConsultaProductos productos, IRepositorioFacturasRecurrentes repositorio, IUnidadDeTrabajoFacturacion unidadDeTrabajo)
    {
        _productos = productos;
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado<FacturaRecurrenteDto>> EjecutarAsync(Guid id, DatosFacturaRecurrente datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var recurrente = await _repositorio.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (recurrente is null)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(Error.NoEncontrado("recurrente.no_encontrada", "La factura recurrente no existe."));
        }

        var lineas = await ResolucionLineasRecurrentes.ResolverAsync(datos.Lineas ?? [], _productos, ct).ConfigureAwait(false);
        if (lineas.EsFallo)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(lineas.Error);
        }

        var actualizacion = recurrente.Actualizar(
            datos.Nombre, datos.Periodicidad, datos.PrimeraEmision, datos.FechaFin, datos.PorcentajeIrpf ?? recurrente.PorcentajeIrpf, lineas.Valor);
        if (actualizacion.EsFallo)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(actualizacion.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaRecurrenteDto.Desde(recurrente));
    }
}

/// <summary>Caso de uso: activar o pausar una factura recurrente.</summary>
public sealed class CambiarEstadoFacturaRecurrente
{
    private readonly IRepositorioFacturasRecurrentes _repositorio;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;

    public CambiarEstadoFacturaRecurrente(IRepositorioFacturasRecurrentes repositorio, IUnidadDeTrabajoFacturacion unidadDeTrabajo)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<Resultado<FacturaRecurrenteDto>> EjecutarAsync(Guid id, bool activa, CancellationToken ct = default)
    {
        var recurrente = await _repositorio.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (recurrente is null)
        {
            return Resultado.Fallo<FacturaRecurrenteDto>(Error.NoEncontrado("recurrente.no_encontrada", "La factura recurrente no existe."));
        }

        if (activa)
        {
            recurrente.Activar();
        }
        else
        {
            recurrente.Desactivar();
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(FacturaRecurrenteDto.Desde(recurrente));
    }
}

/// <summary>Caso de uso: listar las facturas recurrentes de la empresa activa.</summary>
public sealed class ListarFacturasRecurrentes
{
    private readonly IConsultaFacturasRecurrentes _consulta;

    public ListarFacturasRecurrentes(IConsultaFacturasRecurrentes consulta) => _consulta = consulta;

    public Task<IReadOnlyList<FacturaRecurrenteResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener una factura recurrente.</summary>
public sealed class ObtenerFacturaRecurrente
{
    private readonly IConsultaFacturasRecurrentes _consulta;

    public ObtenerFacturaRecurrente(IConsultaFacturasRecurrentes consulta) => _consulta = consulta;

    public async Task<Resultado<FacturaRecurrenteDto>> EjecutarAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await _consulta.ObtenerAsync(id, ct).ConfigureAwait(false);
        return dto is null
            ? Resultado.Fallo<FacturaRecurrenteDto>(Error.NoEncontrado("recurrente.no_encontrada", "La factura recurrente no existe."))
            : Resultado.Ok(dto);
    }
}

/// <summary>Resultado de un proceso de emisión automática.</summary>
public sealed record ResultadoEmisionRecurrente(int Emitidas, IReadOnlyList<Guid> FacturasCreadas);

/// <summary>
/// Caso de uso central de la facturación automática: emite todas las recurrencias vencidas de la
/// empresa activa. Cada una genera una factura ordinaria real (con numeración e invariantes) y
/// avanza su próxima fecha. Lo invoca tanto el proceso en segundo plano como el botón "emitir ahora".
/// </summary>
public sealed class EmitirFacturasRecurrentesVencidas
{
    private readonly IRepositorioFacturasRecurrentes _repositorio;
    private readonly EmitirFactura _emitir;
    private readonly IUnidadDeTrabajoFacturacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EmitirFacturasRecurrentesVencidas(
        IRepositorioFacturasRecurrentes repositorio, EmitirFactura emitir, IUnidadDeTrabajoFacturacion unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _emitir = emitir;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ResultadoEmisionRecurrente>> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime);
        var vencidas = await _repositorio.ListarVencidasAsync(hoy, ct).ConfigureAwait(false);

        var creadas = new List<Guid>();
        foreach (var recurrente in vencidas)
        {
            var comando = new EmitirFacturaComando(
                recurrente.ClienteId,
                recurrente.Lineas
                    .Select(l => new LineaComando(l.Cantidad, l.Descripcion, l.PrecioUnitario, l.CodigoIva, l.PorcentajeDescuento, l.ProductoId))
                    .ToList(),
                FechaEmision: hoy,
                PorcentajeIrpf: recurrente.PorcentajeIrpf);

            var factura = await _emitir.EjecutarAsync(empresaId, comando, ct).ConfigureAwait(false);
            if (factura.EsFallo)
            {
                // Una recurrencia mal configurada no debe frenar al resto; se omite y se sigue.
                continue;
            }

            recurrente.RegistrarEmision(hoy);
            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
            creadas.Add(factura.Valor.Id);
        }

        return Resultado.Ok(new ResultadoEmisionRecurrente(creadas.Count, creadas));
    }
}

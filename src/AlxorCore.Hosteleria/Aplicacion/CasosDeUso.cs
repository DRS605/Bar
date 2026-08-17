using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Datos de una mesa para crear o actualizar.</summary>
public sealed record DatosMesa(string Nombre, string? Zona = null, int Capacidad = 0, string? Forma = null, double PosX = 0, double PosY = 0)
{
    /// <summary>Traduce la forma recibida (texto) a su enumerado; por defecto, cuadrada.</summary>
    public FormaMesa FormaMesa() => Enum.TryParse<FormaMesa>(Forma, ignoreCase: true, out var f) ? f : Dominio.FormaMesa.Cuadrada;
}

/// <summary>Nueva posición de una mesa en el plano.</summary>
public sealed record DatosPosicion(double PosX, double PosY);

/// <summary>Datos para añadir una línea a una comanda (un producto del catálogo y su cantidad).</summary>
public sealed record DatosLineaComanda(Guid ProductoId, decimal Cantidad = 1m);

/// <summary>Datos para fijar la cantidad de una línea existente.</summary>
public sealed record DatosCantidadLinea(decimal Cantidad);

/// <summary>Datos para abrir una comanda en una mesa.</summary>
public sealed record DatosAbrirComanda(Guid MesaId, string? Notas = null);

/// <summary>Datos para cobrar una comanda.</summary>
public sealed record DatosCobro(MetodoCobro Metodo = MetodoCobro.Efectivo, Guid? ClienteId = null, string? Serie = null);

/// <summary>Caso de uso: crear una mesa.</summary>
public sealed class CrearMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearMesa(IRepositorioMesas mesas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MesaDto>> EjecutarAsync(Guid empresaId, DatosMesa datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = Mesa.Crear(empresaId, datos.Nombre, datos.Zona, datos.Capacidad, _reloj, datos.FormaMesa(), datos.PosX, datos.PosY);
        if (mesa.EsFallo)
        {
            return Resultado.Fallo<MesaDto>(mesa.Error);
        }

        _mesas.Agregar(mesa.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(MesaDto.Desde(mesa.Valor));
    }
}

/// <summary>Caso de uso: actualizar una mesa.</summary>
public sealed class ActualizarMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarMesa(IRepositorioMesas mesas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MesaDto>> EjecutarAsync(Guid mesaId, DatosMesa datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = await _mesas.ObtenerPorIdAsync(mesaId, ct).ConfigureAwait(false);
        if (mesa is null)
        {
            return Resultado.Fallo<MesaDto>(Error.NoEncontrado("mesa.no_encontrada", "La mesa no existe."));
        }

        var r = mesa.Actualizar(datos.Nombre, datos.Zona, datos.Capacidad, _reloj, datos.FormaMesa());
        if (r.EsFallo)
        {
            return Resultado.Fallo<MesaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(MesaDto.Desde(mesa));
    }
}

/// <summary>Caso de uso: recolocar una mesa en el plano del local.</summary>
public sealed class MoverMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public MoverMesa(IRepositorioMesas mesas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<MesaDto>> EjecutarAsync(Guid mesaId, DatosPosicion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = await _mesas.ObtenerPorIdAsync(mesaId, ct).ConfigureAwait(false);
        if (mesa is null)
        {
            return Resultado.Fallo<MesaDto>(Error.NoEncontrado("mesa.no_encontrada", "La mesa no existe."));
        }

        mesa.Colocar(datos.PosX, datos.PosY, _reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(MesaDto.Desde(mesa));
    }
}

/// <summary>Caso de uso: desactivar (retirar) una mesa. No se puede si tiene una comanda abierta.</summary>
public sealed class DesactivarMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public DesactivarMesa(IRepositorioMesas mesas, IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid mesaId, CancellationToken ct = default)
    {
        var mesa = await _mesas.ObtenerPorIdAsync(mesaId, ct).ConfigureAwait(false);
        if (mesa is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("mesa.no_encontrada", "La mesa no existe."));
        }

        var abierta = await _comandas.ObtenerAbiertaPorMesaAsync(mesaId, ct).ConfigureAwait(false);
        if (abierta is not null)
        {
            return Resultado.Fallo(Error.Conflicto("mesa.ocupada", "No se puede retirar una mesa con una comanda abierta."));
        }

        mesa.Desactivar(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso: listar las mesas de la empresa activa con su ocupación.</summary>
public sealed class ListarMesas
{
    private readonly IConsultaMesas _consulta;

    public ListarMesas(IConsultaMesas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<MesaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, incluirInactivas: false, ct);
}

/// <summary>Caso de uso: abrir una comanda en una mesa libre.</summary>
public sealed class AbrirComanda
{
    private readonly IRepositorioMesas _mesas;
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AbrirComanda(IRepositorioMesas mesas, IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid empresaId, DatosAbrirComanda datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = await _mesas.ObtenerPorIdAsync(datos.MesaId, ct).ConfigureAwait(false);
        if (mesa is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("mesa.no_encontrada", "La mesa no existe."));
        }

        if (!mesa.Activa)
        {
            return Resultado.Fallo<ComandaDto>(Error.Conflicto("mesa.inactiva", "La mesa está retirada."));
        }

        var abierta = await _comandas.ObtenerAbiertaPorMesaAsync(datos.MesaId, ct).ConfigureAwait(false);
        if (abierta is not null)
        {
            return Resultado.Fallo<ComandaDto>(Error.Conflicto("mesa.ocupada", "La mesa ya tiene una comanda abierta."));
        }

        var comanda = Comanda.Abrir(empresaId, datos.MesaId, datos.Notas, _reloj);
        _comandas.Agregar(comanda);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Caso de uso: añadir una línea (un producto pedido) a una comanda abierta.</summary>
public sealed class AgregarLineaComanda
{
    private readonly IRepositorioComandas _comandas;
    private readonly IConsultaProductos _productos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AgregarLineaComanda(IRepositorioComandas comandas, IConsultaProductos productos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid comandaId, DatosLineaComanda datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var producto = await _productos.ObtenerAsync(datos.ProductoId, ct).ConfigureAwait(false);
        if (producto is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("producto.no_encontrado", "El producto no existe."));
        }

        var linea = comanda.AgregarLinea(producto.Id, producto.Nombre, datos.Cantidad, producto.PrecioUnitario, producto.CodigoIva, producto.PorcentajeIva, _reloj);
        if (linea.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(linea.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Caso de uso: fijar la cantidad de una línea (botones +/− del TPV de mesa).</summary>
public sealed class FijarCantidadLineaComanda
{
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public FijarCantidadLineaComanda(IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid comandaId, Guid lineaId, DatosCantidadLinea datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var r = comanda.FijarCantidadLinea(lineaId, datos.Cantidad, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Un artículo que se envía a cocina (cantidad nueva de este envío).</summary>
public sealed record ArticuloCocinaDto(decimal Cantidad, string Descripcion);

/// <summary>Lo que se manda a cocina/barra al enviar una comanda: mesa, hora y artículos nuevos.</summary>
public sealed record ComandaCocinaDto(Guid ComandaId, Guid MesaId, DateTimeOffset Hora, IReadOnlyList<ArticuloCocinaDto> Articulos, string? Notas);

/// <summary>Caso de uso: enviar a cocina/barra la parte pendiente de una comanda (marca y devuelve lo nuevo).</summary>
public sealed class EnviarComandaCocina
{
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public EnviarComandaCocina(IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaCocinaDto>> EjecutarAsync(Guid comandaId, CancellationToken ct = default)
    {
        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<ComandaCocinaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var r = comanda.EnviarACocina();
        if (r.EsFallo)
        {
            return Resultado.Fallo<ComandaCocinaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        var articulos = r.Valor.Select(a => new ArticuloCocinaDto(a.Cantidad, a.Descripcion)).ToList();
        return Resultado.Ok(new ComandaCocinaDto(comanda.Id, comanda.MesaId, _reloj.AhoraUtc, articulos, comanda.Notas));
    }
}

/// <summary>Caso de uso: quitar una línea de una comanda abierta.</summary>
public sealed class QuitarLineaComanda
{
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public QuitarLineaComanda(IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid comandaId, Guid lineaId, CancellationToken ct = default)
    {
        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var r = comanda.QuitarLinea(lineaId, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Caso de uso: listar las comandas abiertas de la empresa activa.</summary>
public sealed class ListarComandasAbiertas
{
    private readonly IConsultaComandas _consulta;

    public ListarComandasAbiertas(IConsultaComandas consulta) => _consulta = consulta;

    public Task<IReadOnlyList<ComandaResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAbiertasAsync(empresaId, ct);
}

/// <summary>Caso de uso: obtener una comanda por su identificador.</summary>
public sealed class ObtenerComanda
{
    private readonly IConsultaComandas _consulta;

    public ObtenerComanda(IConsultaComandas consulta) => _consulta = consulta;

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid comandaId, CancellationToken ct = default)
    {
        var comanda = await _consulta.ObtenerAsync(comandaId, ct).ConfigureAwait(false);
        return comanda is null
            ? Resultado.Fallo<ComandaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."))
            : Resultado.Ok(comanda);
    }
}

/// <summary>Caso de uso: anular una comanda abierta sin cobrarla.</summary>
public sealed class AnularComanda
{
    private readonly IRepositorioComandas _comandas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AnularComanda(IRepositorioComandas comandas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid comandaId, CancellationToken ct = default)
    {
        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var r = comanda.Anular(_reloj);
        if (r.EsFallo)
        {
            return r;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>
/// Caso de uso: cobrar una comanda. Emite un ticket (factura simplificada) con las líneas
/// «congeladas» de la comanda —lo que asigna número correlativo, deja el registro VeriFactu y
/// descuenta existencias— y luego marca la comanda como cobrada, liberando la mesa.
/// </summary>
public sealed class CobrarComanda
{
    private readonly IRepositorioComandas _comandas;
    private readonly EmitirTicket _emitirTicket;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CobrarComanda(IRepositorioComandas comandas, EmitirTicket emitirTicket, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _emitirTicket = emitirTicket;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid empresaId, Guid comandaId, DatosCobro datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        if (comanda.Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<ComandaDto>(Error.Conflicto("comanda.no_abierta", "Solo se puede cobrar una comanda abierta."));
        }

        if (comanda.Lineas.Count == 0)
        {
            return Resultado.Fallo<ComandaDto>(Error.Validacion("comanda.sin_lineas", "No se puede cobrar una comanda vacía."));
        }

        // El precio y el IVA se congelaron al pedir cada línea: se pasan explícitos al ticket para
        // que no dependa de la tarifa actual del catálogo. El ProductoId permite descontar stock.
        var lineas = comanda.Lineas
            .Select(l => new LineaComando(l.Cantidad, l.Descripcion, l.PrecioUnitario, l.CodigoIva, 0m, l.ProductoId))
            .ToList();

        var ticket = await _emitirTicket.EjecutarAsync(empresaId, new EmitirTicketComando(lineas, datos.ClienteId, datos.Serie), ct).ConfigureAwait(false);
        if (ticket.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(ticket.Error);
        }

        var r = comanda.MarcarCobrada(ticket.Valor.Id, ticket.Valor.NumeroCompleto, datos.Metodo, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Datos de un cobro parcial: los artículos que se cobran ahora y la forma de cobro.</summary>
public sealed record DatosCobroParcial(
    IReadOnlyList<ItemCobroParcial> Items,
    MetodoCobro Metodo = MetodoCobro.Efectivo,
    Guid? ClienteId = null,
    string? Serie = null);

/// <summary>Resultado de un cobro parcial: el ticket emitido y el estado de la comanda tras el cobro.</summary>
public sealed record CobroParcialDto(Guid FacturaId, string NumeroTicket, decimal Total, bool Cerrada, ComandaDto Comanda);

/// <summary>
/// Caso de uso del reparto de cuenta: cobra <b>parte</b> de una comanda emitiendo un ticket solo por
/// los artículos indicados (con sus cantidades). Descuenta esas cantidades del pendiente de la comanda
/// y, cuando ya no queda nada por cobrar, la cierra y libera la mesa. Permite que cada comensal pague
/// lo suyo con su propio ticket, dejando la mesa abierta hasta el último pago.
/// </summary>
public sealed class CobrarComandaParcial
{
    private readonly IRepositorioComandas _comandas;
    private readonly EmitirTicket _emitirTicket;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CobrarComandaParcial(IRepositorioComandas comandas, EmitirTicket emitirTicket, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _comandas = comandas;
        _emitirTicket = emitirTicket;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<CobroParcialDto>> EjecutarAsync(Guid empresaId, Guid comandaId, DatosCobroParcial datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var comanda = await _comandas.ObtenerPorIdAsync(comandaId, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<CobroParcialDto>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        // 1) Validar (sin mutar) que las cantidades a cobrar caben en el pendiente y resolver sus líneas.
        var billing = comanda.ValidarCobroParcial(datos.Items);
        if (billing.EsFallo)
        {
            return Resultado.Fallo<CobroParcialDto>(billing.Error);
        }

        // El precio y el IVA van congelados desde la comanda; el ProductoId permite descontar stock.
        var lineas = billing.Valor
            .Select(l => new LineaComando(l.Cantidad, l.Descripcion, l.PrecioUnitario, l.CodigoIva, 0m, l.ProductoId))
            .ToList();

        // 2) Emitir el ticket de esos artículos (transacción propia de Facturación).
        var ticket = await _emitirTicket.EjecutarAsync(empresaId, new EmitirTicketComando(lineas, datos.ClienteId, datos.Serie), ct).ConfigureAwait(false);
        if (ticket.EsFallo)
        {
            return Resultado.Fallo<CobroParcialDto>(ticket.Error);
        }

        // 3) Asentar el cobro en la comanda; si con esto queda todo pagado, se cierra y libera la mesa.
        var r = comanda.AplicarCobroParcial(datos.Items, ticket.Valor.Id, ticket.Valor.NumeroCompleto, datos.Metodo, _reloj);
        if (r.EsFallo)
        {
            return Resultado.Fallo<CobroParcialDto>(r.Error);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(new CobroParcialDto(
            ticket.Valor.Id, ticket.Valor.NumeroCompleto, ticket.Valor.Total,
            comanda.Estado == EstadoComanda.Cobrada, ComandaDto.Desde(comanda)));
    }
}

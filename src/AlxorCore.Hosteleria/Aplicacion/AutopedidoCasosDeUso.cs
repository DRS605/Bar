using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Un artículo que el cliente añade a su pedido desde el móvil.</summary>
public sealed record ItemPedidoWeb(Guid ProductoId, decimal Cantidad = 1m, string? Nota = null);

/// <summary>Pedido que el cliente envía desde la mesa (autopedido por QR).</summary>
public sealed record DatosPedidoWeb(Guid Token, string? Idioma, IReadOnlyList<ItemPedidoWeb> Items);

/// <summary>Confirmación de un pedido web recibido.</summary>
public sealed record PedidoWebCreadoDto(Guid Id, int NumeroLineas);

/// <summary>Aviso que el cliente lanza desde la mesa.</summary>
public sealed record DatosAvisoMesa(Guid Token, string Tipo);

/// <summary>Alta o edición de una traducción de la carta.</summary>
public sealed record DatosTraduccion(string Ambito, string Clave, string Idioma, string? Nombre, string? Descripcion);

/// <summary>Utilidades de conversión de los idiomas/tipos que llegan como texto de la API pública.</summary>
public static class Autopedido
{
    public static IdiomaCarta IdiomaDe(string? valor) => (valor?.Trim().ToLowerInvariant()) switch
    {
        "en" or "en-gb" or "en-us" or "english" => IdiomaCarta.En,
        "fr" or "fr-fr" or "français" or "francais" => IdiomaCarta.Fr,
        _ => IdiomaCarta.Es,
    };

    public static string CodigoDe(IdiomaCarta idioma) => idioma switch
    {
        IdiomaCarta.En => "en",
        IdiomaCarta.Fr => "fr",
        _ => "es",
    };
}

/// <summary>
/// Caso de uso <b>anónimo</b>: el cliente envía un pedido desde la mesa escaneando su QR. Se valida
/// que la mesa existe, está activa y que el token del QR coincide (para no poder pedir a otra mesa),
/// y se guarda como <see cref="EstadoPedidoWeb.Pendiente"/> a la espera de que el camarero lo acepte.
/// </summary>
public sealed class CrearPedidoWeb
{
    private readonly IRepositorioMesas _mesas;
    private readonly IConsultaProductos _productos;
    private readonly IRepositorioPedidosWeb _pedidos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearPedidoWeb(IRepositorioMesas mesas, IConsultaProductos productos, IRepositorioPedidosWeb pedidos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _productos = productos;
        _pedidos = pedidos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<PedidoWebCreadoDto>> EjecutarAsync(Guid empresaId, Guid mesaId, DatosPedidoWeb datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = await _mesas.ObtenerPorIdAsync(mesaId, ct).ConfigureAwait(false);
        var guard = ValidarMesaToken(mesa, empresaId, datos.Token);
        if (guard is not null)
        {
            return Resultado.Fallo<PedidoWebCreadoDto>(guard);
        }

        if (datos.Items is null || datos.Items.Count == 0)
        {
            return Resultado.Fallo<PedidoWebCreadoDto>(Error.Validacion("pedido_web.sin_lineas", "El pedido no tiene artículos."));
        }

        // Solo productos del catálogo de este local y activos (evita pedidos con artículos ajenos/retirados).
        var items = new List<(Guid, string, decimal, string?)>(datos.Items.Count);
        foreach (var item in datos.Items.Where(i => i.Cantidad > 0))
        {
            var producto = await _productos.ObtenerAsync(item.ProductoId, ct).ConfigureAwait(false);
            if (producto is null || !producto.Activo)
            {
                return Resultado.Fallo<PedidoWebCreadoDto>(Error.NoEncontrado("producto.no_encontrado", "Un artículo del pedido no está disponible."));
            }

            items.Add((item.ProductoId, producto.Nombre, item.Cantidad, item.Nota));
        }

        var pedido = PedidoWeb.Crear(empresaId, mesaId, Autopedido.IdiomaDe(datos.Idioma), items, _reloj);
        if (pedido.EsFallo)
        {
            return Resultado.Fallo<PedidoWebCreadoDto>(pedido.Error);
        }

        _pedidos.Agregar(pedido.Valor);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(new PedidoWebCreadoDto(pedido.Valor.Id, pedido.Valor.Lineas.Count));
    }

    internal static Error? ValidarMesaToken(Mesa? mesa, Guid empresaId, Guid token)
    {
        if (mesa is null || mesa.EmpresaId != empresaId)
        {
            return Error.NoEncontrado("mesa.no_encontrada", "La mesa no existe.");
        }

        if (!mesa.Activa)
        {
            return Error.Conflicto("mesa.inactiva", "La mesa está retirada.");
        }

        if (token == Guid.Empty || mesa.TokenCarta != token)
        {
            return Error.Prohibido("carta.token_invalido", "El código QR de la mesa no es válido.");
        }

        return null;
    }
}

/// <summary>Caso de uso (personal): lista los pedidos web pendientes de aceptar de la empresa.</summary>
public sealed class ListarPedidosWebPendientes
{
    private readonly IConsultaPedidosWeb _consulta;

    public ListarPedidosWebPendientes(IConsultaPedidosWeb consulta) => _consulta = consulta;

    public Task<IReadOnlyList<PedidoWebResumen>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarPendientesAsync(empresaId, ct);
}

/// <summary>
/// Caso de uso (personal): acepta un pedido web. Abre la comanda de la mesa si estaba libre, añade
/// sus líneas (con la nota del cliente) y marca el pedido como aceptado. No lo manda a cocina: eso lo
/// decide el camarero con el botón de siempre.
/// </summary>
public sealed class AceptarPedidoWeb
{
    private readonly IRepositorioPedidosWeb _pedidos;
    private readonly IRepositorioComandas _comandas;
    private readonly IRepositorioMesas _mesas;
    private readonly IConsultaProductos _productos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AceptarPedidoWeb(IRepositorioPedidosWeb pedidos, IRepositorioComandas comandas, IRepositorioMesas mesas, IConsultaProductos productos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _pedidos = pedidos;
        _comandas = comandas;
        _mesas = mesas;
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ComandaDto>> EjecutarAsync(Guid empresaId, Guid pedidoId, CancellationToken ct = default)
    {
        var pedido = await _pedidos.ObtenerPorIdAsync(pedidoId, ct).ConfigureAwait(false);
        if (pedido is null)
        {
            return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("pedido_web.no_encontrado", "El pedido no existe."));
        }

        if (pedido.Estado != EstadoPedidoWeb.Pendiente)
        {
            return Resultado.Fallo<ComandaDto>(Error.Conflicto("pedido_web.no_pendiente", "El pedido ya no está pendiente."));
        }

        var mesa = await _mesas.ObtenerPorIdAsync(pedido.MesaId, ct).ConfigureAwait(false);
        if (mesa is null || !mesa.Activa)
        {
            return Resultado.Fallo<ComandaDto>(Error.Conflicto("mesa.inactiva", "La mesa ya no está disponible."));
        }

        var comanda = await _comandas.ObtenerAbiertaPorMesaAsync(pedido.MesaId, ct).ConfigureAwait(false);
        var comandaNueva = comanda is null;
        comanda ??= Comanda.Abrir(empresaId, pedido.MesaId, null, _reloj);

        foreach (var linea in pedido.Lineas)
        {
            var producto = await _productos.ObtenerAsync(linea.ProductoId, ct).ConfigureAwait(false);
            if (producto is null)
            {
                return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("producto.no_encontrado", "Un artículo del pedido ya no existe."));
            }

            var agregada = comanda.AgregarLinea(producto.Id, producto.Nombre, linea.Cantidad, producto.PrecioUnitario, producto.CodigoIva, producto.PorcentajeIva, _reloj);
            if (agregada.EsFallo)
            {
                return Resultado.Fallo<ComandaDto>(agregada.Error);
            }

            if (!string.IsNullOrWhiteSpace(linea.Nota))
            {
                comanda.CambiarNotaLinea(agregada.Valor.Id, linea.Nota);
            }
        }

        var aceptar = pedido.Aceptar(comanda.Id, _reloj);
        if (aceptar.EsFallo)
        {
            return Resultado.Fallo<ComandaDto>(aceptar.Error);
        }

        if (comandaNueva)
        {
            _comandas.Agregar(comanda);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ComandaDto.Desde(comanda));
    }
}

/// <summary>Caso de uso (personal): rechaza un pedido web pendiente.</summary>
public sealed class RechazarPedidoWeb
{
    private readonly IRepositorioPedidosWeb _pedidos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RechazarPedidoWeb(IRepositorioPedidosWeb pedidos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _pedidos = pedidos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid pedidoId, CancellationToken ct = default)
    {
        var pedido = await _pedidos.ObtenerPorIdAsync(pedidoId, ct).ConfigureAwait(false);
        if (pedido is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("pedido_web.no_encontrado", "El pedido no existe."));
        }

        var r = pedido.Rechazar(_reloj);
        if (r.EsFallo)
        {
            return r;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso <b>anónimo</b>: el cliente lanza un aviso desde la mesa (llamar / pedir la cuenta).</summary>
public sealed class CrearAvisoMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IRepositorioAvisos _avisos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearAvisoMesa(IRepositorioMesas mesas, IRepositorioAvisos avisos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _avisos = avisos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid mesaId, DatosAvisoMesa datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var mesa = await _mesas.ObtenerPorIdAsync(mesaId, ct).ConfigureAwait(false);
        var guard = CrearPedidoWeb.ValidarMesaToken(mesa, empresaId, datos.Token);
        if (guard is not null)
        {
            return Resultado.Fallo(guard);
        }

        if (!Enum.TryParse<TipoAvisoMesa>(datos.Tipo, ignoreCase: true, out var tipo))
        {
            return Resultado.Fallo(Error.Validacion("aviso.tipo_invalido", "El tipo de aviso no es válido."));
        }

        _avisos.Agregar(AvisoMesa.Crear(empresaId, mesaId, tipo, _reloj));
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso (personal): lista los avisos de mesa pendientes de atender.</summary>
public sealed class ListarAvisosPendientes
{
    private readonly IConsultaAvisos _consulta;

    public ListarAvisosPendientes(IConsultaAvisos consulta) => _consulta = consulta;

    public Task<IReadOnlyList<AvisoMesaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarPendientesAsync(empresaId, ct);
}

/// <summary>Caso de uso (personal): marca un aviso de mesa como atendido.</summary>
public sealed class AtenderAviso
{
    private readonly IRepositorioAvisos _avisos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public AtenderAviso(IRepositorioAvisos avisos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _avisos = avisos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid avisoId, CancellationToken ct = default)
    {
        var aviso = await _avisos.ObtenerPorIdAsync(avisoId, ct).ConfigureAwait(false);
        if (aviso is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("aviso.no_encontrado", "El aviso no existe."));
        }

        var r = aviso.Atender(_reloj);
        if (r.EsFallo)
        {
            return r;
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso (personal): lista todas las traducciones de la carta de la empresa.</summary>
public sealed class ListarTraducciones
{
    private readonly IConsultaTraducciones _consulta;

    public ListarTraducciones(IConsultaTraducciones consulta) => _consulta = consulta;

    public Task<IReadOnlyList<TraduccionCartaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>
/// Caso de uso (personal): guarda (crea o edita) una traducción de la carta. Un nombre vacío borra la
/// traducción existente (volver al español base).
/// </summary>
public sealed class GuardarTraduccion
{
    private readonly IRepositorioTraducciones _traducciones;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarTraduccion(IRepositorioTraducciones traducciones, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _traducciones = traducciones;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, DatosTraduccion datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (!Enum.TryParse<AmbitoTraduccion>(datos.Ambito, ignoreCase: true, out var ambito))
        {
            return Resultado.Fallo(Error.Validacion("traduccion.ambito_invalido", "El ámbito de la traducción no es válido."));
        }

        if (string.IsNullOrWhiteSpace(datos.Clave))
        {
            return Resultado.Fallo(Error.Validacion("traduccion.clave_vacia", "Falta el elemento a traducir."));
        }

        var idioma = Autopedido.IdiomaDe(datos.Idioma);
        var clave = datos.Clave.Trim();
        var existente = await _traducciones.ObtenerAsync(empresaId, ambito, clave, idioma, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(datos.Nombre) && string.IsNullOrWhiteSpace(datos.Descripcion))
        {
            // Sin texto: borrar la traducción si existía.
            if (existente is not null)
            {
                _traducciones.Quitar(existente);
                await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
            }

            return Resultado.Ok();
        }

        var nombre = string.IsNullOrWhiteSpace(datos.Nombre) ? clave : datos.Nombre!;
        if (existente is null)
        {
            _traducciones.Agregar(TraduccionCarta.Crear(empresaId, ambito, clave, idioma, nombre, datos.Descripcion, _reloj));
        }
        else
        {
            existente.Actualizar(nombre, datos.Descripcion, _reloj);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Caso de uso (personal): genera un token de carta nuevo para una mesa (invalida sus QR).</summary>
public sealed class RegenerarTokenCartaMesa
{
    private readonly IRepositorioMesas _mesas;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public RegenerarTokenCartaMesa(IRepositorioMesas mesas, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
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

        mesa.RegenerarTokenCarta(_reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

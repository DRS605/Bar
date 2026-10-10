using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Aplicacion;

/// <summary>Un artículo que el cliente añade a su pedido desde el móvil.</summary>
public sealed record ItemPedidoWeb(Guid ProductoId, decimal Cantidad = 1m, string? Nota = null);

/// <summary>Un menú del día que el cliente pide desde el móvil, con los platos elegidos por sección.</summary>
public sealed record MenuPedidoWeb(IReadOnlyList<string> Platos, decimal Cantidad = 1m);

/// <summary>Pedido que el cliente envía desde la mesa (autopedido por QR).</summary>
public sealed record DatosPedidoWeb(Guid Token, string? Idioma, IReadOnlyList<ItemPedidoWeb> Items, IReadOnlyList<MenuPedidoWeb>? Menus = null);

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
    private readonly IRepositorioFichasCarta _fichas;
    private readonly IRepositorioMenuDia _menuDia;
    private readonly IRepositorioPedidosWeb _pedidos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearPedidoWeb(IRepositorioMesas mesas, IConsultaProductos productos, IRepositorioFichasCarta fichas, IRepositorioMenuDia menuDia, IRepositorioPedidosWeb pedidos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _mesas = mesas;
        _productos = productos;
        _fichas = fichas;
        _menuDia = menuDia;
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

        var hayItems = datos.Items is { Count: > 0 };
        var hayMenus = datos.Menus is { Count: > 0 };
        if (!hayItems && !hayMenus)
        {
            return Resultado.Fallo<PedidoWebCreadoDto>(Error.Validacion("pedido_web.sin_lineas", "El pedido no tiene artículos."));
        }

        // Solo productos del catálogo de este local y activos (evita pedidos con artículos ajenos/retirados).
        var items = new List<(Guid, string, decimal, string?, decimal?)>();
        foreach (var item in (datos.Items ?? Array.Empty<ItemPedidoWeb>()).Where(i => i.Cantidad > 0))
        {
            var producto = await _productos.ObtenerAsync(item.ProductoId, ct).ConfigureAwait(false);
            if (producto is null || !producto.Activo)
            {
                return Resultado.Fallo<PedidoWebCreadoDto>(Error.NoEncontrado("producto.no_encontrado", "Un artículo del pedido no está disponible."));
            }

            var ficha = await _fichas.ObtenerPorProductoAsync(item.ProductoId, ct).ConfigureAwait(false);
            if (ficha is { Agotado: true })
            {
                return Resultado.Fallo<PedidoWebCreadoDto>(Error.Conflicto("producto.agotado", $"«{producto.Nombre}» está agotado."));
            }

            items.Add((item.ProductoId, producto.Nombre, item.Cantidad, item.Nota, (decimal?)null));
        }

        // Menú del día: línea sin producto, con el precio cerrado del menú (lo fija el servidor, no el cliente).
        if (hayMenus)
        {
            var menu = await _menuDia.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
            if (menu is null || !menu.Activo)
            {
                return Resultado.Fallo<PedidoWebCreadoDto>(Error.Conflicto("menu_dia.no_disponible", "El menú del día no está disponible."));
            }

            foreach (var m in datos.Menus!.Where(x => x.Cantidad > 0))
            {
                var platos = (m.Platos ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
                var nota = platos.Count > 0 ? string.Join(" · ", platos) : null;
                items.Add((Guid.Empty, "Menú del día", m.Cantidad, nota, menu.Precio));
            }
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
        comanda ??= Comanda.Abrir(empresaId, pedido.MesaId, null, usuarioId: null, usuarioNombre: null, _reloj);

        foreach (var linea in pedido.Lineas)
        {
            Resultado<LineaComanda> agregada;
            if (linea.Precio is { } precioMenu)
            {
                // Línea sin producto (menú del día): se añade con su precio cerrado e IVA de hostelería (10 %).
                agregada = comanda.AgregarLinea(Guid.Empty, linea.Descripcion, linea.Cantidad, precioMenu, "IVA10", 10m, _reloj);
            }
            else
            {
                var producto = await _productos.ObtenerAsync(linea.ProductoId, ct).ConfigureAwait(false);
                if (producto is null)
                {
                    return Resultado.Fallo<ComandaDto>(Error.NoEncontrado("producto.no_encontrado", "Un artículo del pedido ya no existe."));
                }

                agregada = comanda.AgregarLinea(producto.Id, producto.Nombre, linea.Cantidad, producto.PrecioUnitario, producto.CodigoIva, producto.PorcentajeIva, _reloj);
            }

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

/// <summary>Alta o edición de la ficha de carta (alérgenos y foto) de un producto.</summary>
public sealed record DatosFichaCarta(
    IReadOnlyList<string>? Alergenos = null,
    bool Recomendado = false,
    bool Picante = false,
    bool Agotado = false,
    string? FotoBase64 = null,
    string? FotoTipo = null,
    bool QuitarFoto = false);

/// <summary>Cambio rápido de disponibilidad de un plato (agotar / reactivar).</summary>
public sealed record DatosDisponibilidad(bool Agotado);

/// <summary>Caso de uso (personal): lista las fichas de carta (alérgenos y foto) de la empresa.</summary>
public sealed class ListarFichasCarta
{
    private readonly IConsultaFichasCarta _consulta;

    public ListarFichasCarta(IConsultaFichasCarta consulta) => _consulta = consulta;

    public Task<IReadOnlyList<FichaCartaDto>> EjecutarAsync(Guid empresaId, CancellationToken ct = default) =>
        _consulta.ListarAsync(empresaId, ct);
}

/// <summary>
/// Caso de uso (personal): guarda la ficha de carta de un producto (alérgenos y/o foto). La foto llega
/// como data URL o base64 desde el navegador (que ya la reduce de tamaño).
/// </summary>
public sealed class GuardarFichaCarta
{
    private readonly IRepositorioFichasCarta _fichas;
    private readonly IConsultaProductos _productos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarFichaCarta(IRepositorioFichasCarta fichas, IConsultaProductos productos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _fichas = fichas;
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid productoId, DatosFichaCarta datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var producto = await _productos.ObtenerAsync(productoId, ct).ConfigureAwait(false);
        if (producto is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("producto.no_encontrado", "El producto no existe."));
        }

        var ficha = await _fichas.ObtenerPorProductoAsync(productoId, ct).ConfigureAwait(false);
        var nueva = ficha is null;
        ficha ??= FichaCarta.Crear(empresaId, productoId, _reloj);

        ficha.FijarAlergenos(Alergenos.DeNombres(datos.Alergenos), _reloj);
        ficha.FijarDestacados(datos.Recomendado, datos.Picante, _reloj);
        ficha.FijarDisponibilidad(datos.Agotado, _reloj);

        if (datos.QuitarFoto)
        {
            ficha.QuitarFoto(_reloj);
        }
        else if (!string.IsNullOrWhiteSpace(datos.FotoBase64))
        {
            var (bytes, tipo) = DecodificarFoto(datos.FotoBase64!, datos.FotoTipo);
            if (bytes is null)
            {
                return Resultado.Fallo(Error.Validacion("ficha.foto_invalida", "No se pudo leer la imagen."));
            }

            var r = ficha.FijarFoto(bytes, tipo, _reloj);
            if (r.EsFallo)
            {
                return r;
            }
        }

        if (nueva)
        {
            _fichas.Agregar(ficha);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }

    private static (byte[]? Bytes, string Tipo) DecodificarFoto(string valor, string? tipoIndicado)
    {
        var tipo = tipoIndicado;
        var payload = valor.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var coma = payload.IndexOf(',');
            if (coma < 0)
            {
                return (null, string.Empty);
            }

            var cabecera = payload[5..coma]; // p. ej. image/jpeg;base64
            var puntoComa = cabecera.IndexOf(';');
            tipo = puntoComa >= 0 ? cabecera[..puntoComa] : cabecera;
            payload = payload[(coma + 1)..];
        }

        try
        {
            return (Convert.FromBase64String(payload), (tipo ?? string.Empty).Trim().ToLowerInvariant());
        }
        catch (FormatException)
        {
            return (null, string.Empty);
        }
    }
}

/// <summary>
/// Caso de uso (personal): cambio rápido de disponibilidad de un plato (agotar / reactivar), sin tocar
/// el resto de la ficha. Pensado para usarlo sobre la marcha durante el servicio.
/// </summary>
public sealed class CambiarDisponibilidad
{
    private readonly IRepositorioFichasCarta _fichas;
    private readonly IConsultaProductos _productos;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CambiarDisponibilidad(IRepositorioFichasCarta fichas, IConsultaProductos productos, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _fichas = fichas;
        _productos = productos;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid productoId, DatosDisponibilidad datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var producto = await _productos.ObtenerAsync(productoId, ct).ConfigureAwait(false);
        if (producto is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("producto.no_encontrado", "El producto no existe."));
        }

        var ficha = await _fichas.ObtenerPorProductoAsync(productoId, ct).ConfigureAwait(false);
        var nueva = ficha is null;
        ficha ??= FichaCarta.Crear(empresaId, productoId, _reloj);
        ficha.FijarDisponibilidad(datos.Agotado, _reloj);

        if (nueva)
        {
            _fichas.Agregar(ficha);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

/// <summary>Datos para fijar el tema visual de la carta.</summary>
public sealed record DatosConfiguracionCarta(string Tema);

/// <summary>Caso de uso: obtiene la configuración de carta de la empresa (tema), con valores por defecto.</summary>
public sealed class ObtenerConfiguracionCarta
{
    private readonly IRepositorioConfiguracionCarta _repositorio;

    public ObtenerConfiguracionCarta(IRepositorioConfiguracionCarta repositorio) => _repositorio = repositorio;

    public async Task<ConfiguracionCartaDto> EjecutarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var cfg = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        return new ConfiguracionCartaDto(cfg?.Tema ?? TemasCarta.PorDefecto);
    }
}

/// <summary>Caso de uso (personal): fija el tema visual de la carta del local.</summary>
public sealed class GuardarConfiguracionCarta
{
    private readonly IRepositorioConfiguracionCarta _repositorio;
    private readonly IUnidadDeTrabajoHosteleria _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public GuardarConfiguracionCarta(IRepositorioConfiguracionCarta repositorio, IUnidadDeTrabajoHosteleria unidadDeTrabajo, IReloj reloj)
    {
        _repositorio = repositorio;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<ConfiguracionCartaDto>> EjecutarAsync(Guid empresaId, DatosConfiguracionCarta datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var cfg = await _repositorio.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (cfg is null)
        {
            cfg = ConfiguracionCarta.Crear(empresaId, datos.Tema, _reloj);
            _repositorio.Agregar(cfg);
        }
        else
        {
            cfg.FijarTema(datos.Tema, _reloj);
        }

        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(ConfiguracionCartaDto.Desde(cfg));
    }
}

/// <summary>Caso de uso <b>anónimo</b>: sirve la foto de un producto para la carta pública.</summary>
public sealed class ObtenerFotoProducto
{
    private readonly IConsultaFichasCarta _fichas;

    public ObtenerFotoProducto(IConsultaFichasCarta fichas) => _fichas = fichas;

    public Task<FotoProducto?> EjecutarAsync(Guid empresaId, Guid productoId, CancellationToken ct = default) =>
        _fichas.ObtenerFotoAsync(empresaId, productoId, ct);
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

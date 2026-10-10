using System.Security.Claims;
using AlxorCore.Api.Comun;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Tesoreria.Aplicacion;
using QRCoder;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Hostelería (mesas y comandas del TPV de barra/salón).</summary>
public static class EndpointsHosteleria
{
    public static IEndpointRouteBuilder MapearHosteleria(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var mesas = rutas.MapGroup("/mesas").WithTags("Mesas");

        mesas.MapGet("", ListarMesasAsync)
            .WithSummary("Lista las mesas de la empresa activa con su ocupación.")
            .RequireAuthorization();

        mesas.MapPost("", CrearMesaAsync)
            .WithSummary("Crea una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapPut("/{id:guid}", ActualizarMesaAsync)
            .WithSummary("Actualiza una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapPut("/{id:guid}/posicion", MoverMesaAsync)
            .WithSummary("Recoloca una mesa en el plano del local.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapDelete("/{id:guid}", DesactivarMesaAsync)
            .WithSummary("Retira (desactiva) una mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        mesas.MapGet("/{id:guid}/qr-carta.svg", QrCartaMesaAsync)
            .WithSummary("Código QR (SVG) de autopedido de una mesa: enlaza a la carta de esa mesa para pedir.")
            .RequireAuthorization()
            .RequierePlanPro();

        mesas.MapGet("/{id:guid}/carta-link", CartaLinkMesaAsync)
            .WithSummary("Enlace (y token) de autopedido de una mesa, para imprimir o compartir el QR.")
            .RequireAuthorization()
            .RequierePlanPro();

        mesas.MapPost("/{id:guid}/regenerar-token", RegenerarTokenAsync)
            .WithSummary("Genera un token de carta nuevo para la mesa (invalida sus QR de autopedido ya impresos).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        // Autopedido: pedidos del cliente (pendientes de aceptar) y avisos de mesa.
        var pedidosWeb = rutas.MapGroup("/pedidos-web").WithTags("Autopedido");

        pedidosWeb.MapGet("", ListarPedidosWebAsync)
            .WithSummary("Lista los pedidos hechos por los clientes desde el móvil, pendientes de aceptar.")
            .RequireAuthorization()
            .RequierePlanPro();

        pedidosWeb.MapPost("/{id:guid}/aceptar", AceptarPedidoWebAsync)
            .WithSummary("Acepta un pedido del cliente: lo añade a la cuenta de la mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        pedidosWeb.MapPost("/{id:guid}/rechazar", RechazarPedidoWebAsync)
            .WithSummary("Rechaza un pedido del cliente.")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        var avisos = rutas.MapGroup("/avisos").WithTags("Autopedido");

        avisos.MapGet("", ListarAvisosAsync)
            .WithSummary("Lista los avisos de mesa pendientes (llamar al camarero / pedir la cuenta).")
            .RequireAuthorization()
            .RequierePlanPro();

        avisos.MapPost("/{id:guid}/atender", AtenderAvisoAsync)
            .WithSummary("Marca un aviso de mesa como atendido.")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        var traducciones = rutas.MapGroup("/carta/traducciones").WithTags("Autopedido");

        traducciones.MapGet("", ListarTraduccionesAsync)
            .WithSummary("Lista las traducciones de la carta (inglés/francés) del local.")
            .RequireAuthorization()
            .RequierePlanPro();

        traducciones.MapPut("", GuardarTraduccionAsync)
            .WithSummary("Guarda o borra una traducción de la carta (nombre vacío = volver al español).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        var fichas = rutas.MapGroup("/carta/fichas").WithTags("Autopedido");

        fichas.MapGet("", ListarFichasCartaAsync)
            .WithSummary("Lista las fichas de carta (alérgenos y si tienen foto) de los productos.")
            .RequireAuthorization()
            .RequierePlanPro();

        fichas.MapPut("/{productoId:guid}", GuardarFichaCartaAsync)
            .WithSummary("Guarda la ficha de carta de un producto (alérgenos y/o foto).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        fichas.MapPut("/{productoId:guid}/disponibilidad", CambiarDisponibilidadAsync)
            .WithSummary("Marca un plato como agotado o lo reactiva (cambio rápido durante el servicio).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        var configuracionCarta = rutas.MapGroup("/carta/configuracion").WithTags("Autopedido");

        configuracionCarta.MapGet("", ObtenerConfiguracionCartaAsync)
            .WithSummary("Obtiene la configuración de la carta (tema visual).")
            .RequireAuthorization()
            .RequierePlanPro();

        configuracionCarta.MapPut("", GuardarConfiguracionCartaAsync)
            .WithSummary("Fija el tema visual de la carta del local.")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        var menuDia = rutas.MapGroup("/carta/menu-dia").WithTags("Autopedido");

        menuDia.MapGet("", ObtenerMenuDiaAsync)
            .WithSummary("Obtiene el menú del día del local (precio, qué incluye y platos por secciones).")
            .RequireAuthorization()
            .RequierePlanPro();

        menuDia.MapPut("", GuardarMenuDiaAsync)
            .WithSummary("Fija el menú del día del local (se muestra en la carta del cliente si está activo).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        // Caja: movimientos de efectivo (fondo, entradas, salidas) y arqueo del día.
        var caja = rutas.MapGroup("/caja").WithTags("Caja");

        caja.MapGet("/movimientos", ListarMovimientosCajaAsync)
            .WithSummary("Lista los movimientos de efectivo (fondo, entradas y salidas) de un día.")
            .RequireAuthorization();

        caja.MapPost("/movimientos", RegistrarMovimientoCajaAsync)
            .WithSummary("Registra un movimiento de efectivo: fondo inicial, entrada o salida.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        caja.MapDelete("/movimientos/{id:guid}", QuitarMovimientoCajaAsync)
            .WithSummary("Elimina un movimiento de caja (corrige un error de registro).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        caja.MapGet("/arqueo", ArqueoCajaAsync)
            .WithSummary("Arqueo del día: efectivo teórico (fondo + cobros en efectivo + entradas − salidas) para cuadrar la caja.")
            .RequireAuthorization();

        // Opciones de producto (formatos/tamaños y extras con suplemento) para el TPV.
        var opciones = rutas.MapGroup("/carta/opciones").WithTags("Opciones");

        opciones.MapGet("", ListarOpcionesEmpresaAsync)
            .WithSummary("Todas las opciones del local agrupadas por producto (para el TPV).")
            .RequireAuthorization();

        opciones.MapGet("/{productoId:guid}", ObtenerOpcionesProductoAsync)
            .WithSummary("Grupos de opciones (formatos y extras) de un producto.")
            .RequireAuthorization();

        opciones.MapPut("/{productoId:guid}", GuardarOpcionesProductoAsync)
            .WithSummary("Fija los grupos de opciones de un producto (reemplaza los existentes).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        // Zonas de preparación (Cocina/Barra) por producto.
        var zonas = rutas.MapGroup("/carta/zonas").WithTags("Cocina");

        zonas.MapGet("", ListarZonasAsync)
            .WithSummary("Zona de preparación (Cocina/Barra) de cada producto del local.")
            .RequireAuthorization();

        zonas.MapPut("/{productoId:guid}", GuardarZonaProductoAsync)
            .WithSummary("Fija la zona de preparación de un producto.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        // Pantalla de cocina (KDS): artículos pendientes de servir.
        var cocina = rutas.MapGroup("/cocina").WithTags("Cocina");

        cocina.MapGet("/pendientes", PendientesCocinaAsync)
            .WithSummary("Artículos enviados y pendientes de servir, para la pantalla de cocina (opcional por zona).")
            .RequireAuthorization();

        cocina.MapPost("/servir", ServirCocinaAsync)
            .WithSummary("Marca un artículo como servido desde la pantalla de cocina.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var comandas = rutas.MapGroup("/comandas").WithTags("Comandas");

        comandas.MapGet("", ListarComandasAsync)
            .WithSummary("Lista las comandas abiertas de la empresa activa.")
            .RequireAuthorization();

        comandas.MapGet("/ventas-por-camarero", VentasPorCamareroAsync)
            .WithSummary("Ventas (comandas cobradas) de un día agrupadas por camarero, para el arqueo por persona.")
            .RequireAuthorization();

        comandas.MapGet("/{id:guid}", ObtenerComandaAsync)
            .WithSummary("Obtiene una comanda con sus líneas.")
            .RequireAuthorization();

        comandas.MapPost("", AbrirComandaAsync)
            .WithSummary("Abre una comanda en una mesa libre.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/lineas", AgregarLineaAsync)
            .WithSummary("Añade un producto a la comanda.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPut("/{id:guid}/lineas/{lineaId:guid}", FijarCantidadLineaAsync)
            .WithSummary("Fija la cantidad de una línea (botones +/− del TPV).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPut("/{id:guid}/lineas/{lineaId:guid}/precio", CambiarPrecioLineaAsync)
            .WithSummary("Cambia el precio de una línea (hacer precio a mano o invitar con 0).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPut("/{id:guid}/lineas/{lineaId:guid}/nota", CambiarNotaLineaAsync)
            .WithSummary("Fija la nota de preparación de una línea (para cocina).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapDelete("/{id:guid}/lineas/{lineaId:guid}", QuitarLineaAsync)
            .WithSummary("Quita una línea de la comanda.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/cocina", EnviarCocinaAsync)
            .WithSummary("Envía a cocina/barra los artículos nuevos de la comanda (marca e imprime).")
            .RequierePermiso(Permisos.HosteleriaGestionar)
            .RequierePlanPro();

        // Plan contratado del local (Essential vs Pro). No se capa por plan: cualquiera puede verlo/cambiarlo.
        var suscripcion = rutas.MapGroup("/suscripcion").WithTags("Plan");

        suscripcion.MapGet("", ObtenerSuscripcionAsync)
            .WithSummary("Plan (tarifa) contratado por el local y, por tanto, qué funciones tiene disponibles.")
            .RequireAuthorization();

        suscripcion.MapPut("", CambiarSuscripcionAsync)
            .WithSummary("Cambia el plan contratado del local (Essential / Pro).")
            .RequierePermiso(Permisos.EmpresaAjustes);

        comandas.MapPost("/{id:guid}/cobrar", CobrarComandaAsync)
            .WithSummary("Cobra la comanda emitiendo un ticket y libera la mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/cobrar-parcial", CobrarComandaParcialAsync)
            .WithSummary("Cobra parte de la comanda (reparto por artículos): emite un ticket de los artículos indicados y cierra la mesa cuando queda todo pagado.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapGet("/{id:guid}/cuenta.escpos", DescargarCuentaAsync)
            .WithSummary("Descarga la cuenta previa (pre-ticket, sin valor fiscal) de la comanda en ESC/POS.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/cuenta/imprimir", ImprimirCuentaAsync)
            .WithSummary("Imprime la cuenta previa (pre-ticket) de la comanda en la impresora térmica.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/mover", MoverComandaAsync)
            .WithSummary("Mueve la comanda a otra mesa libre (los clientes se cambian de sitio).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/juntar", JuntarComandasAsync)
            .WithSummary("Junta otra comanda en esta (funde las dos cuentas y libera la mesa de origen).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        comandas.MapPost("/{id:guid}/anular", AnularComandaAsync)
            .WithSummary("Anula la comanda sin cobrarla.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        return rutas;
    }

    private static async Task<IResult> ListarMesasAsync(IContextoEmpresa contexto, ListarMesas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CrearMesaAsync(DatosMesa datos, IContextoEmpresa contexto, CrearMesa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/mesas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> ActualizarMesaAsync(Guid id, DatosMesa datos, ActualizarMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> MoverMesaAsync(Guid id, DatosPosicion datos, MoverMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> DesactivarMesaAsync(Guid id, DesactivarMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> QrCartaMesaAsync(Guid id, IContextoEmpresa contexto, IRepositorioMesas mesas, HttpContext http, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var mesa = await mesas.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (mesa is null || mesa.EmpresaId != contexto.EmpresaId.Value)
        {
            return Results.NotFound();
        }

        var url = $"{http.Request.Scheme}://{http.Request.Host}/carta.html?e={contexto.EmpresaId.Value}&m={id}&t={mesa.TokenCarta}";
        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(datos).GetGraphic(6);
        return Results.Content(svg, "image/svg+xml");
    }

    private sealed record CartaLinkDto(string Url, Guid Token);

    private static async Task<IResult> CartaLinkMesaAsync(Guid id, IContextoEmpresa contexto, IRepositorioMesas mesas, HttpContext http, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var mesa = await mesas.ObtenerPorIdAsync(id, ct).ConfigureAwait(false);
        if (mesa is null || mesa.EmpresaId != contexto.EmpresaId.Value)
        {
            return Results.NotFound();
        }

        var url = $"{http.Request.Scheme}://{http.Request.Host}/carta.html?e={contexto.EmpresaId.Value}&m={id}&t={mesa.TokenCarta}";
        return Results.Ok(new CartaLinkDto(url, mesa.TokenCarta));
    }

    private static async Task<IResult> RegenerarTokenAsync(Guid id, RegenerarTokenCartaMesa caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ObtenerSuscripcionAsync(IContextoEmpresa contexto, ObtenerSuscripcion caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CambiarSuscripcionAsync(DatosSuscripcion datos, IContextoEmpresa contexto, CambiarPlanBar caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> ListarPedidosWebAsync(IContextoEmpresa contexto, ListarPedidosWebPendientes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> AceptarPedidoWebAsync(Guid id, IContextoEmpresa contexto, AceptarPedidoWeb caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, id, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> RechazarPedidoWebAsync(Guid id, RechazarPedidoWeb caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarAvisosAsync(IContextoEmpresa contexto, ListarAvisosPendientes caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> AtenderAvisoAsync(Guid id, AtenderAviso caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarTraduccionesAsync(IContextoEmpresa contexto, ListarTraducciones caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GuardarTraduccionAsync(DatosTraduccion datos, IContextoEmpresa contexto, GuardarTraduccion caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> ListarFichasCartaAsync(IContextoEmpresa contexto, ListarFichasCarta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GuardarFichaCartaAsync(Guid productoId, DatosFichaCarta datos, IContextoEmpresa contexto, GuardarFichaCarta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, productoId, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> CambiarDisponibilidadAsync(Guid productoId, DatosDisponibilidad datos, IContextoEmpresa contexto, CambiarDisponibilidad caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, productoId, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> ObtenerConfiguracionCartaAsync(IContextoEmpresa contexto, ObtenerConfiguracionCarta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GuardarConfiguracionCartaAsync(DatosConfiguracionCarta datos, IContextoEmpresa contexto, GuardarConfiguracionCarta caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> ObtenerMenuDiaAsync(IContextoEmpresa contexto, ObtenerMenuDia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GuardarMenuDiaAsync(DatosMenuDia datos, IContextoEmpresa contexto, GuardarMenuDia caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> ListarComandasAsync(IContextoEmpresa contexto, ListarComandasAbiertas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerComandaAsync(Guid id, ObtenerComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> AbrirComandaAsync(DatosAbrirComanda datos, IContextoEmpresa contexto, ClaimsPrincipal usuario, AbrirComanda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var (usuarioId, usuarioNombre) = CamareroActual(usuario);
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, usuarioId, usuarioNombre, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/comandas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
    }

    /// <summary>Extrae el usuario (camarero) y su nombre del token, para atribuir la comanda.</summary>
    private static (Guid? Id, string? Nombre) CamareroActual(ClaimsPrincipal usuario)
    {
        var sub = usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario.FindFirstValue("sub");
        var id = Guid.TryParse(sub, out var g) ? g : (Guid?)null;
        var nombre = usuario.FindFirstValue("nombre");
        return (id, string.IsNullOrWhiteSpace(nombre) ? null : nombre);
    }

    private static async Task<IResult> VentasPorCamareroAsync(IContextoEmpresa contexto, VentasPorCamarero caso, CancellationToken ct, DateOnly? dia = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, dia, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ListarOpcionesEmpresaAsync(IContextoEmpresa contexto, ListarOpcionesEmpresa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ObtenerOpcionesProductoAsync(Guid productoId, ObtenerOpcionesProducto caso, CancellationToken ct) =>
        Results.Ok(await caso.EjecutarAsync(productoId, ct).ConfigureAwait(false));

    private static async Task<IResult> GuardarOpcionesProductoAsync(Guid productoId, DatosOpcionesProducto datos, IContextoEmpresa contexto, GuardarOpcionesProducto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, productoId, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> ListarZonasAsync(IContextoEmpresa contexto, ObtenerZonasEmpresa caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> GuardarZonaProductoAsync(Guid productoId, DatosZonaProducto datos, IContextoEmpresa contexto, GuardarZonaProducto caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, productoId, datos, ct).ConfigureAwait(false)).ASinContenido();
    }

    private sealed record ServirCocinaPeticion(Guid ComandaId, Guid LineaId);

    private static async Task<IResult> PendientesCocinaAsync(IContextoEmpresa contexto, ListarPendientesCocina caso, CancellationToken ct, string? zona = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, zona, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> ServirCocinaAsync(ServirCocinaPeticion datos, ServirItemCocina caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(datos.ComandaId, datos.LineaId, ct).ConfigureAwait(false)).ASinContenido();

    private static async Task<IResult> ListarMovimientosCajaAsync(IContextoEmpresa contexto, ListarMovimientosCaja caso, CancellationToken ct, DateOnly? dia = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, dia, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> RegistrarMovimientoCajaAsync(DatosMovimientoCaja datos, IContextoEmpresa contexto, ClaimsPrincipal usuario, RegistrarMovimientoCaja caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var (usuarioId, usuarioNombre) = CamareroActual(usuario);
        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, usuarioId, usuarioNombre, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado("/caja/movimientos") : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> QuitarMovimientoCajaAsync(Guid id, IContextoEmpresa contexto, QuitarMovimientoCaja caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(contexto.EmpresaId.Value, id, ct).ConfigureAwait(false)).ASinContenido();
    }

    private static async Task<IResult> ArqueoCajaAsync(
        IContextoEmpresa contexto, ListarMovimientosCaja movimientosCaso, IConsultaTesoreria tesoreria, IReloj reloj, CancellationToken ct, DateOnly? dia = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var d = dia ?? DateOnly.FromDateTime(reloj.AhoraUtc.UtcDateTime);
        var movs = await movimientosCaso.EjecutarAsync(contexto.EmpresaId.Value, d, ct).ConfigureAwait(false);

        var fondo = movs.Where(m => m.Tipo == nameof(Hosteleria.Dominio.TipoMovimientoCaja.FondoInicial)).Sum(m => m.Importe);
        var entradas = movs.Where(m => m.Tipo == nameof(Hosteleria.Dominio.TipoMovimientoCaja.Entrada)).Sum(m => m.Importe);
        var salidas = movs.Where(m => m.Tipo == nameof(Hosteleria.Dominio.TipoMovimientoCaja.Salida)).Sum(m => m.Importe);

        var tes = await tesoreria.ListarPorPeriodoAsync(contexto.EmpresaId.Value, d, d, ct).ConfigureAwait(false);
        var cobrosEfectivo = tes
            .Where(x => x.Sentido == "Cobro" && string.Equals(x.Metodo, "Efectivo", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Importe);

        var teorico = Math.Round(fondo + cobrosEfectivo + entradas - salidas, 2, MidpointRounding.AwayFromZero);
        return Results.Ok(new ArqueoCajaDto(d, fondo, Math.Round(cobrosEfectivo, 2, MidpointRounding.AwayFromZero), entradas, salidas, teorico, movs));
    }

    private static async Task<IResult> AgregarLineaAsync(Guid id, DatosLineaComanda datos, AgregarLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> FijarCantidadLineaAsync(Guid id, Guid lineaId, DatosCantidadLinea datos, FijarCantidadLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, lineaId, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CambiarPrecioLineaAsync(Guid id, Guid lineaId, DatosPrecioLinea datos, CambiarPrecioLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, lineaId, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> CambiarNotaLineaAsync(Guid id, Guid lineaId, DatosNotaLinea datos, CambiarNotaLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, lineaId, datos, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> QuitarLineaAsync(Guid id, Guid lineaId, QuitarLineaComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, lineaId, ct).ConfigureAwait(false)).AOk();

    private static async Task<IResult> EnviarCocinaAsync(
        Guid id, IContextoEmpresa contexto, EnviarComandaCocina caso, IConsultaMesas mesas, IRepositorioZonasProducto zonas,
        IGeneradorComandaCocina generador, IImpresoraTickets impresora, ILoggerFactory registros, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(id, ct).ConfigureAwait(false);

        // Imprimir la comanda de cocina de los artículos nuevos (mejor esfuerzo: no bloquea el pedido).
        if (resultado.EsCorrecto && resultado.Valor.Articulos.Count > 0 && impresora.Configurada)
        {
            try
            {
                var mesa = await mesas.ObtenerAsync(resultado.Valor.MesaId, ct).ConfigureAwait(false);
                var mapaZonas = (await zonas.ListarPorEmpresaAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false))
                    .ToDictionary(z => z.ProductoId, z => z.Zona.ToString());
                var datos = new DatosComandaCocina(
                    string.IsNullOrWhiteSpace(mesa?.Nombre) ? "Mesa" : mesa!.Nombre,
                    resultado.Valor.Hora,
                    resultado.Valor.Articulos.Select(a => new LineaCocina(a.Cantidad, a.Descripcion, a.Nota,
                        mapaZonas.TryGetValue(a.ProductoId, out var z) ? z : null)).ToList(),
                    resultado.Valor.Notas);
                await impresora.ImprimirAsync(generador.Generar(datos), ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Un fallo de impresión no debe interrumpir el envío a cocina.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                registros.CreateLogger("Hosteleria.Cocina").LogWarning(ex, "No se pudo imprimir la comanda de cocina {ComandaId}.", id);
            }
        }

        return resultado.AOk();
    }

    private static async Task<IResult> CobrarComandaAsync(
        Guid id, DatosCobro datos, IContextoEmpresa contexto, ClaimsPrincipal usuario, CobrarComanda caso,
        RegistrarCobro registrarCobro, RegistrarMovimientoCaja movimientoCaja, ILoggerFactory registros, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, id, datos, ct).ConfigureAwait(false);

        // Registrar el cobro del ticket para que la venta figure en el cierre de caja del día. La comanda
        // ya está cobrada (transacción propia); si el registro del cobro fallara, se avisa sin deshacerla.
        if (resultado.EsCorrecto && resultado.Valor.FacturaId is { } facturaId)
        {
            var empresaId = contexto.EmpresaId.Value;
            var registro = registros.CreateLogger("Hosteleria.Cobro");
            var total = resultado.Valor.Total;

            async Task RegistrarAsync(decimal importe, string metodo)
            {
                if (importe <= 0m)
                {
                    return;
                }

                var cobro = await registrarCobro.EjecutarAsync(empresaId, new RegistrarCobroComando(facturaId, importe, Metodo: metodo), ct).ConfigureAwait(false);
                if (cobro.EsFallo)
                {
                    registro.LogWarning("Comanda {ComandaId}: cobro ({Metodo}) no registrado en caja: {Codigo}.", id, metodo, cobro.Error.Codigo);
                }
            }

            // Pago mixto: la parte en efectivo y la parte con tarjeta se registran por separado para que
            // el cierre de caja y el arqueo de efectivo cuadren.
            if (datos.ImporteEfectivo is { } efectivo && efectivo > 0m && efectivo < total)
            {
                await RegistrarAsync(Math.Round(efectivo, 2, MidpointRounding.AwayFromZero), "Efectivo").ConfigureAwait(false);
                await RegistrarAsync(total - Math.Round(efectivo, 2, MidpointRounding.AwayFromZero), "Tarjeta").ConfigureAwait(false);
            }
            else
            {
                await RegistrarAsync(total, datos.Metodo.ToString() == "Mixto" ? "Efectivo" : datos.Metodo.ToString()).ConfigureAwait(false);
            }

            // Propina (en efectivo): entra en la caja, no forma parte de la base imponible del ticket.
            if (datos.Propina > 0m)
            {
                var (usuarioId, usuarioNombre) = CamareroActual(usuario);
                var prop = await movimientoCaja.EjecutarAsync(empresaId, new DatosMovimientoCaja("Entrada", datos.Propina, "Propina"), usuarioId, usuarioNombre, ct).ConfigureAwait(false);
                if (prop.EsFallo)
                {
                    registro.LogWarning("Comanda {ComandaId}: propina no registrada en caja: {Codigo}.", id, prop.Error.Codigo);
                }
            }
        }

        return resultado.AOk();
    }

    private static async Task<IResult> CobrarComandaParcialAsync(
        Guid id, DatosCobroParcial datos, IContextoEmpresa contexto, CobrarComandaParcial caso,
        RegistrarCobro registrarCobro, ILoggerFactory registros, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, id, datos, ct).ConfigureAwait(false);

        // Registrar el cobro del ticket parcial en caja (para el cierre del día). El ticket ya está
        // emitido en su propia transacción; si el registro fallara, se avisa sin deshacer el cobro.
        if (resultado.EsCorrecto)
        {
            var cobro = await registrarCobro.EjecutarAsync(
                contexto.EmpresaId.Value,
                new RegistrarCobroComando(resultado.Valor.FacturaId, resultado.Valor.Total, Metodo: datos.Metodo.ToString()),
                ct).ConfigureAwait(false);
            if (cobro.EsFallo)
            {
                registros.CreateLogger("Hosteleria.Cobro").LogWarning(
                    "Cobro parcial de la comanda {ComandaId} emitido, pero no se registró en caja: {Codigo}.", id, cobro.Error.Codigo);
            }
        }

        return resultado.AOk();
    }

    private static async Task<Resultado<DatosCuenta>> ConstruirCuentaAsync(
        Guid empresaId, Guid id, IConsultaComandas comandas, IConsultaMesas mesas, IConsultaEmpresas empresas, IReloj reloj, CancellationToken ct)
    {
        var comanda = await comandas.ObtenerAsync(id, ct).ConfigureAwait(false);
        if (comanda is null)
        {
            return Resultado.Fallo<DatosCuenta>(Error.NoEncontrado("comanda.no_encontrada", "La comanda no existe."));
        }

        var empresa = await empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<DatosCuenta>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        var mesa = await mesas.ObtenerAsync(comanda.MesaId, ct).ConfigureAwait(false);
        var lineas = comanda.Lineas
            .Select(l => new LineaCuenta(l.Cantidad, l.Descripcion, l.PrecioUnitario, l.Total))
            .ToList();

        return Resultado.Ok(new DatosCuenta(
            empresa.RazonSocial,
            string.IsNullOrWhiteSpace(mesa?.Nombre) ? "Mesa" : mesa!.Nombre,
            reloj.AhoraUtc,
            lineas,
            comanda.BaseImponible,
            comanda.CuotaIva,
            comanda.Total,
            comanda.Notas));
    }

    private static async Task<IResult> DescargarCuentaAsync(
        Guid id, IContextoEmpresa contexto, IConsultaComandas comandas, IConsultaMesas mesas,
        IConsultaEmpresas empresas, IGeneradorCuenta generador, IReloj reloj, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var datos = await ConstruirCuentaAsync(contexto.EmpresaId.Value, id, comandas, mesas, empresas, reloj, ct).ConfigureAwait(false);
        if (datos.EsFallo)
        {
            return ResultadosHttp.AProblema(datos.Error);
        }

        return Results.File(generador.Generar(datos.Valor), "application/octet-stream", $"cuenta-{datos.Valor.Mesa}.escpos");
    }

    private static async Task<IResult> ImprimirCuentaAsync(
        Guid id, IContextoEmpresa contexto, IConsultaComandas comandas, IConsultaMesas mesas,
        IConsultaEmpresas empresas, IGeneradorCuenta generador, IImpresoraTickets impresora, IReloj reloj, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        if (!impresora.Configurada)
        {
            return ResultadosHttp.AProblema(Error.Validacion("impresora.no_configurada",
                "No hay ninguna impresora de tickets configurada. Configura la sección «Impresora» o descarga la cuenta."));
        }

        var datos = await ConstruirCuentaAsync(contexto.EmpresaId.Value, id, comandas, mesas, empresas, reloj, ct).ConfigureAwait(false);
        if (datos.EsFallo)
        {
            return ResultadosHttp.AProblema(datos.Error);
        }

        try
        {
            await impresora.ImprimirAsync(generador.Generar(datos.Valor), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Cualquier fallo de E/S con la impresora se traduce a un error de negocio legible.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ResultadosHttp.AProblema(Error.Conflicto("impresora.error", $"No se pudo imprimir la cuenta: {ex.Message}"));
        }

        return Results.NoContent();
    }

    private static async Task<IResult> MoverComandaAsync(Guid id, DatosMoverComanda datos, IContextoEmpresa contexto, MoverComanda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> JuntarComandasAsync(Guid id, DatosJuntarComandas datos, IContextoEmpresa contexto, JuntarComandas caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return (await caso.EjecutarAsync(id, datos, ct).ConfigureAwait(false)).AOk();
    }

    private static async Task<IResult> AnularComandaAsync(Guid id, AnularComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();
}

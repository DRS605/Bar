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
            .RequireAuthorization();

        mesas.MapGet("/{id:guid}/carta-link", CartaLinkMesaAsync)
            .WithSummary("Enlace (y token) de autopedido de una mesa, para imprimir o compartir el QR.")
            .RequireAuthorization();

        mesas.MapPost("/{id:guid}/regenerar-token", RegenerarTokenAsync)
            .WithSummary("Genera un token de carta nuevo para la mesa (invalida sus QR de autopedido ya impresos).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        // Autopedido: pedidos del cliente (pendientes de aceptar) y avisos de mesa.
        var pedidosWeb = rutas.MapGroup("/pedidos-web").WithTags("Autopedido");

        pedidosWeb.MapGet("", ListarPedidosWebAsync)
            .WithSummary("Lista los pedidos hechos por los clientes desde el móvil, pendientes de aceptar.")
            .RequireAuthorization();

        pedidosWeb.MapPost("/{id:guid}/aceptar", AceptarPedidoWebAsync)
            .WithSummary("Acepta un pedido del cliente: lo añade a la cuenta de la mesa.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        pedidosWeb.MapPost("/{id:guid}/rechazar", RechazarPedidoWebAsync)
            .WithSummary("Rechaza un pedido del cliente.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var avisos = rutas.MapGroup("/avisos").WithTags("Autopedido");

        avisos.MapGet("", ListarAvisosAsync)
            .WithSummary("Lista los avisos de mesa pendientes (llamar al camarero / pedir la cuenta).")
            .RequireAuthorization();

        avisos.MapPost("/{id:guid}/atender", AtenderAvisoAsync)
            .WithSummary("Marca un aviso de mesa como atendido.")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var traducciones = rutas.MapGroup("/carta/traducciones").WithTags("Autopedido");

        traducciones.MapGet("", ListarTraduccionesAsync)
            .WithSummary("Lista las traducciones de la carta (inglés/francés) del local.")
            .RequireAuthorization();

        traducciones.MapPut("", GuardarTraduccionAsync)
            .WithSummary("Guarda o borra una traducción de la carta (nombre vacío = volver al español).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var fichas = rutas.MapGroup("/carta/fichas").WithTags("Autopedido");

        fichas.MapGet("", ListarFichasCartaAsync)
            .WithSummary("Lista las fichas de carta (alérgenos y si tienen foto) de los productos.")
            .RequireAuthorization();

        fichas.MapPut("/{productoId:guid}", GuardarFichaCartaAsync)
            .WithSummary("Guarda la ficha de carta de un producto (alérgenos y/o foto).")
            .RequierePermiso(Permisos.HosteleriaGestionar);

        var comandas = rutas.MapGroup("/comandas").WithTags("Comandas");

        comandas.MapGet("", ListarComandasAsync)
            .WithSummary("Lista las comandas abiertas de la empresa activa.")
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
            .RequierePermiso(Permisos.HosteleriaGestionar);

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

    private static async Task<IResult> AbrirComandaAsync(DatosAbrirComanda datos, IContextoEmpresa contexto, AbrirComanda caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, datos, ct).ConfigureAwait(false);
        return resultado.EsCorrecto ? resultado.ACreado($"/comandas/{resultado.Valor.Id}") : ResultadosHttp.AProblema(resultado.Error);
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
        Guid id, IContextoEmpresa contexto, EnviarComandaCocina caso, IConsultaMesas mesas,
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
                var datos = new DatosComandaCocina(
                    string.IsNullOrWhiteSpace(mesa?.Nombre) ? "Mesa" : mesa!.Nombre,
                    resultado.Valor.Hora,
                    resultado.Valor.Articulos.Select(a => new LineaCocina(a.Cantidad, a.Descripcion, a.Nota)).ToList(),
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
        Guid id, DatosCobro datos, IContextoEmpresa contexto, CobrarComanda caso,
        RegistrarCobro registrarCobro, ILoggerFactory registros, CancellationToken ct)
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
            var cobro = await registrarCobro.EjecutarAsync(
                contexto.EmpresaId.Value,
                new RegistrarCobroComando(facturaId, resultado.Valor.Total, Metodo: datos.Metodo.ToString()),
                ct).ConfigureAwait(false);
            if (cobro.EsFallo)
            {
                registros.CreateLogger("Hosteleria.Cobro").LogWarning(
                    "Comanda {ComandaId} cobrada, pero el cobro no se registró en caja: {Codigo}.", id, cobro.Error.Codigo);
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

using AlxorCore.Api.Comun;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Tesoreria.Aplicacion;

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
                    resultado.Valor.Articulos.Select(a => new LineaCocina(a.Cantidad, a.Descripcion)).ToList(),
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

    private static async Task<IResult> AnularComandaAsync(Guid id, AnularComanda caso, CancellationToken ct) =>
        (await caso.EjecutarAsync(id, ct).ConfigureAwait(false)).ASinContenido();
}

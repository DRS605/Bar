using System.Text;
using AlxorCore.Api.Comun;
using AlxorCore.Informes.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints REST del módulo Informes (panel, libros de IVA y exportación).</summary>
public static class EndpointsInformes
{
    public static IEndpointRouteBuilder MapearInformes(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var informes = rutas.MapGroup("/informes").WithTags("Informes");

        informes.MapGet("/dashboard", DashboardAsync)
            .WithSummary("Resumen del panel principal (totales del mes y pendientes).")
            .RequierePermiso(Permisos.InformeLeer);

        informes.MapGet("/libro-iva", LibroIvaAsync)
            .WithSummary("Libro de IVA (repercutido o soportado) de un periodo.")
            .RequierePermiso(Permisos.InformeLeer);

        informes.MapGet("/libro-iva/csv", LibroIvaCsvAsync)
            .WithSummary("Exporta el libro de IVA a CSV para la gestoría.")
            .RequierePermiso(Permisos.DatosExportar);

        informes.MapGet("/resumen-trimestral", ResumenTrimestralAsync)
            .WithSummary("Resúmenes fiscales del trimestre: modelo 303 (IVA) y modelo 130 (IRPF).")
            .RequierePermiso(Permisos.InformeLeer);

        informes.MapGet("/beneficio", BeneficioAsync)
            .WithSummary("Beneficio del periodo: margen bruto (venta − compra) y neto (menos gastos).")
            .RequierePermiso(Permisos.InformeLeer);

        informes.MapGet("/cierre-caja", CierreCajaAsync)
            .WithSummary("Cierre de caja (arqueo) de un día: cobrado por método, pagado y neto.")
            .RequierePermiso(Permisos.InformeLeer);

        return rutas;
    }

    private static async Task<IResult> DashboardAsync(IContextoEmpresa contexto, ObtenerDashboard caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> LibroIvaAsync(
        IContextoEmpresa contexto, GenerarLibroIva caso, CancellationToken ct,
        TipoLibroIva tipo = TipoLibroIva.Repercutido, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var (d, h) = RangoPorDefecto(desde, hasta);
        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, tipo, d, h, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> LibroIvaCsvAsync(
        IContextoEmpresa contexto, GenerarLibroIva caso, CancellationToken ct,
        TipoLibroIva tipo = TipoLibroIva.Repercutido, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var (d, h) = RangoPorDefecto(desde, hasta);
        var libro = await caso.EjecutarAsync(contexto.EmpresaId.Value, tipo, d, h, ct).ConfigureAwait(false);
        var csv = ExportadorLibroIvaCsv.Generar(libro);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return Results.File(bytes, "text/csv", $"libro-iva-{tipo}-{d:yyyyMMdd}-{h:yyyyMMdd}.csv");
    }

    private static async Task<IResult> ResumenTrimestralAsync(
        IContextoEmpresa contexto, GenerarResumenesFiscales caso, CancellationToken ct,
        int? anio = null, int trimestre = 1)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        if (trimestre is < 1 or > 4)
        {
            return ResultadosHttp.AProblema(Error.Validacion("trimestre.invalido", "El trimestre debe estar entre 1 y 4."));
        }

        var ejercicio = anio ?? DateTime.UtcNow.Year;
        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, ejercicio, trimestre, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> BeneficioAsync(
        IContextoEmpresa contexto, GenerarBeneficio caso, CancellationToken ct,
        DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var (d, h) = RangoPorDefecto(desde, hasta);
        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, d, h, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CierreCajaAsync(
        IContextoEmpresa contexto, GenerarCierreCaja caso, CancellationToken ct, DateOnly? dia = null)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var d = dia ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Results.Ok(await caso.EjecutarAsync(contexto.EmpresaId.Value, d, ct).ConfigureAwait(false));
    }

    private static (DateOnly Desde, DateOnly Hasta) RangoPorDefecto(DateOnly? desde, DateOnly? hasta)
    {
        // Por defecto, el año en curso del rango indicado (o de 'hasta').
        var h = hasta ?? (desde is { } dd ? new DateOnly(dd.Year, 12, 31) : new DateOnly(DateTime.UtcNow.Year, 12, 31));
        var d = desde ?? new DateOnly(h.Year, 1, 1);
        return (d, h);
    }
}

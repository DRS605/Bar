using AlxorCore.Api.Comun;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;

namespace AlxorCore.Api.Endpoints;

/// <summary>Petición para enviar una factura por correo.</summary>
public sealed record EnviarFacturaPeticion(string Email);

/// <summary>Endpoints REST del módulo Documentos (PDF y correo de facturas).</summary>
public static class EndpointsDocumentos
{
    public static IEndpointRouteBuilder MapearDocumentos(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapGet("/facturas/{id:guid}/pdf", PdfAsync)
            .WithTags("Documentos").WithSummary("Descarga el PDF de una factura.")
            .RequierePermiso(Permisos.FacturaLeer);

        rutas.MapPost("/facturas/{id:guid}/enviar", EnviarAsync)
            .WithTags("Documentos").WithSummary("Envía la factura por correo con el PDF adjunto.")
            .RequierePermiso(Permisos.FacturaLeer);

        return rutas;
    }

    private static async Task<IResult> PdfAsync(Guid id, IContextoEmpresa contexto, GenerarPdfFactura caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, id, ct).ConfigureAwait(false);
        return resultado.EsCorrecto
            ? Results.File(resultado.Valor.Contenido, "application/pdf", resultado.Valor.NombreArchivo)
            : ResultadosHttp.AProblema(resultado.Error);
    }

    private static async Task<IResult> EnviarAsync(Guid id, EnviarFacturaPeticion peticion, IContextoEmpresa contexto, EnviarFacturaPorEmail caso, CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var resultado = await caso.EjecutarAsync(contexto.EmpresaId.Value, new EnviarFacturaComando(id, peticion.Email), ct).ConfigureAwait(false);
        return resultado.ASinContenido();
    }
}

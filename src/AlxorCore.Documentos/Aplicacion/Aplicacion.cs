using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;

namespace AlxorCore.Documentos.Aplicacion;

/// <summary>Puerto de generación del PDF de una factura.</summary>
public interface IGeneradorPdfFactura
{
    /// <summary>Genera el PDF de la factura con los datos del emisor (la empresa).</summary>
    byte[] Generar(FacturaDto factura, EmpresaDto emisor);
}

/// <summary>Mensaje de correo con un adjunto.</summary>
public sealed record MensajeCorreo(string Para, string Asunto, string Cuerpo, byte[] Adjunto, string NombreAdjunto);

/// <summary>Puerto de envío de correo. En el MVP la implementación es un stub.</summary>
public interface IServicioCorreo
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);
}

/// <summary>Caso de uso: generar el PDF de una factura.</summary>
public sealed class GenerarPdfFactura
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaEmpresas _empresas;
    private readonly IGeneradorPdfFactura _generador;

    public GenerarPdfFactura(IConsultaFacturas facturas, IConsultaEmpresas empresas, IGeneradorPdfFactura generador)
    {
        _facturas = facturas;
        _empresas = empresas;
        _generador = generador;
    }

    public async Task<Resultado<DocumentoPdf>> EjecutarAsync(Guid empresaId, Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _facturas.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
        if (factura is null)
        {
            return Resultado.Fallo<DocumentoPdf>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."));
        }

        var empresa = await _empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<DocumentoPdf>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        var bytes = _generador.Generar(factura, empresa);
        return Resultado.Ok(new DocumentoPdf($"{factura.NumeroCompleto.Replace('/', '-')}.pdf", bytes));
    }
}

/// <summary>PDF generado (nombre de archivo y contenido).</summary>
public sealed record DocumentoPdf(string NombreArchivo, byte[] Contenido);

/// <summary>Datos para enviar una factura por correo.</summary>
public sealed record EnviarFacturaComando(Guid FacturaId, string Email);

/// <summary>Caso de uso: enviar una factura por correo con su PDF adjunto.</summary>
public sealed class EnviarFacturaPorEmail
{
    private readonly GenerarPdfFactura _generarPdf;
    private readonly IServicioCorreo _correo;

    public EnviarFacturaPorEmail(GenerarPdfFactura generarPdf, IServicioCorreo correo)
    {
        _generarPdf = generarPdf;
        _correo = correo;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, EnviarFacturaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (string.IsNullOrWhiteSpace(comando.Email))
        {
            return Resultado.Fallo(Error.Validacion("correo.destinatario", "El correo del destinatario es obligatorio."));
        }

        var pdf = await _generarPdf.EjecutarAsync(empresaId, comando.FacturaId, ct).ConfigureAwait(false);
        if (pdf.EsFallo)
        {
            return Resultado.Fallo(pdf.Error);
        }

        var mensaje = new MensajeCorreo(
            comando.Email.Trim(),
            $"Factura {pdf.Valor.NombreArchivo}",
            "Adjuntamos su factura. Gracias por su confianza.",
            pdf.Valor.Contenido,
            pdf.Valor.NombreArchivo);

        await _correo.EnviarAsync(mensaje, ct).ConfigureAwait(false);
        return Resultado.Ok();
    }
}

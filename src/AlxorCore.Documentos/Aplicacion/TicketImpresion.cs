using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;

namespace AlxorCore.Documentos.Aplicacion;

/// <summary>Puerto de generación del ticket de una factura en formato <b>ESC/POS</b> (impresora térmica).</summary>
public interface IGeneradorTicketEscPos
{
    /// <summary>Genera los bytes ESC/POS del ticket con los datos del emisor (la empresa).</summary>
    byte[] Generar(FacturaDto factura, EmpresaDto emisor);
}

/// <summary>
/// Puerto de envío de un trabajo de impresión a la impresora de tickets. La implementación real
/// (impresora de red por TCP) se elige por configuración; si no hay ninguna, se usa una nula.
/// </summary>
public interface IImpresoraTickets
{
    /// <summary>¿Hay una impresora configurada?</summary>
    bool Configurada { get; }

    /// <summary>Envía los bytes (p. ej. ESC/POS) a la impresora.</summary>
    Task ImprimirAsync(byte[] datos, CancellationToken ct = default);
}

/// <summary>Documento listo para imprimir (nombre de archivo y contenido en bytes).</summary>
public sealed record DocumentoImpresion(string NombreArchivo, byte[] Contenido);

/// <summary>Caso de uso: obtener el ticket ESC/POS de una factura (para descargar o enviar a impresora).</summary>
public sealed class ObtenerTicketEscPos
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaEmpresas _empresas;
    private readonly IGeneradorTicketEscPos _generador;

    public ObtenerTicketEscPos(IConsultaFacturas facturas, IConsultaEmpresas empresas, IGeneradorTicketEscPos generador)
    {
        _facturas = facturas;
        _empresas = empresas;
        _generador = generador;
    }

    public async Task<Resultado<DocumentoImpresion>> EjecutarAsync(Guid empresaId, Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _facturas.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
        if (factura is null)
        {
            return Resultado.Fallo<DocumentoImpresion>(Error.NoEncontrado("factura.no_encontrada", "La factura no existe."));
        }

        var empresa = await _empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<DocumentoImpresion>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        var bytes = _generador.Generar(factura, empresa);
        return Resultado.Ok(new DocumentoImpresion($"{factura.NumeroCompleto.Replace('/', '-')}.escpos", bytes));
    }
}

/// <summary>Caso de uso: imprimir el ticket de una factura en la impresora térmica configurada.</summary>
public sealed class ImprimirTicket
{
    private readonly ObtenerTicketEscPos _obtener;
    private readonly IImpresoraTickets _impresora;

    public ImprimirTicket(ObtenerTicketEscPos obtener, IImpresoraTickets impresora)
    {
        _obtener = obtener;
        _impresora = impresora;
    }

    public async Task<Resultado> EjecutarAsync(Guid empresaId, Guid facturaId, CancellationToken ct = default)
    {
        if (!_impresora.Configurada)
        {
            return Resultado.Fallo(Error.Validacion("impresora.no_configurada",
                "No hay ninguna impresora de tickets configurada. Configura la sección «Impresora» o descarga el ticket."));
        }

        var doc = await _obtener.EjecutarAsync(empresaId, facturaId, ct).ConfigureAwait(false);
        if (doc.EsFallo)
        {
            return Resultado.Fallo(doc.Error);
        }

        try
        {
            await _impresora.ImprimirAsync(doc.Valor.Contenido, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Cualquier fallo de E/S con la impresora se traduce a un error de negocio legible.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Resultado.Fallo(Error.Conflicto("impresora.error", $"No se pudo imprimir el ticket: {ex.Message}"));
        }

        return Resultado.Ok();
    }
}

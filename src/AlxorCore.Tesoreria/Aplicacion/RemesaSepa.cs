using System.Globalization;
using System.Xml.Linq;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Terceros.Aplicacion;
using AlxorCore.Tesoreria.Dominio;

namespace AlxorCore.Tesoreria.Aplicacion;

/// <summary>Resultado de generar una remesa de adeudos SEPA (Norma 19).</summary>
public sealed record RemesaSepaDto(string FicheroXml, string NombreArchivo, int NumeroAdeudos, decimal Total, IReadOnlyList<string> Omitidas);

/// <summary>Datos para generar una remesa: las facturas que se quieren domiciliar.</summary>
public sealed record GenerarRemesaComando(IReadOnlyList<Guid> FacturaIds, DateOnly? FechaCobro = null);

/// <summary>
/// Caso de uso: genera una <b>remesa de adeudos directos SEPA</b> (fichero <c>pain.008.001.02</c>,
/// equivalente a la Norma 19) para cobrar por domiciliación las facturas indicadas. Necesita que la
/// empresa tenga IBAN e identificador de acreedor, y que cada cliente tenga IBAN y mandato. Cobra el
/// <b>importe pendiente</b> de cada factura; omite (informando) las que no se pueden domiciliar.
/// </summary>
public sealed class GenerarRemesaSepa
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.02";

    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaClientes _clientes;
    private readonly IConsultaEmpresas _empresas;
    private readonly IRepositorioMovimientos _movimientos;
    private readonly IReloj _reloj;

    public GenerarRemesaSepa(IConsultaFacturas facturas, IConsultaClientes clientes, IConsultaEmpresas empresas, IRepositorioMovimientos movimientos, IReloj reloj)
    {
        _facturas = facturas;
        _clientes = clientes;
        _empresas = empresas;
        _movimientos = movimientos;
        _reloj = reloj;
    }

    public async Task<Resultado<RemesaSepaDto>> EjecutarAsync(Guid empresaId, GenerarRemesaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.FacturaIds is null || comando.FacturaIds.Count == 0)
        {
            return Resultado.Fallo<RemesaSepaDto>(Error.Validacion("remesa.sin_facturas", "Selecciona al menos una factura para domiciliar."));
        }

        var empresa = await _empresas.ObtenerAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<RemesaSepaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        if (string.IsNullOrWhiteSpace(empresa.Iban) || string.IsNullOrWhiteSpace(empresa.IdentificadorAcreedor))
        {
            return Resultado.Fallo<RemesaSepaDto>(Error.Validacion("remesa.empresa_sin_datos_cobro", "Configura el IBAN y el identificador de acreedor de la empresa (Ajustes → Datos de cobro)."));
        }

        var adeudos = new List<Adeudo>();
        var omitidas = new List<string>();

        foreach (var facturaId in comando.FacturaIds)
        {
            var factura = await _facturas.ObtenerAsync(facturaId, ct).ConfigureAwait(false);
            if (factura is null || factura.ClienteId is null)
            {
                omitidas.Add($"{facturaId}: factura no encontrada o sin cliente.");
                continue;
            }

            var liquidado = await _movimientos.SumaAsync(TipoDocumentoTesoreria.Factura, facturaId, ct).ConfigureAwait(false);
            var pendiente = Redondeo.Dos(factura.Total - liquidado);
            if (pendiente <= 0)
            {
                omitidas.Add($"{factura.NumeroCompleto}: ya está cobrada.");
                continue;
            }

            var cliente = await _clientes.ObtenerAsync(factura.ClienteId.Value, ct).ConfigureAwait(false);
            if (cliente is null || string.IsNullOrWhiteSpace(cliente.Iban) || string.IsNullOrWhiteSpace(cliente.MandatoReferencia) || cliente.MandatoFecha is null)
            {
                omitidas.Add($"{factura.NumeroCompleto}: el cliente no tiene IBAN y mandato de domiciliación.");
                continue;
            }

            adeudos.Add(new Adeudo(factura.NumeroCompleto, pendiente, cliente.Nombre, cliente.Iban!, cliente.MandatoReferencia!, cliente.MandatoFecha!.Value));
        }

        if (adeudos.Count == 0)
        {
            return Resultado.Fallo<RemesaSepaDto>(Error.Validacion("remesa.sin_adeudos", "Ninguna de las facturas seleccionadas se puede domiciliar. " + string.Join(" ", omitidas)));
        }

        var fechaCobro = comando.FechaCobro ?? DateOnly.FromDateTime(_reloj.AhoraUtc.UtcDateTime).AddDays(3);
        var xml = Construir(empresa.RazonSocial, empresa.Nif, empresa.Iban!, empresa.IdentificadorAcreedor!, fechaCobro, adeudos);
        var total = Redondeo.Dos(adeudos.Sum(a => a.Importe));
        var nombre = $"remesa-{fechaCobro:yyyyMMdd}.xml";
        return Resultado.Ok(new RemesaSepaDto(xml, nombre, adeudos.Count, total, omitidas));
    }

    private string Construir(string acreedorNombre, string acreedorNif, string acreedorIban, string acreedorId, DateOnly fechaCobro, IReadOnlyList<Adeudo> adeudos)
    {
        var total = Redondeo.Dos(adeudos.Sum(a => a.Importe)).ToString("F2", CultureInfo.InvariantCulture);
        var numero = adeudos.Count.ToString(CultureInfo.InvariantCulture);
        var ahora = _reloj.AhoraUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var mensajeId = $"ALX{_reloj.AhoraUtc:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..Math.Min(35, 17 + 18)];

        var transacciones = adeudos.Select(a => new XElement(
            Ns + "DrctDbtTxInf",
            new XElement(Ns + "PmtId", new XElement(Ns + "EndToEndId", Limitar(a.NumeroFactura, 35))),
            new XElement(Ns + "InstdAmt", new XAttribute("Ccy", "EUR"), a.Importe.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(
                Ns + "DrctDbtTx",
                new XElement(
                    Ns + "MndtRltdInf",
                    new XElement(Ns + "MndtId", Limitar(a.MandatoReferencia, 35)),
                    new XElement(Ns + "DtOfSgntr", a.MandatoFecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))),
            new XElement(Ns + "DbtrAgt", new XElement(Ns + "FinInstnId", new XElement(Ns + "Othr", new XElement(Ns + "Id", "NOTPROVIDED")))),
            new XElement(Ns + "Dbtr", new XElement(Ns + "Nm", Limitar(a.ClienteNombre, 70))),
            new XElement(Ns + "DbtrAcct", new XElement(Ns + "Id", new XElement(Ns + "IBAN", a.Iban))),
            new XElement(Ns + "RmtInf", new XElement(Ns + "Ustrd", Limitar($"Factura {a.NumeroFactura}", 140)))));

        var documento = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                Ns + "Document",
                new XElement(
                    Ns + "CstmrDrctDbtInitn",
                    new XElement(
                        Ns + "GrpHdr",
                        new XElement(Ns + "MsgId", mensajeId),
                        new XElement(Ns + "CreDtTm", ahora),
                        new XElement(Ns + "NbOfTxs", numero),
                        new XElement(Ns + "CtrlSum", total),
                        new XElement(Ns + "InitgPty", new XElement(Ns + "Nm", Limitar(acreedorNombre, 70)))),
                    new XElement(
                        Ns + "PmtInf",
                        new XElement(Ns + "PmtInfId", mensajeId),
                        new XElement(Ns + "PmtMtd", "DD"),
                        new XElement(Ns + "NbOfTxs", numero),
                        new XElement(Ns + "CtrlSum", total),
                        new XElement(
                            Ns + "PmtTpInf",
                            new XElement(Ns + "SvcLvl", new XElement(Ns + "Cd", "SEPA")),
                            new XElement(Ns + "LclInstrm", new XElement(Ns + "Cd", "CORE")),
                            new XElement(Ns + "SeqTp", "OOFF")),
                        new XElement(Ns + "ReqdColltnDt", fechaCobro.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                        new XElement(Ns + "Cdtr", new XElement(Ns + "Nm", Limitar(acreedorNombre, 70))),
                        new XElement(Ns + "CdtrAcct", new XElement(Ns + "Id", new XElement(Ns + "IBAN", acreedorIban))),
                        new XElement(Ns + "CdtrAgt", new XElement(Ns + "FinInstnId", new XElement(Ns + "Othr", new XElement(Ns + "Id", "NOTPROVIDED")))),
                        new XElement(
                            Ns + "CdtrSchmeId",
                            new XElement(
                                Ns + "Id",
                                new XElement(
                                    Ns + "PrvtId",
                                    new XElement(
                                        Ns + "Othr",
                                        new XElement(Ns + "Id", acreedorId),
                                        new XElement(Ns + "SchmeNm", new XElement(Ns + "Prtry", "SEPA")))))),
                        transacciones))));

        return documento.Declaration + Environment.NewLine + documento.ToString();
    }

    private static string Limitar(string valor, int max) => valor.Length <= max ? valor : valor[..max];

    private sealed record Adeudo(string NumeroFactura, decimal Importe, string ClienteNombre, string Iban, string MandatoReferencia, DateOnly MandatoFecha);
}

using System.Globalization;
using System.Text;
using System.Xml;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Organizacion.Aplicacion.Modelos;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Genera el <b>registro de alta VeriFactu</b> de una factura en XML, con la estructura y los campos
/// que exige la AEAT (IDFactura, TipoFactura, desglose de IVA, importes, encadenamiento con la huella
/// anterior, sistema informático, huella…). Es el documento que se remitiría al servicio web de la
/// AEAT; aquí se genera y se puede inspeccionar. El envío en vivo (SOAP + certificado) es el paso
/// posterior, que solo requiere conectar el certificado sin rehacer este registro.
/// </summary>
public static class GeneradorXmlVerifactu
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        var sb = new StringBuilder();
        var ajustes = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, OmitXmlDeclaration = false };
        using var w = XmlWriter.Create(sb, ajustes);

        w.WriteStartDocument();
        w.WriteStartElement("RegistroAlta");
        w.WriteElementString("IDVersion", "1.0");

        w.WriteStartElement("IDFactura");
        w.WriteElementString("IDEmisorFactura", emisor.Nif);
        w.WriteElementString("NumSerieFactura", factura.NumeroCompleto);
        w.WriteElementString("FechaExpedicionFactura", factura.FechaEmision.ToString("dd-MM-yyyy", Inv));
        w.WriteEndElement();

        w.WriteElementString("NombreRazonEmisor", emisor.RazonSocial);
        w.WriteElementString("TipoFactura", Verifactu.TipoCodigo(TipoFacturaDe(factura.Tipo)));
        w.WriteElementString("DescripcionOperacion", DescripcionOperacion(factura));

        // Destinatario (salvo tickets sin cliente identificado).
        if (factura.ClienteId is not null || !string.IsNullOrWhiteSpace(factura.ClienteNif))
        {
            w.WriteStartElement("Destinatarios");
            w.WriteStartElement("IDDestinatario");
            w.WriteElementString("NombreRazon", factura.ClienteNombre);
            if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
            {
                w.WriteElementString("NIF", factura.ClienteNif);
            }

            w.WriteEndElement();
            w.WriteEndElement();
        }

        // Desglose por tipo de IVA.
        w.WriteStartElement("Desglose");
        foreach (var grupo in factura.Lineas.GroupBy(l => l.PorcentajeIva))
        {
            var baseImp = Redondear(grupo.Sum(l => l.Base));
            var cuota = Redondear(grupo.Sum(l => l.CuotaIva));
            w.WriteStartElement("DetalleDesglose");
            w.WriteElementString("ClaveRegimen", "01"); // régimen general
            w.WriteElementString("CalificacionOperacion", "S1"); // sujeta y no exenta
            w.WriteElementString("TipoImpositivo", grupo.Key.ToString("F2", Inv));
            w.WriteElementString("BaseImponibleOimporteNoSujeto", baseImp.ToString("F2", Inv));
            w.WriteElementString("CuotaRepercutida", cuota.ToString("F2", Inv));
            w.WriteEndElement();
        }

        w.WriteEndElement();

        w.WriteElementString("CuotaTotal", factura.CuotaIva.ToString("F2", Inv));
        w.WriteElementString("ImporteTotal", factura.Total.ToString("F2", Inv));

        // Encadenamiento: primer registro o huella del anterior.
        w.WriteStartElement("Encadenamiento");
        if (string.IsNullOrEmpty(factura.HuellaAnterior))
        {
            w.WriteElementString("PrimerRegistro", "S");
        }
        else
        {
            w.WriteStartElement("RegistroAnterior");
            w.WriteElementString("Huella", factura.HuellaAnterior);
            w.WriteEndElement();
        }

        w.WriteEndElement();

        w.WriteStartElement("SistemaInformatico");
        w.WriteElementString("NombreRazon", "ALXOR Core");
        w.WriteElementString("NombreSistemaInformatico", "ALXOR Core");
        w.WriteElementString("Version", "1.0");
        w.WriteEndElement();

        var generado = factura.FechaHoraGenRegistro ?? factura.FechaEmision.ToDateTime(TimeOnly.MinValue);
        w.WriteElementString("FechaHoraHusoGenRegistro", generado.ToString("yyyy-MM-ddTHH:mm:sszzz", Inv));
        w.WriteElementString("TipoHuella", "01"); // 01 = SHA-256
        w.WriteElementString("Huella", factura.Huella ?? string.Empty);

        w.WriteEndElement();
        w.WriteEndDocument();
        w.Flush();
        return sb.ToString();
    }

    private static TipoFactura TipoFacturaDe(string tipo) => tipo switch
    {
        "Simplificada" => TipoFactura.Simplificada,
        "Rectificativa" => TipoFactura.Rectificativa,
        _ => TipoFactura.Ordinaria,
    };

    private static string DescripcionOperacion(FacturaDto factura) =>
        factura.Lineas.Count > 0 ? factura.Lineas[0].Descripcion : "Operación";

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}

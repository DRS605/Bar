using System.Globalization;
using System.Text;
using System.Xml;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Organizacion.Aplicacion.Modelos;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Genera el <b>registro de alta VeriFactu</b> de una factura como documento <c>RegFactuSistemaFacturacion</c>
/// listo para remitir al servicio web de la AEAT: sobre + <c>Cabecera</c> (obligado a emitir) + el
/// <c>RegistroAlta</c> (IDFactura, tipo, desglose de IVA, importes, encadenamiento con la huella anterior,
/// sistema informático y huella), con los <b>espacios de nombres</b> oficiales
/// (<c>SuministroLR.xsd</c> = <c>sum</c>, <c>SuministroInformacion.xsd</c> = <c>sum1</c>). El envío en vivo
/// (SOAP + certificado mTLS) es el paso posterior y no rehace este registro.
/// </summary>
public static class GeneradorXmlVerifactu
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Espacio de nombres del listado de registros (SuministroLR.xsd).</summary>
    public const string NsLR = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";

    /// <summary>Espacio de nombres de los tipos comunes (SuministroInformacion.xsd).</summary>
    public const string NsInfo = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

    public static string Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var ms = new MemoryStream();
        var ajustes = new XmlWriterSettings { Indent = true, Encoding = utf8, OmitXmlDeclaration = false };
        using var w = XmlWriter.Create(ms, ajustes);

        void Info(string nombre, string valor) => w.WriteElementString("sum1", nombre, NsInfo, valor);

        w.WriteStartDocument();

        // Sobre del listado de registros (SuministroLR).
        w.WriteStartElement("sum", "RegFactuSistemaFacturacion", NsLR);
        w.WriteAttributeString("xmlns", "sum1", null, NsInfo);

        // Cabecera: obligado a emitir (el local).
        w.WriteStartElement("sum", "Cabecera", NsLR);
        w.WriteStartElement("sum1", "ObligadoEmision", NsInfo);
        Info("NombreRazon", emisor.RazonSocial);
        Info("NIF", emisor.Nif);
        w.WriteEndElement();
        w.WriteEndElement();

        // Registro de alta.
        w.WriteStartElement("sum", "RegistroFactura", NsLR);
        w.WriteStartElement("sum1", "RegistroAlta", NsInfo);

        Info("IDVersion", "1.0");

        w.WriteStartElement("sum1", "IDFactura", NsInfo);
        Info("IDEmisorFactura", emisor.Nif);
        Info("NumSerieFactura", factura.NumeroCompleto);
        Info("FechaExpedicionFactura", factura.FechaEmision.ToString("dd-MM-yyyy", Inv));
        w.WriteEndElement();

        Info("NombreRazonEmisor", emisor.RazonSocial);
        Info("TipoFactura", Verifactu.TipoCodigo(TipoFacturaDe(factura.Tipo)));
        Info("DescripcionOperacion", DescripcionOperacion(factura));

        // Destinatario (salvo tickets/simplificadas sin cliente identificado).
        if (factura.ClienteId is not null || !string.IsNullOrWhiteSpace(factura.ClienteNif))
        {
            w.WriteStartElement("sum1", "Destinatarios", NsInfo);
            w.WriteStartElement("sum1", "IDDestinatario", NsInfo);
            Info("NombreRazon", factura.ClienteNombre);
            if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
            {
                Info("NIF", factura.ClienteNif);
            }

            w.WriteEndElement();
            w.WriteEndElement();
        }

        // Desglose por tipo de IVA.
        w.WriteStartElement("sum1", "Desglose", NsInfo);
        foreach (var grupo in factura.Lineas.GroupBy(l => l.PorcentajeIva))
        {
            w.WriteStartElement("sum1", "DetalleDesglose", NsInfo);
            Info("Impuesto", "01");            // 01 = IVA
            Info("ClaveRegimen", "01");        // 01 = régimen general
            Info("CalificacionOperacion", "S1"); // S1 = sujeta y no exenta
            Info("TipoImpositivo", grupo.Key.ToString("F2", Inv));
            Info("BaseImponibleOimporteNoSujeto", Redondear(grupo.Sum(l => l.Base)).ToString("F2", Inv));
            Info("CuotaRepercutida", Redondear(grupo.Sum(l => l.CuotaIva)).ToString("F2", Inv));

            var recargo = Redondear(grupo.Sum(l => l.CuotaRecargo));
            if (recargo > 0m)
            {
                Info("TipoRecargoEquivalencia", grupo.First().PorcentajeRecargo.ToString("F2", Inv));
                Info("CuotaRecargoEquivalencia", recargo.ToString("F2", Inv));
            }

            w.WriteEndElement();
        }

        w.WriteEndElement();

        Info("CuotaTotal", factura.CuotaIva.ToString("F2", Inv));
        Info("ImporteTotal", factura.Total.ToString("F2", Inv));

        // Encadenamiento: primer registro o huella del anterior.
        w.WriteStartElement("sum1", "Encadenamiento", NsInfo);
        if (string.IsNullOrEmpty(factura.HuellaAnterior))
        {
            Info("PrimerRegistro", "S");
        }
        else
        {
            // NumSerie/Fecha del registro anterior se completan al conectar la remisión real (encadenado
            // por empresa); aquí van el emisor y la huella anterior, que sí conocemos.
            w.WriteStartElement("sum1", "RegistroAnterior", NsInfo);
            Info("IDEmisorFactura", emisor.Nif);
            Info("Huella", factura.HuellaAnterior);
            w.WriteEndElement();
        }

        w.WriteEndElement();

        // Sistema informático (Comandia). Los datos de registro definitivos del SIF se fijan en la
        // certificación; aquí van los identificativos del producto.
        w.WriteStartElement("sum1", "SistemaInformatico", NsInfo);
        Info("NombreRazon", "Comandia");
        Info("NIF", emisor.Nif);
        Info("NombreSistemaInformatico", "Comandia");
        Info("IdSistemaInformatico", "01");
        Info("Version", "1.0");
        Info("NumeroInstalacion", "1");
        Info("TipoUsoPosibleSoloVerifactu", "S");
        Info("TipoUsoPosibleMultiOT", "N");
        Info("IndicadorMultiplesOT", "N");
        w.WriteEndElement();

        var generado = factura.FechaHoraGenRegistro ?? factura.FechaEmision.ToDateTime(TimeOnly.MinValue);
        Info("FechaHoraHusoGenRegistro", generado.ToString("yyyy-MM-ddTHH:mm:sszzz", Inv));
        Info("TipoHuella", "01"); // 01 = SHA-256
        Info("Huella", factura.Huella ?? string.Empty);

        w.WriteEndElement(); // RegistroAlta
        w.WriteEndElement(); // RegistroFactura
        w.WriteEndElement(); // RegFactuSistemaFacturacion
        w.WriteEndDocument();
        w.Flush();
        return utf8.GetString(ms.ToArray());
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

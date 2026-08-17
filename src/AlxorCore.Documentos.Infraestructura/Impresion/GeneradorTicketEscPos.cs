using System.Globalization;
using System.Text;
using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Organizacion.Aplicacion.Modelos;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Genera el ticket de una factura en <b>ESC/POS</b>, el lenguaje de las impresoras térmicas de tickets
/// (papel de 80 mm ≈ 48 columnas). Produce la secuencia de bytes lista para enviar a la impresora:
/// cabecera del local, líneas, totales con IVA, pie y corte de papel. El texto se codifica en la página
/// de códigos <b>858</b> (Europa occidental, con «€») y se le indica a la impresora que la use.
/// </summary>
internal sealed class GeneradorTicketEscPos : IGeneradorTicketEscPos
{
    private const int Ancho = 48;                 // columnas del papel de 80 mm en fuente A
    private const int PaginaCodigos858 = 19;      // tabla ESC/POS para la página de códigos 858 (Epson)

    // Formato español (coma decimal, punto de millares) sin depender de culturas instaladas —el entorno
    // puede correr en modo «globalization-invariant» (sin ICU), donde GetCultureInfo("es-ES") falla.
    private static readonly NumberFormatInfo Es = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = new[] { 3 } };
    private static readonly Encoding Cp858 = CrearCodificacion();

    // ESC/POS
    private static readonly byte[] Inicializar = { 0x1B, 0x40 };                 // ESC @
    private static readonly byte[] SeleccionarCp = { 0x1B, 0x74, PaginaCodigos858 }; // ESC t n
    private static readonly byte[] AlinearIzq = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] AlinearCentro = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] TamanoDoble = { 0x1D, 0x21, 0x11 };           // GS ! doble alto+ancho
    private static readonly byte[] TamanoNormal = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] CortarPapel = { 0x1D, 0x56, 0x42, 0x00 };     // GS V B: avanza y corta

    public byte[] Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        using var flujo = new MemoryStream();
        void Bytes(byte[] b) => flujo.Write(b, 0, b.Length);
        void Texto(string t) { var b = Cp858.GetBytes(t); flujo.Write(b, 0, b.Length); }
        void Linea(string t = "") { Texto(t); flujo.WriteByte(0x0A); }

        Bytes(Inicializar);
        Bytes(SeleccionarCp);

        // Cabecera: nombre del local grande y centrado.
        Bytes(AlinearCentro);
        Bytes(NegritaOn); Bytes(TamanoDoble);
        Linea(Recortar(emisor.RazonSocial, Ancho / 2));
        Bytes(TamanoNormal); Bytes(NegritaOff);
        Linea($"NIF: {emisor.Nif}");
        Linea($"Ticket {factura.NumeroCompleto}");
        Linea(factura.FechaEmision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

        // Cuerpo: una o dos líneas por consumición.
        Bytes(AlinearIzq);
        Linea(new string('-', Ancho));
        foreach (var l in factura.Lineas)
        {
            Linea(Recortar(l.Descripcion, Ancho));
            var detalle = $"  {Cantidad(l.Cantidad)} x {Eur(l.PrecioUnitario)}";
            Linea(IzqDer(detalle, Eur(l.Base + l.CuotaIva)));
        }

        Linea(new string('-', Ancho));
        Linea(IzqDer("Base imponible", Eur(factura.BaseImponible)));
        Linea(IzqDer("IVA", Eur(factura.CuotaIva)));

        // Total destacado.
        Bytes(NegritaOn); Bytes(TamanoDoble);
        Linea(IzqDer("TOTAL", Eur(factura.Total), Ancho / 2));
        Bytes(TamanoNormal); Bytes(NegritaOff);

        // VeriFactu: leyenda + QR de cotejo de la AEAT (obligatorio en el ticket cuando hay registro).
        if (!string.IsNullOrEmpty(factura.Huella))
        {
            Linea();
            Bytes(AlinearCentro);
            Bytes(NegritaOn); Linea("VERI*FACTU"); Bytes(NegritaOff);
            Linea("Factura verificable en la sede de la AEAT");
            var url = Verifactu.UrlCotejo(emisor.Nif, factura.NumeroCompleto, factura.FechaEmision, factura.Total);
            Bytes(QrEscPos.Raster(url));
            Linea();
            Linea($"Huella: {factura.Huella[..Math.Min(16, factura.Huella.Length)]}...");
        }

        // Pie.
        Linea();
        Bytes(AlinearCentro);
        Linea("Gracias por su visita");
        Linea("Enviado con Comandia");
        Linea(); Linea(); Linea();

        Bytes(CortarPapel);
        return flujo.ToArray();
    }

    private static string Eur(decimal valor) => valor.ToString("N2", Es) + " €";

    private static string Cantidad(decimal cantidad) =>
        cantidad == Math.Truncate(cantidad) ? ((long)cantidad).ToString(Es) : cantidad.ToString("0.###", Es);

    private static string Recortar(string texto, int ancho) =>
        string.IsNullOrEmpty(texto) ? string.Empty : (texto.Length > ancho ? texto[..ancho] : texto);

    /// <summary>Coloca <paramref name="izq"/> a la izquierda y <paramref name="der"/> a la derecha del ancho dado.</summary>
    private static string IzqDer(string izq, string der, int ancho = Ancho)
    {
        var huecoMin = 1;
        var maxIzq = Math.Max(0, ancho - der.Length - huecoMin);
        var i = izq.Length > maxIzq ? izq[..maxIzq] : izq;
        var relleno = Math.Max(huecoMin, ancho - i.Length - der.Length);
        return i + new string(' ', relleno) + der;
    }

    private static Encoding CrearCodificacion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858);
    }
}

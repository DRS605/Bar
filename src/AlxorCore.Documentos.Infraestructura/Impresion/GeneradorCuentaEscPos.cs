using System.Globalization;
using System.Text;
using AlxorCore.Documentos.Aplicacion;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Genera la <b>cuenta previa</b> (pre-ticket) de una mesa en <b>ESC/POS</b>: lo consumido con sus
/// importes y el total, marcada claramente como <b>documento sin valor fiscal</b> (no es la factura).
/// Es lo que el cliente pide para revisar antes de pagar. El texto usa la página de códigos <b>858</b>
/// (acentos y «€») como el resto de la impresión.
/// </summary>
internal sealed class GeneradorCuentaEscPos : IGeneradorCuenta
{
    private const int Ancho = 48;                 // columnas del papel de 80 mm en fuente A
    private const int PaginaCodigos858 = 19;      // tabla ESC/POS para la página de códigos 858 (Epson)

    private static readonly NumberFormatInfo Es = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = new[] { 3 } };
    private static readonly Encoding Cp858 = CrearCodificacion();

    private static readonly byte[] Inicializar = { 0x1B, 0x40 };
    private static readonly byte[] SeleccionarCp = { 0x1B, 0x74, PaginaCodigos858 };
    private static readonly byte[] AlinearIzq = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] AlinearCentro = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] TamanoDoble = { 0x1D, 0x21, 0x11 };
    private static readonly byte[] TamanoNormal = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] CortarPapel = { 0x1D, 0x56, 0x42, 0x00 };

    public byte[] Generar(DatosCuenta datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        using var flujo = new MemoryStream();
        void Bytes(byte[] b) => flujo.Write(b, 0, b.Length);
        void Texto(string t) { var b = Cp858.GetBytes(t); flujo.Write(b, 0, b.Length); }
        void Linea(string t = "") { Texto(t); flujo.WriteByte(0x0A); }

        Bytes(Inicializar);
        Bytes(SeleccionarCp);

        // Cabecera: nombre del local grande y centrado, mesa y hora.
        Bytes(AlinearCentro);
        Bytes(NegritaOn); Bytes(TamanoDoble);
        Linea(Recortar(datos.Local, Ancho / 2));
        Bytes(TamanoNormal);
        Linea("CUENTA");
        Bytes(NegritaOff);
        Linea(datos.Mesa);
        Linea(datos.Hora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));

        // Cuerpo: una o dos líneas por consumición.
        Bytes(AlinearIzq);
        Linea(new string('-', Ancho));
        foreach (var l in datos.Lineas)
        {
            Linea(Recortar(l.Descripcion, Ancho));
            var detalle = $"  {Cantidad(l.Cantidad)} x {Eur(l.PrecioUnitario)}";
            Linea(IzqDer(detalle, Eur(l.Total)));
        }

        Linea(new string('-', Ancho));
        Linea(IzqDer("Base imponible", Eur(datos.Base)));
        Linea(IzqDer("IVA", Eur(datos.CuotaIva)));

        // Total destacado.
        Bytes(NegritaOn); Bytes(TamanoDoble);
        Linea(IzqDer("TOTAL", Eur(datos.Total), Ancho / 2));
        Bytes(TamanoNormal); Bytes(NegritaOff);

        if (!string.IsNullOrWhiteSpace(datos.Notas))
        {
            Bytes(AlinearIzq);
            Linea($"Nota: {datos.Notas}");
        }

        // Pie: deja claro que NO es la factura.
        Linea();
        Bytes(AlinearCentro);
        Bytes(NegritaOn);
        Linea("Documento sin valor fiscal");
        Bytes(NegritaOff);
        Linea("No es una factura. Pida su ticket al pagar.");
        Linea(); Linea(); Linea();

        Bytes(CortarPapel);
        return flujo.ToArray();
    }

    private static string Eur(decimal valor) => valor.ToString("N2", Es) + " €";

    private static string Cantidad(decimal cantidad) =>
        cantidad == Math.Truncate(cantidad) ? ((long)cantidad).ToString(Es) : cantidad.ToString("0.###", Es);

    private static string Recortar(string texto, int ancho) =>
        string.IsNullOrEmpty(texto) ? string.Empty : (texto.Length > ancho ? texto[..ancho] : texto);

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

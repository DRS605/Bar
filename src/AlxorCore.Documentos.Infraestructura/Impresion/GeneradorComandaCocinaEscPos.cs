using System.Globalization;
using System.Text;
using AlxorCore.Documentos.Aplicacion;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Genera la <b>comanda de cocina/barra</b> en <b>ESC/POS</b>: lo que hay que preparar (mesa, hora y
/// artículos en grande, <b>sin precios</b>), para el pase de cocina. Distinta del ticket de cobro. El
/// texto usa la página de códigos <b>858</b> (acentos y «€») como el resto de la impresión.
/// </summary>
internal sealed class GeneradorComandaCocinaEscPos : IGeneradorComandaCocina
{
    private static readonly NumberFormatInfo Es = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = new[] { 3 } };
    private static readonly Encoding Cp858 = CrearCodificacion();

    private static readonly byte[] Inicializar = { 0x1B, 0x40 };
    private static readonly byte[] SeleccionarCp = { 0x1B, 0x74, 19 };
    private static readonly byte[] AlinearIzq = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] AlinearCentro = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] TamanoDoble = { 0x1D, 0x21, 0x11 };
    private static readonly byte[] TamanoAlto = { 0x1D, 0x21, 0x01 };  // solo doble alto
    private static readonly byte[] TamanoNormal = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] CortarPapel = { 0x1D, 0x56, 0x42, 0x00 };

    public byte[] Generar(DatosComandaCocina datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        using var flujo = new MemoryStream();
        void Bytes(byte[] b) => flujo.Write(b, 0, b.Length);
        void Linea(string t = "") { var b = Cp858.GetBytes(t); flujo.Write(b, 0, b.Length); flujo.WriteByte(0x0A); }

        Bytes(Inicializar);
        Bytes(SeleccionarCp);

        // Cabecera grande y centrada: mesa y hora.
        Bytes(AlinearCentro);
        Bytes(NegritaOn); Bytes(TamanoDoble);
        Linea(datos.Mesa);
        Bytes(TamanoNormal);
        Linea(datos.Hora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        Bytes(NegritaOff);

        // Artículos a preparar, en grande y sin precios.
        Bytes(AlinearIzq);
        Linea(new string('-', 32));
        Bytes(NegritaOn); Bytes(TamanoAlto);
        foreach (var l in datos.Lineas)
        {
            Linea($"{Cantidad(l.Cantidad)} x {l.Descripcion}");
        }

        Bytes(TamanoNormal); Bytes(NegritaOff);
        Linea(new string('-', 32));

        if (!string.IsNullOrWhiteSpace(datos.Notas))
        {
            Linea($"Nota: {datos.Notas}");
        }

        Linea(); Linea(); Linea();
        Bytes(CortarPapel);
        return flujo.ToArray();
    }

    private static string Cantidad(decimal cantidad) =>
        cantidad == Math.Truncate(cantidad) ? ((long)cantidad).ToString(Es) : cantidad.ToString("0.###", Es);

    private static Encoding CrearCodificacion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858);
    }
}

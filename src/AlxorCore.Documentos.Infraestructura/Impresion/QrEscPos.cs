using QRCoder;

namespace AlxorCore.Documentos.Infraestructura.Impresion;

/// <summary>
/// Rasteriza un código <b>QR</b> (con QRCoder) a un bloque de <b>imagen ESC/POS</b> (<c>GS v 0</c>),
/// que imprimen prácticamente todas las térmicas de tickets. Se usa para el QR de cotejo <b>VeriFactu</b>
/// en el ticket, sin depender de que la impresora tenga soporte nativo de QR.
/// </summary>
internal static class QrEscPos
{
    /// <summary>
    /// Devuelve el comando ESC/POS <c>GS v 0</c> (imagen ráster) con el QR del contenido dado. Cada
    /// módulo del QR se amplía <paramref name="escala"/> puntos. La matriz incluye la zona de silencio.
    /// </summary>
    public static byte[] Raster(string contenido, int escala = 5)
    {
        ArgumentException.ThrowIfNullOrEmpty(contenido);

        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        var matriz = datos.ModuleMatrix;          // filas [y][x]; true = módulo oscuro
        var modulos = matriz.Count;
        var alto = modulos * escala;
        var anchoBytes = (modulos * escala + 7) / 8;

        var salida = new List<byte>(8 + (anchoBytes * alto))
        {
            0x1D, 0x76, 0x30, 0x00,                 // GS v 0, modo normal
            (byte)(anchoBytes & 0xFF), (byte)(anchoBytes >> 8),   // bytes por fila (xL xH)
            (byte)(alto & 0xFF), (byte)(alto >> 8),               // número de filas (yL yH)
        };

        for (var y = 0; y < alto; y++)
        {
            var fila = matriz[y / escala];
            for (var b = 0; b < anchoBytes; b++)
            {
                byte valor = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    var mx = ((b * 8) + bit) / escala;
                    if (mx < fila.Count && fila[mx])
                    {
                        valor |= (byte)(0x80 >> bit);
                    }
                }

                salida.Add(valor);
            }
        }

        return salida.ToArray();
    }
}

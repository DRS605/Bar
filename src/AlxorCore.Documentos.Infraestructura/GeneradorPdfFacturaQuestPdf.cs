using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Genera el PDF de una factura con QuestPDF. Diseño limpio y sobrio (español).</summary>
internal sealed class GeneradorPdfFacturaQuestPdf : IGeneradorPdfFactura
{
    public byte[] Generar(FacturaDto factura, EmpresaDto emisor)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(emisor);

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.DefaultTextStyle(x => x.FontSize(10));

                pagina.Header().Row(fila =>
                {
                    fila.RelativeItem().Column(col =>
                    {
                        col.Item().Text(emisor.RazonSocial).Bold().FontSize(16);
                        col.Item().Text($"NIF: {emisor.Nif}");
                    });
                    fila.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().Text("FACTURA").Bold().FontSize(16);
                        col.Item().Text(factura.NumeroCompleto);
                        col.Item().Text($"Fecha: {factura.FechaEmision:dd/MM/yyyy}");
                    });
                });

                pagina.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().PaddingBottom(10).Column(cliente =>
                    {
                        cliente.Item().Text("Cliente").Bold();
                        cliente.Item().Text(factura.ClienteNombre);
                        if (!string.IsNullOrWhiteSpace(factura.ClienteNif))
                        {
                            cliente.Item().Text($"NIF: {factura.ClienteNif}");
                        }
                    });

                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(4);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });

                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Descripción").Bold();
                            encabezado.Cell().AlignRight().Text("Cantidad").Bold();
                            encabezado.Cell().AlignRight().Text("Precio").Bold();
                            encabezado.Cell().AlignRight().Text("IVA").Bold();
                            encabezado.Cell().AlignRight().Text("Base").Bold();
                        });

                        foreach (var linea in factura.Lineas)
                        {
                            tabla.Cell().Text(linea.Descripcion);
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.Cantidad));
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.PrecioUnitario));
                            tabla.Cell().AlignRight().Text($"{linea.PorcentajeIva:0}%");
                            tabla.Cell().AlignRight().Text(Redondeo.Formatear(linea.Base));
                        }
                    });

                    col.Item().AlignRight().PaddingTop(15).Column(totales =>
                    {
                        totales.Item().Text($"Base imponible: {Redondeo.Formatear(factura.BaseImponible)} €");
                        totales.Item().Text($"IVA: {Redondeo.Formatear(factura.CuotaIva)} €");
                        if (factura.RetencionIrpf > 0)
                        {
                            totales.Item().Text($"Retención IRPF ({factura.PorcentajeIrpf:0}%): -{Redondeo.Formatear(factura.RetencionIrpf)} €");
                        }

                        totales.Item().Text($"TOTAL: {Redondeo.Formatear(factura.Total)} €").Bold().FontSize(13);
                    });
                });

                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span("ALXOR Core · ").FontColor(Colors.Grey.Medium);
                    texto.Span(emisor.RazonSocial).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return documento.GeneratePdf();
    }
}

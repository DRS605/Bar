using AlxorCore.Documentos.Aplicacion;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Composición del módulo Documentos (PDF y correo). No tiene persistencia propia.</summary>
public static class RegistroServicios
{
    public static IServiceCollection AgregarModuloDocumentos(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        // Licencia Community de QuestPDF (gratuita para facturación de pequeño volumen).
        QuestPDF.Settings.License = LicenseType.Community;

        servicios.AddScoped<IGeneradorPdfFactura, GeneradorPdfFacturaQuestPdf>();
        servicios.AddScoped<IServicioCorreo, ServicioCorreoStub>();
        servicios.AddScoped<GenerarPdfFactura>();
        servicios.AddScoped<EnviarFacturaPorEmail>();

        return servicios;
    }
}

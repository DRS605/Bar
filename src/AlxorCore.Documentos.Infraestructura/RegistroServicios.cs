using AlxorCore.Documentos.Aplicacion;
using AlxorCore.Documentos.Infraestructura.Correo;
using AlxorCore.Documentos.Infraestructura.Impresion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace AlxorCore.Documentos.Infraestructura;

/// <summary>Composición del módulo Documentos (PDF y correo). No tiene persistencia propia.</summary>
public static class RegistroServicios
{
    public static IServiceCollection AgregarModuloDocumentos(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        // Licencia Community de QuestPDF (gratuita para facturación de pequeño volumen).
        QuestPDF.Settings.License = LicenseType.Community;

        servicios.AddScoped<IGeneradorPdfFactura, GeneradorPdfFacturaQuestPdf>();
        servicios.AddScoped<IGeneradorPdfPresupuesto, GeneradorPdfPresupuestoQuestPdf>();

        // Correo: SMTP real si está configurado (sección «Correo», compartida con Identidad); si no, el
        // stub, que registra el envío en el log. El puerto IServicioCorreo no cambia en ningún caso.
        servicios.AddOptions<OpcionesCorreo>().Bind(configuracion.GetSection(OpcionesCorreo.Seccion));
        var opcionesCorreo = new OpcionesCorreo();
        configuracion.GetSection(OpcionesCorreo.Seccion).Bind(opcionesCorreo);
        if (opcionesCorreo.Configurado)
        {
            servicios.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
        }
        else
        {
            servicios.AddScoped<IServicioCorreo, ServicioCorreoStub>();
        }

        // Impresión de tickets: generador ESC/POS y la impresora (de red si hay host en la sección
        // «Impresora»; si no, la nula, que informa de que no está configurada).
        servicios.AddScoped<IGeneradorTicketEscPos, GeneradorTicketEscPos>();
        servicios.AddScoped<IGeneradorComandaCocina, GeneradorComandaCocinaEscPos>();
        servicios.AddOptions<OpcionesImpresora>().Bind(configuracion.GetSection(OpcionesImpresora.Seccion));
        var opcionesImpresora = new OpcionesImpresora();
        configuracion.GetSection(OpcionesImpresora.Seccion).Bind(opcionesImpresora);
        if (opcionesImpresora.Configurada)
        {
            servicios.AddScoped<IImpresoraTickets, ImpresoraTicketsRed>();
        }
        else
        {
            servicios.AddScoped<IImpresoraTickets, ImpresoraTicketsNula>();
        }

        servicios.AddScoped<GenerarPdfFactura>();
        servicios.AddScoped<EnviarFacturaPorEmail>();
        servicios.AddScoped<GenerarPdfPresupuesto>();
        servicios.AddScoped<EnviarPresupuestoPorEmail>();
        servicios.AddScoped<ObtenerTicketEscPos>();
        servicios.AddScoped<ImprimirTicket>();

        return servicios;
    }
}

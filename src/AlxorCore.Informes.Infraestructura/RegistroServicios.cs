using AlxorCore.Informes.Aplicacion;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Informes.Infraestructura;

/// <summary>Composición del módulo Informes (solo casos de uso de lectura).</summary>
public static class RegistroServicios
{
    public static IServiceCollection AgregarModuloInformes(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<ObtenerDashboard>();
        servicios.AddScoped<GenerarLibroIva>();
        servicios.AddScoped<GenerarResumenesFiscales>();
        servicios.AddScoped<GenerarBeneficio>();
        servicios.AddScoped<GenerarCierreCaja>();

        return servicios;
    }
}

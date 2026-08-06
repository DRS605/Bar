using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Facturacion.Infraestructura;

/// <summary>Composición del módulo Facturación.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloFacturacion(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<FacturacionDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", FacturacionDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoFacturacion>(sp => sp.GetRequiredService<FacturacionDbContext>());
        servicios.AddScoped<RepositorioFacturas>();
        servicios.AddScoped<IRepositorioFacturas>(sp => sp.GetRequiredService<RepositorioFacturas>());
        servicios.AddScoped<IConsultaFacturas>(sp => sp.GetRequiredService<RepositorioFacturas>());

        servicios.AddScoped<EmitirFactura>();
        servicios.AddScoped<EmitirTicket>();
        servicios.AddScoped<EmitirRectificativa>();
        servicios.AddScoped<ListarFacturas>();
        servicios.AddScoped<ObtenerFactura>();

        // Facturación automática periódica.
        servicios.AddScoped<RepositorioFacturasRecurrentes>();
        servicios.AddScoped<IRepositorioFacturasRecurrentes>(sp => sp.GetRequiredService<RepositorioFacturasRecurrentes>());
        servicios.AddScoped<IConsultaFacturasRecurrentes>(sp => sp.GetRequiredService<RepositorioFacturasRecurrentes>());
        servicios.AddScoped<CrearFacturaRecurrente>();
        servicios.AddScoped<ActualizarFacturaRecurrente>();
        servicios.AddScoped<CambiarEstadoFacturaRecurrente>();
        servicios.AddScoped<ListarFacturasRecurrentes>();
        servicios.AddScoped<ObtenerFacturaRecurrente>();
        servicios.AddScoped<EmitirFacturasRecurrentesVencidas>();

        servicios.AddScoped<RepositorioPresupuestos>();
        servicios.AddScoped<IRepositorioPresupuestos>(sp => sp.GetRequiredService<RepositorioPresupuestos>());
        servicios.AddScoped<IConsultaPresupuestos>(sp => sp.GetRequiredService<RepositorioPresupuestos>());
        servicios.AddScoped<CrearPresupuesto>();
        servicios.AddScoped<ActualizarPresupuesto>();
        servicios.AddScoped<ListarPresupuestos>();
        servicios.AddScoped<ObtenerPresupuesto>();
        servicios.AddScoped<AceptarPresupuesto>();
        servicios.AddScoped<RechazarPresupuesto>();

        return servicios;
    }
}

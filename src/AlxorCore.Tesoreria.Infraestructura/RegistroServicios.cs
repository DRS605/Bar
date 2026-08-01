using AlxorCore.Persistencia;
using AlxorCore.Tesoreria.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Tesoreria.Infraestructura;

/// <summary>Composición del módulo Tesorería.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloTesoreria(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<TesoreriaDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", TesoreriaDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoTesoreria>(sp => sp.GetRequiredService<TesoreriaDbContext>());
        servicios.AddScoped<IRepositorioMovimientos, RepositorioMovimientos>();

        servicios.AddScoped<RegistrarCobro>();
        servicios.AddScoped<RegistrarPago>();
        servicios.AddScoped<ConsultarSaldo>();

        return servicios;
    }
}

using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Gastos.Infraestructura;

/// <summary>Composición del módulo Gastos.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloGastos(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<GastosDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", GastosDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoGastos>(sp => sp.GetRequiredService<GastosDbContext>());
        servicios.AddScoped<RepositorioGastos>();
        servicios.AddScoped<IRepositorioGastos>(sp => sp.GetRequiredService<RepositorioGastos>());
        servicios.AddScoped<IConsultaGastos>(sp => sp.GetRequiredService<RepositorioGastos>());

        servicios.AddScoped<RegistrarGasto>();
        servicios.AddScoped<ListarGastos>();
        servicios.AddScoped<ObtenerGasto>();

        return servicios;
    }
}

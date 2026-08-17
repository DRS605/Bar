using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Hosteleria.Infraestructura;

/// <summary>Composición del módulo Hostelería.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloHosteleria(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<HosteleriaDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", HosteleriaDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoHosteleria>(sp => sp.GetRequiredService<HosteleriaDbContext>());

        servicios.AddScoped<RepositorioMesas>();
        servicios.AddScoped<IRepositorioMesas>(sp => sp.GetRequiredService<RepositorioMesas>());
        servicios.AddScoped<IConsultaMesas>(sp => sp.GetRequiredService<RepositorioMesas>());
        servicios.AddScoped<RepositorioComandas>();
        servicios.AddScoped<IRepositorioComandas>(sp => sp.GetRequiredService<RepositorioComandas>());
        servicios.AddScoped<IConsultaComandas>(sp => sp.GetRequiredService<RepositorioComandas>());

        servicios.AddScoped<CrearMesa>();
        servicios.AddScoped<ActualizarMesa>();
        servicios.AddScoped<MoverMesa>();
        servicios.AddScoped<DesactivarMesa>();
        servicios.AddScoped<ListarMesas>();
        servicios.AddScoped<AbrirComanda>();
        servicios.AddScoped<AgregarLineaComanda>();
        servicios.AddScoped<FijarCantidadLineaComanda>();
        servicios.AddScoped<QuitarLineaComanda>();
        servicios.AddScoped<EnviarComandaCocina>();
        servicios.AddScoped<ListarComandasAbiertas>();
        servicios.AddScoped<ObtenerComanda>();
        servicios.AddScoped<AnularComanda>();
        servicios.AddScoped<CobrarComanda>();

        return servicios;
    }
}

using AlxorCore.Persistencia;
using AlxorCore.Reservas.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Reservas.Infraestructura;

/// <summary>Composición del módulo Reservas.</summary>
public static class RegistroServicios
{
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloReservas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{CadenaConexion}».");

        servicios.AddScoped<InterceptorEmpresa>();
        servicios.AddDbContext<ReservasDbContext>((sp, opciones) =>
            opciones
                .UseNpgsql(conexion, npgsql =>
                    npgsql.MigrationsHistoryTable("__historial_migraciones", ReservasDbContext.Esquema))
                .AddInterceptors(sp.GetRequiredService<InterceptorEmpresa>()));

        servicios.AddScoped<IUnidadDeTrabajoReservas>(sp => sp.GetRequiredService<ReservasDbContext>());

        servicios.AddScoped<RepositorioReservas>();
        servicios.AddScoped<IRepositorioReservas>(sp => sp.GetRequiredService<RepositorioReservas>());
        servicios.AddScoped<IConsultaReservas>(sp => sp.GetRequiredService<RepositorioReservas>());
        servicios.AddScoped<IRepositorioAgenda, RepositorioAgenda>();
        servicios.AddScoped<IRepositorioTurnos, RepositorioTurnos>();
        servicios.AddScoped<INotificadorReservas, NotificadorReservas>();

        servicios.AddScoped<CrearReserva>();
        servicios.AddScoped<ActualizarReserva>();
        servicios.AddScoped<CambiarEstadoReserva>();
        servicios.AddScoped<SentarReserva>();
        servicios.AddScoped<ListarReservas>();
        servicios.AddScoped<ObtenerReserva>();
        servicios.AddScoped<ObtenerAgenda>();
        servicios.AddScoped<FeedCalendario>();
        servicios.AddScoped<EnviarRecordatoriosReservas>();
        servicios.AddScoped<CrearTurno>();
        servicios.AddScoped<ActualizarTurno>();
        servicios.AddScoped<DesactivarTurno>();
        servicios.AddScoped<ListarTurnos>();
        servicios.AddScoped<ObtenerDisponibilidad>();

        return servicios;
    }
}

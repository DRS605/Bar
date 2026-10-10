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
        servicios.AddScoped<IConsultaCocina>(sp => sp.GetRequiredService<RepositorioComandas>());
        servicios.AddScoped<IRepositorioZonasProducto, RepositorioZonasProducto>();
        servicios.AddScoped<RepositorioPedidosWeb>();
        servicios.AddScoped<IRepositorioPedidosWeb>(sp => sp.GetRequiredService<RepositorioPedidosWeb>());
        servicios.AddScoped<IConsultaPedidosWeb>(sp => sp.GetRequiredService<RepositorioPedidosWeb>());
        servicios.AddScoped<RepositorioAvisos>();
        servicios.AddScoped<IRepositorioAvisos>(sp => sp.GetRequiredService<RepositorioAvisos>());
        servicios.AddScoped<IConsultaAvisos>(sp => sp.GetRequiredService<RepositorioAvisos>());
        servicios.AddScoped<RepositorioTraducciones>();
        servicios.AddScoped<IRepositorioTraducciones>(sp => sp.GetRequiredService<RepositorioTraducciones>());
        servicios.AddScoped<IConsultaTraducciones>(sp => sp.GetRequiredService<RepositorioTraducciones>());
        servicios.AddScoped<RepositorioFichasCarta>();
        servicios.AddScoped<IRepositorioFichasCarta>(sp => sp.GetRequiredService<RepositorioFichasCarta>());
        servicios.AddScoped<IConsultaFichasCarta>(sp => sp.GetRequiredService<RepositorioFichasCarta>());
        servicios.AddScoped<IRepositorioConfiguracionCarta, RepositorioConfiguracionCarta>();
        servicios.AddScoped<RepositorioSuscripcion>();
        servicios.AddScoped<IRepositorioSuscripcion>(sp => sp.GetRequiredService<RepositorioSuscripcion>());
        servicios.AddScoped<IConsultaPlanBar>(sp => sp.GetRequiredService<RepositorioSuscripcion>());
        servicios.AddScoped<IRepositorioMenuDia, RepositorioMenuDia>();
        servicios.AddScoped<IRepositorioMovimientosCaja, RepositorioMovimientosCaja>();
        servicios.AddScoped<IRepositorioGruposOpcion, RepositorioGruposOpcion>();
        servicios.AddScoped<IRepositorioPromociones, RepositorioPromociones>();

        servicios.AddScoped<CrearMesa>();
        servicios.AddScoped<ActualizarMesa>();
        servicios.AddScoped<MoverMesa>();
        servicios.AddScoped<DesactivarMesa>();
        servicios.AddScoped<ListarMesas>();
        servicios.AddScoped<AbrirComanda>();
        servicios.AddScoped<AgregarLineaComanda>();
        servicios.AddScoped<FijarCantidadLineaComanda>();
        servicios.AddScoped<CambiarPrecioLineaComanda>();
        servicios.AddScoped<CambiarNotaLineaComanda>();
        servicios.AddScoped<QuitarLineaComanda>();
        servicios.AddScoped<EnviarComandaCocina>();
        servicios.AddScoped<ListarComandasAbiertas>();
        servicios.AddScoped<VentasPorCamarero>();
        servicios.AddScoped<ObtenerComanda>();
        servicios.AddScoped<AnularComanda>();
        servicios.AddScoped<CobrarComanda>();
        servicios.AddScoped<CobrarComandaParcial>();
        servicios.AddScoped<MoverComanda>();
        servicios.AddScoped<JuntarComandas>();

        // Autopedido por QR (carta interactiva, pedidos del cliente y avisos de mesa).
        servicios.AddScoped<CrearPedidoWeb>();
        servicios.AddScoped<ListarPedidosWebPendientes>();
        servicios.AddScoped<AceptarPedidoWeb>();
        servicios.AddScoped<RechazarPedidoWeb>();
        servicios.AddScoped<CrearAvisoMesa>();
        servicios.AddScoped<ListarAvisosPendientes>();
        servicios.AddScoped<AtenderAviso>();
        servicios.AddScoped<ListarTraducciones>();
        servicios.AddScoped<GuardarTraduccion>();
        servicios.AddScoped<ListarFichasCarta>();
        servicios.AddScoped<GuardarFichaCarta>();
        servicios.AddScoped<CambiarDisponibilidad>();
        servicios.AddScoped<ObtenerConfiguracionCarta>();
        servicios.AddScoped<GuardarConfiguracionCarta>();
        servicios.AddScoped<ObtenerFotoProducto>();
        servicios.AddScoped<RegenerarTokenCartaMesa>();

        // Planes / suscripción (Essential vs Pro).
        servicios.AddScoped<ObtenerSuscripcion>();
        servicios.AddScoped<CambiarPlanBar>();

        // Menú del día.
        servicios.AddScoped<ObtenerMenuDia>();
        servicios.AddScoped<GuardarMenuDia>();

        // Caja: movimientos de efectivo y arqueo.
        servicios.AddScoped<RegistrarMovimientoCaja>();
        servicios.AddScoped<QuitarMovimientoCaja>();
        servicios.AddScoped<ListarMovimientosCaja>();

        // Opciones de producto (formatos/extras).
        servicios.AddScoped<ObtenerOpcionesProducto>();
        servicios.AddScoped<ListarOpcionesEmpresa>();
        servicios.AddScoped<GuardarOpcionesProducto>();

        // Promociones.
        servicios.AddScoped<ListarPromociones>();
        servicios.AddScoped<CrearPromocion>();
        servicios.AddScoped<CambiarActivaPromocion>();
        servicios.AddScoped<EliminarPromocion>();

        // Zonas de preparación y pantalla de cocina (KDS).
        servicios.AddScoped<ObtenerZonasEmpresa>();
        servicios.AddScoped<GuardarZonaProducto>();
        servicios.AddScoped<ListarPendientesCocina>();
        servicios.AddScoped<ServirItemCocina>();

        return servicios;
    }
}

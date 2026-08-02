using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Infraestructura.Correo;
using AlxorCore.Identidad.Infraestructura.Eventos;
using AlxorCore.Identidad.Infraestructura.Persistencia;
using AlxorCore.Identidad.Infraestructura.Seguridad;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Seguridad;
using AlxorCore.Nucleo.Tiempo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlxorCore.Identidad.Infraestructura;

/// <summary>
/// Composición del módulo Identidad: registra persistencia, seguridad, puertos y casos de uso.
/// Es el único punto por el que el host (la API) conecta con el módulo.
/// </summary>
public static class RegistroServicios
{
    /// <summary>Nombre de la cadena de conexión esperada en la configuración.</summary>
    public const string CadenaConexion = "AlxorCore";

    public static IServiceCollection AgregarModuloIdentidad(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var conexion = configuracion.GetConnectionString(CadenaConexion)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión «{CadenaConexion}» en la configuración.");

        servicios.AddDbContext<IdentidadDbContext>(opciones =>
            opciones.UseNpgsql(conexion, npgsql =>
                npgsql.MigrationsHistoryTable("__historial_migraciones", IdentidadDbContext.Esquema)));

        // Unidad de trabajo respaldada por el DbContext del módulo.
        servicios.AddScoped<IUnidadDeTrabajoIdentidad>(sp => sp.GetRequiredService<IdentidadDbContext>());
        servicios.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        servicios.AddScoped<IConsultaUsuarios, ConsultaUsuarios>();
        servicios.AddScoped<IPublicadorEventos, PublicadorEventosRegistro>();
        servicios.AddScoped<IServicioVerificacionEmail, ServicioVerificacionEmailStub>();

        // Seguridad.
        servicios.AddOptions<OpcionesJwt>()
            .Bind(configuracion.GetSection(OpcionesJwt.Seccion))
            .ValidateDataAnnotations();
        servicios.AddSingleton<IHasherContrasena, HasherContrasenaIdentity>();
        servicios.AddScoped<IProveedorTokens, ProveedorTokensJwt>();

        // Reloj del sistema (determinista solo en tests, donde se sustituye).
        servicios.AddSingleton<IReloj, RelojSistema>();

        // Casos de uso.
        servicios.AddScoped<RegistrarUsuario>();
        servicios.AddScoped<IniciarSesion>();
        servicios.AddScoped<ObtenerPerfil>();
        servicios.AddScoped<VerificarEmail>();
        servicios.AddScoped<RecuperarContrasena>();
        servicios.AddScoped<RestablecerContrasena>();
        servicios.AddScoped<CrearUsuarioInvitado>();

        return servicios;
    }
}

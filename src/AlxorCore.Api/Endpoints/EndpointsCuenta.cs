using AlxorCore.Api.Comun;
using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Autorizacion;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Api.Endpoints;

/// <summary>Endpoints de la cuenta/empresa: derechos RGPD (portabilidad y supresión).</summary>
public static class EndpointsCuenta
{
    private static readonly System.Text.Json.JsonSerializerOptions OpcionesExport = CrearOpciones();

    private static System.Text.Json.JsonSerializerOptions CrearOpciones()
    {
        var opciones = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        opciones.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return opciones;
    }

    public static IEndpointRouteBuilder MapearCuenta(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var cuenta = rutas.MapGroup("/cuenta").WithTags("Cuenta / RGPD");

        cuenta.MapGet("/exportar", ExportarAsync)
            .WithSummary("Exporta todos los datos de la empresa activa (RGPD: acceso y portabilidad).")
            .RequierePermiso(Permisos.DatosExportar);

        return rutas;
    }

    private static async Task<IResult> ExportarAsync(
        IContextoEmpresa contexto,
        IConsultaEmpresas empresas,
        IConsultaClientes clientes,
        IConsultaProveedores proveedores,
        IConsultaProductos productos,
        IConsultaFacturas facturas,
        IConsultaGastos gastos,
        CancellationToken ct)
    {
        if (contexto.EmpresaId is null)
        {
            return ResultadosHttp.AProblema(Error.Validacion("empresa.no_seleccionada", "Selecciona una empresa primero."));
        }

        var id = contexto.EmpresaId.Value;
        var datos = new
        {
            generadoEn = DateTimeOffset.UtcNow,
            empresa = await empresas.ObtenerAsync(id, ct).ConfigureAwait(false),
            clientes = await clientes.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            proveedores = await proveedores.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            productos = await productos.ListarAsync(id, incluirInactivos: true, ct).ConfigureAwait(false),
            facturas = await facturas.ListarAsync(id, ct).ConfigureAwait(false),
            gastos = await gastos.ListarAsync(id, ct).ConfigureAwait(false),
        };

        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(datos, OpcionesExport);
        var nombre = $"alxor-export-{DateTime.UtcNow:yyyyMMdd}.json";
        return Results.File(bytes, "application/json", nombre);
    }
}

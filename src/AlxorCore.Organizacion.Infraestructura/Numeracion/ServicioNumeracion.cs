using System.Data;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;
using AlxorCore.Organizacion.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Organizacion.Infraestructura.Numeracion;

/// <summary>
/// Servicio de numeración correlativa. Usa un <c>UPDATE ... RETURNING</c> atómico (con bloqueo de
/// fila) para asignar el siguiente número sin duplicados ni condiciones de carrera, y crea la serie
/// por defecto de forma perezosa si aún no existe (así cada nuevo ejercicio obtiene su serie).
/// </summary>
internal sealed class ServicioNumeracion : IServicioNumeracion
{
    private readonly OrganizacionDbContext _contexto;
    private readonly IReloj _reloj;

    public ServicioNumeracion(OrganizacionDbContext contexto, IReloj reloj)
    {
        _contexto = contexto;
        _reloj = reloj;
    }

    public async Task<Resultado<NumeroDocumento>> SiguienteAsync(
        Guid empresaId,
        TipoDocumento tipoDocumento,
        int ejercicio,
        CancellationToken ct = default)
    {
        var prefijo = SerieNumeracion.PrefijoFacturaPorDefecto;
        var tipoTexto = tipoDocumento.ToString();

        var conexion = _contexto.Database.GetDbConnection();
        var estabaAbierta = conexion.State == ConnectionState.Open;
        if (!estabaAbierta)
        {
            await conexion.OpenAsync(ct).ConfigureAwait(false);
        }

        try
        {
            // 1) Crea la serie si no existe (idempotente ante concurrencia).
            await using (var insertar = conexion.CreateCommand())
            {
                insertar.CommandText =
                    """
                    INSERT INTO organizacion.serie_numeracion
                        (id, empresa_id, tipo_documento, ejercicio, prefijo, siguiente_numero, creado_en)
                    VALUES (@id, @empresa, @tipo, @ejercicio, @prefijo, 1, @creado)
                    ON CONFLICT (empresa_id, tipo_documento, ejercicio, prefijo) DO NOTHING
                    """;
                Agregar(insertar, "id", Guid.NewGuid());
                Agregar(insertar, "empresa", empresaId);
                Agregar(insertar, "tipo", tipoTexto);
                Agregar(insertar, "ejercicio", ejercicio);
                Agregar(insertar, "prefijo", prefijo);
                Agregar(insertar, "creado", _reloj.AhoraUtc);
                await insertar.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // 2) Asigna el siguiente número de forma atómica.
            await using (var actualizar = conexion.CreateCommand())
            {
                actualizar.CommandText =
                    """
                    UPDATE organizacion.serie_numeracion
                    SET siguiente_numero = siguiente_numero + 1
                    WHERE empresa_id = @empresa AND tipo_documento = @tipo AND ejercicio = @ejercicio AND prefijo = @prefijo
                    RETURNING siguiente_numero - 1
                    """;
                Agregar(actualizar, "empresa", empresaId);
                Agregar(actualizar, "tipo", tipoTexto);
                Agregar(actualizar, "ejercicio", ejercicio);
                Agregar(actualizar, "prefijo", prefijo);

                var resultado = await actualizar.ExecuteScalarAsync(ct).ConfigureAwait(false);
                var numero = Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
                return Resultado.Ok(new NumeroDocumento(prefijo, ejercicio, numero));
            }
        }
        finally
        {
            if (!estabaAbierta)
            {
                await conexion.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static void Agregar(System.Data.Common.DbCommand comando, string nombre, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}

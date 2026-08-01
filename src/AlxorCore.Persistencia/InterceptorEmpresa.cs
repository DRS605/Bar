using System.Data.Common;
using AlxorCore.Nucleo.Multiempresa;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AlxorCore.Persistencia;

/// <summary>
/// Interceptor que fija el parámetro de sesión <c>app.empresa_actual</c> al abrir la conexión,
/// para que las políticas de Row-Level Security de PostgreSQL filtren por la empresa activa.
/// Es la segunda barrera de aislamiento (la primera es el filtro global de EF Core).
/// </summary>
public sealed class InterceptorEmpresa : DbConnectionInterceptor
{
    private readonly IContextoEmpresa _contextoEmpresa;

    public InterceptorEmpresa(IContextoEmpresa contextoEmpresa) => _contextoEmpresa = contextoEmpresa;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        EstablecerEmpresa(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await EstablecerEmpresaAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private void EstablecerEmpresa(DbConnection connection)
    {
        using var comando = CrearComando(connection);
        comando.ExecuteNonQuery();
    }

    private async Task EstablecerEmpresaAsync(DbConnection connection, CancellationToken ct)
    {
        await using var comando = CrearComando(connection);
        await comando.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private DbCommand CrearComando(DbConnection connection)
    {
        var comando = connection.CreateCommand();
        comando.CommandText = "SELECT set_config('app.empresa_actual', @empresa, false)";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "empresa";
        parametro.Value = _contextoEmpresa.EmpresaId?.ToString() ?? string.Empty;
        comando.Parameters.Add(parametro);
        return comando;
    }
}

namespace AlxorCore.Persistencia;

/// <summary>
/// Genera el SQL para activar Row-Level Security por empresa en una tabla. Se usa desde las
/// migraciones. La política compara <c>empresa_id</c> con el parámetro de sesión
/// <c>app.empresa_actual</c> (fijado por <see cref="InterceptorEmpresa"/>).
/// </summary>
/// <remarks>
/// La RLS solo se aplica si la aplicación se conecta con un rol <b>sin</b> privilegios de
/// superusuario y sin BYPASSRLS. En desarrollo/tests se usa el rol <c>postgres</c> (superusuario),
/// que la ignora; el aislamiento queda garantizado por el filtro global de EF Core. En producción
/// debe usarse un rol de aplicación restringido para que esta segunda barrera sea efectiva.
/// </remarks>
public static class RlsSql
{
    /// <summary>SQL que activa (y fuerza) la RLS y crea la política de aislamiento por empresa.</summary>
    public static string Activar(string esquema, string tabla)
    {
        var cualificada = $"\"{esquema}\".\"{tabla}\"";
        var nombrePolitica = $"pol_empresa_{tabla}";

        return $"""
            ALTER TABLE {cualificada} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {cualificada} FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS "{nombrePolitica}" ON {cualificada};
            CREATE POLICY "{nombrePolitica}" ON {cualificada}
                USING (empresa_id = NULLIF(current_setting('app.empresa_actual', true), '')::uuid)
                WITH CHECK (empresa_id = NULLIF(current_setting('app.empresa_actual', true), '')::uuid);
            """;
    }

    /// <summary>SQL que desactiva la RLS de la tabla (para revertir la migración).</summary>
    public static string Desactivar(string esquema, string tabla)
    {
        var cualificada = $"\"{esquema}\".\"{tabla}\"";
        var nombrePolitica = $"pol_empresa_{tabla}";

        return $"""
            DROP POLICY IF EXISTS "{nombrePolitica}" ON {cualificada};
            ALTER TABLE {cualificada} DISABLE ROW LEVEL SECURITY;
            """;
    }
}

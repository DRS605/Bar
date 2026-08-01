using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.Puertos;

/// <summary>Repositorio de empresas (tenants).</summary>
public interface IRepositorioEmpresas
{
    Task<Empresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExisteNifAsync(string nif, CancellationToken ct = default);

    void Agregar(Empresa empresa);
}

/// <summary>Repositorio de membresías.</summary>
public interface IRepositorioMembresias
{
    Task<Membresia?> ObtenerAsync(Guid usuarioId, Guid empresaId, CancellationToken ct = default);

    void Agregar(Membresia membresia);
}

/// <summary>Repositorio de series de numeración.</summary>
public interface IRepositorioSeries
{
    void Agregar(SerieNumeracion serie);

    Task<IReadOnlyList<SerieNumeracion>> ListarAsync(Guid empresaId, CancellationToken ct = default);

    Task<bool> ExisteAsync(Guid empresaId, TipoDocumento tipo, int ejercicio, string prefijo, CancellationToken ct = default);
}

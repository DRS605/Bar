using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Auditoria;

/// <summary>
/// Registro de auditoría: deja constancia de <b>quién</b> hizo <b>qué</b> y <b>cuándo</b>. Es
/// inmutable (solo se añaden filas) y pertenece a una empresa (RLS). Registra las operaciones que
/// modifican datos (altas, cambios, bajas) a nivel de petición HTTP.
/// </summary>
public sealed class RegistroAuditoria : RaizAgregadoEmpresa<Guid>
{
    private RegistroAuditoria(Guid id)
        : base(id, Guid.Empty)
    {
        UsuarioNombre = null!;
        Accion = null!;
        Metodo = null!;
        Ruta = null!;
    }

    private RegistroAuditoria(
        Guid id, Guid empresaId, Guid? usuarioId, string usuarioNombre, string accion, string metodo, string ruta, int codigoEstado, DateTimeOffset ocurridoEn)
        : base(id, empresaId)
    {
        UsuarioId = usuarioId;
        UsuarioNombre = usuarioNombre;
        Accion = accion;
        Metodo = metodo;
        Ruta = ruta;
        CodigoEstado = codigoEstado;
        OcurridoEn = ocurridoEn;
    }

    public Guid? UsuarioId { get; private set; }

    public string UsuarioNombre { get; private set; }

    /// <summary>Acción legible (p. ej. «Alta en facturas»).</summary>
    public string Accion { get; private set; }

    public string Metodo { get; private set; }

    public string Ruta { get; private set; }

    public int CodigoEstado { get; private set; }

    public DateTimeOffset OcurridoEn { get; private set; }

    public static RegistroAuditoria Crear(
        Guid empresaId, Guid? usuarioId, string usuarioNombre, string accion, string metodo, string ruta, int codigoEstado, DateTimeOffset ocurridoEn) =>
        new(Guid.NewGuid(), empresaId, usuarioId, usuarioNombre, accion, metodo, ruta, codigoEstado, ocurridoEn);
}

/// <summary>Vista de un registro de auditoría.</summary>
public sealed record RegistroAuditoriaDto(
    Guid Id, Guid? UsuarioId, string UsuarioNombre, string Accion, string Metodo, string Ruta, int CodigoEstado, DateTimeOffset OcurridoEn)
{
    public static RegistroAuditoriaDto Desde(RegistroAuditoria r) =>
        new(r.Id, r.UsuarioId, r.UsuarioNombre, r.Accion, r.Metodo, r.Ruta, r.CodigoEstado, r.OcurridoEn);
}

/// <summary>Escritura del registro de auditoría (append-only).</summary>
public interface IRepositorioAuditoria
{
    /// <summary>Añade y persiste un registro de auditoría.</summary>
    Task RegistrarAsync(RegistroAuditoria registro, CancellationToken ct = default);
}

/// <summary>Consulta del registro de auditoría de la empresa activa.</summary>
public interface IConsultaAuditoria
{
    Task<IReadOnlyList<RegistroAuditoriaDto>> RecientesAsync(Guid empresaId, int limite = 100, CancellationToken ct = default);
}

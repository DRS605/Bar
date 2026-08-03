using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Auditoria.Infraestructura;

/// <summary>Contexto de persistencia del módulo Auditoría.</summary>
public sealed class AuditoriaDbContext : DbContextEmpresaBase
{
    public AuditoriaDbContext(DbContextOptions<AuditoriaDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "auditoria";

    public DbSet<RegistroAuditoria> Registros => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditoriaDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionRegistroAuditoria : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("registro_auditoria");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(r => r.UsuarioId).HasColumnName("usuario_id");
        builder.Property(r => r.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Accion).HasColumnName("accion").HasMaxLength(120).IsRequired();
        builder.Property(r => r.Metodo).HasColumnName("metodo").HasMaxLength(10).IsRequired();
        builder.Property(r => r.Ruta).HasColumnName("ruta").HasMaxLength(300).IsRequired();
        builder.Property(r => r.CodigoEstado).HasColumnName("codigo_estado").IsRequired();
        builder.Property(r => r.OcurridoEn).HasColumnName("ocurrido_en").IsRequired();

        builder.HasIndex(r => new { r.EmpresaId, r.OcurridoEn }).HasDatabaseName("ix_auditoria_empresa_fecha");
        builder.Ignore(r => r.EventosDominio);
    }
}

internal sealed class RepositorioAuditoria : IRepositorioAuditoria, IConsultaAuditoria
{
    private readonly AuditoriaDbContext _contexto;

    public RepositorioAuditoria(AuditoriaDbContext contexto) => _contexto = contexto;

    public async Task RegistrarAsync(RegistroAuditoria registro, CancellationToken ct = default)
    {
        _contexto.Registros.Add(registro);
        await _contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RegistroAuditoriaDto>> RecientesAsync(Guid empresaId, int limite = 100, CancellationToken ct = default)
    {
        var registros = await _contexto.Registros
            .Where(r => r.EmpresaId == empresaId)
            .OrderByDescending(r => r.OcurridoEn)
            .Take(limite)
            .ToListAsync(ct).ConfigureAwait(false);
        return registros.Select(RegistroAuditoriaDto.Desde).ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class AuditoriaDbContextFactory : IDesignTimeDbContextFactory<AuditoriaDbContext>
{
    public AuditoriaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<AuditoriaDbContext>().UseNpgsql(conexion).Options;
        return new AuditoriaDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
    }

    private sealed class PublicadorInactivo : IPublicadorEventos
    {
        public Task PublicarAsync(IReadOnlyCollection<IEventoDominio> eventos, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ContextoVacio : IContextoEmpresa
    {
        public Guid? EmpresaId => null;
    }
}

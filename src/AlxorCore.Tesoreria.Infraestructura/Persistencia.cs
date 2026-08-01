using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using AlxorCore.Tesoreria.Aplicacion;
using AlxorCore.Tesoreria.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Tesoreria.Infraestructura;

/// <summary>Contexto de persistencia del módulo Tesorería.</summary>
public sealed class TesoreriaDbContext : DbContextEmpresaBase, IUnidadDeTrabajoTesoreria
{
    public TesoreriaDbContext(DbContextOptions<TesoreriaDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "tesoreria";

    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TesoreriaDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionMovimiento : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> builder)
    {
        builder.ToTable("movimiento");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(m => m.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.DocumentoId).HasColumnName("documento_id").IsRequired();
        builder.Property(m => m.Sentido).HasColumnName("sentido").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.Importe).HasColumnName("importe").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(m => m.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(m => m.Metodo).HasColumnName("metodo").HasMaxLength(40);
        builder.Property(m => m.CreadoEn).HasColumnName("creado_en").IsRequired();

        builder.HasIndex(m => new { m.EmpresaId, m.TipoDocumento, m.DocumentoId }).HasDatabaseName("ix_movimiento_documento");
        builder.Ignore(m => m.EventosDominio);
    }
}

internal sealed class RepositorioMovimientos : IRepositorioMovimientos, IConsultaTesoreria
{
    private readonly TesoreriaDbContext _contexto;

    public RepositorioMovimientos(TesoreriaDbContext contexto) => _contexto = contexto;

    public void Agregar(Movimiento movimiento) => _contexto.Movimientos.Add(movimiento);

    public async Task<decimal> SumaAsync(TipoDocumentoTesoreria tipo, Guid documentoId, CancellationToken ct = default)
    {
        var suma = await _contexto.Movimientos
            .Where(m => m.TipoDocumento == tipo && m.DocumentoId == documentoId)
            .SumAsync(m => (decimal?)m.Importe, ct).ConfigureAwait(false);
        return suma ?? 0m;
    }

    public async Task<IReadOnlyList<Movimiento>> ListarAsync(TipoDocumentoTesoreria tipo, Guid documentoId, CancellationToken ct = default) =>
        await _contexto.Movimientos
            .Where(m => m.TipoDocumento == tipo && m.DocumentoId == documentoId)
            .OrderBy(m => m.Fecha)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<decimal> TotalLiquidadoAsync(TipoDocumentoTesoreria tipo, CancellationToken ct = default)
    {
        var suma = await _contexto.Movimientos
            .Where(m => m.TipoDocumento == tipo)
            .SumAsync(m => (decimal?)m.Importe, ct).ConfigureAwait(false);
        return suma ?? 0m;
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class TesoreriaDbContextFactory : IDesignTimeDbContextFactory<TesoreriaDbContext>
{
    public TesoreriaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<TesoreriaDbContext>().UseNpgsql(conexion).Options;
        return new TesoreriaDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

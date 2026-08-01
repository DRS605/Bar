using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Gastos.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Gastos.Infraestructura;

/// <summary>Contexto de persistencia del módulo Gastos.</summary>
public sealed class GastosDbContext : DbContextEmpresaBase, IUnidadDeTrabajoGastos
{
    public GastosDbContext(DbContextOptions<GastosDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "gastos";

    public DbSet<Gasto> Gastos => Set<Gasto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GastosDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionGasto : IEntityTypeConfiguration<Gasto>
{
    public void Configure(EntityTypeBuilder<Gasto> builder)
    {
        builder.ToTable("gasto");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(g => g.ProveedorTexto).HasColumnName("proveedor_texto").HasMaxLength(200);
        builder.Property(g => g.Concepto).HasColumnName("concepto").HasMaxLength(Gasto.LongitudMaximaConcepto).IsRequired();
        builder.Property(g => g.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(g => g.BaseImponible).HasColumnName("base_imponible").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(g => g.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
        builder.Property(g => g.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(g => g.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(g => g.PorcentajeIrpf).HasColumnName("porcentaje_irpf").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(g => g.RetencionIrpf).HasColumnName("retencion_irpf").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(g => g.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(g => g.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(g => g.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(g => g.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(g => new { g.EmpresaId, g.Fecha }).HasDatabaseName("ix_gasto_empresa_fecha");
        builder.Ignore(g => g.EventosDominio);
    }
}

internal sealed class RepositorioGastos : IRepositorioGastos, IConsultaGastos
{
    private readonly GastosDbContext _contexto;

    public RepositorioGastos(GastosDbContext contexto) => _contexto = contexto;

    public Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Gastos.SingleOrDefaultAsync(g => g.Id == id, ct);

    public void Agregar(Gasto gasto) => _contexto.Gastos.Add(gasto);

    public async Task<GastoDto?> ObtenerAsync(Guid gastoId, CancellationToken ct = default)
    {
        var gasto = await _contexto.Gastos.SingleOrDefaultAsync(g => g.Id == gastoId, ct).ConfigureAwait(false);
        return gasto is null ? null : GastoDto.Desde(gasto);
    }

    public async Task<IReadOnlyList<GastoDto>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var gastos = await _contexto.Gastos
            .Where(g => g.EmpresaId == empresaId)
            .OrderByDescending(g => g.Fecha)
            .ToListAsync(ct).ConfigureAwait(false);
        return gastos.Select(GastoDto.Desde).ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class GastosDbContextFactory : IDesignTimeDbContextFactory<GastosDbContext>
{
    public GastosDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<GastosDbContext>().UseNpgsql(conexion).Options;
        return new GastosDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

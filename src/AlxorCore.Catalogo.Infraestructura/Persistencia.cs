using AlxorCore.Catalogo.Aplicacion;
using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Catalogo.Infraestructura;

/// <summary>Contexto de persistencia del módulo Catálogo.</summary>
public sealed class CatalogoDbContext : DbContextEmpresaBase, IUnidadDeTrabajoCatalogo
{
    public CatalogoDbContext(DbContextOptions<CatalogoDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "catalogo";

    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionProducto : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("producto");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(p => p.Referencia).HasColumnName("referencia").HasMaxLength(60);
        builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(Producto.LongitudMaximaNombre).IsRequired();
        builder.Property(p => p.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
        builder.Property(p => p.Unidad).HasColumnName("unidad").HasMaxLength(20).IsRequired();
        builder.Property(p => p.Activo).HasColumnName("activo").IsRequired();
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(p => new { p.EmpresaId, p.Nombre }).HasDatabaseName("ix_producto_empresa_nombre");
        builder.Ignore(p => p.EventosDominio);
    }
}

internal sealed class RepositorioProductos : IRepositorioProductos, IConsultaProductos
{
    private readonly CatalogoDbContext _contexto;

    public RepositorioProductos(CatalogoDbContext contexto) => _contexto = contexto;

    public Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Productos.SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Producto producto) => _contexto.Productos.Add(producto);

    public async Task<ProductoDto?> ObtenerAsync(Guid productoId, CancellationToken ct = default)
    {
        var producto = await _contexto.Productos.SingleOrDefaultAsync(p => p.Id == productoId, ct).ConfigureAwait(false);
        return producto is null ? null : ProductoDto.Desde(producto);
    }

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid empresaId, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Productos.Where(p => p.EmpresaId == empresaId);
        if (!incluirInactivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        var productos = await consulta.OrderBy(p => p.Nombre).ToListAsync(ct).ConfigureAwait(false);
        return productos.Select(ProductoDto.Desde).ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class CatalogoDbContextFactory : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<CatalogoDbContext>().UseNpgsql(conexion).Options;
        return new CatalogoDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

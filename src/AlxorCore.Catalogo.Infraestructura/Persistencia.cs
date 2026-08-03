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

    public DbSet<HistoricoPrecio> HistoricoPrecios => Set<HistoricoPrecio>();

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
        builder.Property(p => p.PrecioCompra).HasColumnName("precio_compra").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
        builder.Property(p => p.Unidad).HasColumnName("unidad").HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProveedorHabitualId).HasColumnName("proveedor_habitual_id");
        builder.Property(p => p.Activo).HasColumnName("activo").IsRequired();
        builder.Property(p => p.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(p => new { p.EmpresaId, p.Nombre }).HasDatabaseName("ix_producto_empresa_nombre");
        builder.Ignore(p => p.EventosDominio);
    }
}

internal sealed class ConfiguracionHistoricoPrecio : IEntityTypeConfiguration<HistoricoPrecio>
{
    public void Configure(EntityTypeBuilder<HistoricoPrecio> builder)
    {
        builder.ToTable("historico_precio");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(h => h.ProductoId).HasColumnName("producto_id").IsRequired();
        builder.Property(h => h.PrecioVenta).HasColumnName("precio_venta").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(h => h.PrecioCompra).HasColumnName("precio_compra").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(h => h.RegistradoEn).HasColumnName("registrado_en").IsRequired();

        builder.HasIndex(h => new { h.EmpresaId, h.ProductoId, h.RegistradoEn }).HasDatabaseName("ix_historico_precio_producto");
        builder.Ignore(h => h.EventosDominio);
    }
}

internal sealed class RepositorioHistoricoPrecios : IRepositorioHistoricoPrecios, IConsultaHistoricoPrecios
{
    private readonly CatalogoDbContext _contexto;

    public RepositorioHistoricoPrecios(CatalogoDbContext contexto) => _contexto = contexto;

    public void Agregar(HistoricoPrecio historico) => _contexto.HistoricoPrecios.Add(historico);

    public async Task<IReadOnlyList<HistoricoPrecioDto>> ListarPorProductoAsync(Guid productoId, CancellationToken ct = default)
    {
        var filas = await _contexto.HistoricoPrecios
            .Where(h => h.ProductoId == productoId)
            .OrderByDescending(h => h.RegistradoEn)
            .ToListAsync(ct).ConfigureAwait(false);
        return filas.Select(HistoricoPrecioDto.Desde).ToList();
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

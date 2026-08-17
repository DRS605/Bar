using AlxorCore.Hosteleria.Aplicacion;
using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Hosteleria.Infraestructura;

/// <summary>Contexto de persistencia del módulo Hostelería.</summary>
public sealed class HosteleriaDbContext : DbContextEmpresaBase, IUnidadDeTrabajoHosteleria
{
    public HosteleriaDbContext(DbContextOptions<HosteleriaDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "hosteleria";

    public DbSet<Mesa> Mesas => Set<Mesa>();

    public DbSet<Comanda> Comandas => Set<Comanda>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HosteleriaDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionMesa : IEntityTypeConfiguration<Mesa>
{
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("mesa");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(m => m.Nombre).HasColumnName("nombre").HasMaxLength(Mesa.LongitudMaximaNombre).IsRequired();
        builder.Property(m => m.Zona).HasColumnName("zona").HasMaxLength(Mesa.LongitudMaximaZona);
        builder.Property(m => m.Capacidad).HasColumnName("capacidad").IsRequired();
        builder.Property(m => m.Forma).HasColumnName("forma").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.PosX).HasColumnName("pos_x").IsRequired();
        builder.Property(m => m.PosY).HasColumnName("pos_y").IsRequired();
        builder.Property(m => m.Activa).HasColumnName("activa").IsRequired();
        builder.Property(m => m.CreadaEn).HasColumnName("creada_en").IsRequired();
        builder.Property(m => m.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(m => new { m.EmpresaId, m.Nombre }).HasDatabaseName("ix_mesa_empresa_nombre");
        builder.Ignore(m => m.EventosDominio);
    }
}

internal sealed class ConfiguracionComanda : IEntityTypeConfiguration<Comanda>
{
    public void Configure(EntityTypeBuilder<Comanda> builder)
    {
        builder.ToTable("comanda");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.MesaId).HasColumnName("mesa_id").IsRequired();
        builder.Property(c => c.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(c => c.Notas).HasColumnName("notas").HasMaxLength(Comanda.LongitudMaximaNotas);
        builder.Property(c => c.AbiertaEn).HasColumnName("abierta_en").IsRequired();
        builder.Property(c => c.CerradaEn).HasColumnName("cerrada_en");
        builder.Property(c => c.BaseImponible).HasColumnName("base_imponible").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(c => c.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(c => c.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(c => c.MetodoCobro).HasColumnName("metodo_cobro").HasMaxLength(20).HasConversion<string>();
        builder.Property(c => c.FacturaId).HasColumnName("factura_id");
        builder.Property(c => c.NumeroTicket).HasColumnName("numero_ticket").HasMaxLength(30);

        builder.HasIndex(c => new { c.EmpresaId, c.Estado, c.MesaId }).HasDatabaseName("ix_comanda_empresa_estado_mesa");
        builder.Ignore(c => c.EventosDominio);

        builder.OwnsMany(c => c.Lineas, linea =>
        {
            linea.ToTable("linea_comanda");
            linea.WithOwner().HasForeignKey("ComandaId");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            linea.Property(l => l.ComandaId).HasColumnName("comanda_id").IsRequired();
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id").IsRequired();
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(LineaComanda.LongitudMaximaDescripcion).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CantidadEnviadaCocina).HasColumnName("cantidad_enviada_cocina").HasColumnType("numeric(14,3)").IsRequired();
            linea.Ignore(l => l.Total);
            linea.Ignore(l => l.CantidadPendienteCocina);
            linea.HasIndex("ComandaId").HasDatabaseName("ix_linea_comanda_comanda");
        });
    }
}

internal sealed class RepositorioMesas : IRepositorioMesas, IConsultaMesas
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioMesas(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<Mesa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Mesas.SingleOrDefaultAsync(m => m.Id == id, ct);

    public void Agregar(Mesa mesa) => _contexto.Mesas.Add(mesa);

    public async Task<MesaDto?> ObtenerAsync(Guid mesaId, CancellationToken ct = default)
    {
        var mesa = await _contexto.Mesas.SingleOrDefaultAsync(m => m.Id == mesaId, ct).ConfigureAwait(false);
        if (mesa is null)
        {
            return null;
        }

        var abierta = await _contexto.Comandas
            .Where(c => c.MesaId == mesaId && c.Estado == EstadoComanda.Abierta)
            .Select(c => new { c.Id, c.Total })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return MesaDto.Desde(mesa, abierta is not null, abierta?.Id, abierta?.Total ?? 0m);
    }

    public async Task<IReadOnlyList<MesaDto>> ListarAsync(Guid empresaId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        var consulta = _contexto.Mesas.Where(m => m.EmpresaId == empresaId);
        if (!incluirInactivas)
        {
            consulta = consulta.Where(m => m.Activa);
        }

        var mesas = await consulta.OrderBy(m => m.Zona).ThenBy(m => m.Nombre).ToListAsync(ct).ConfigureAwait(false);

        var abiertas = await _contexto.Comandas
            .Where(c => c.EmpresaId == empresaId && c.Estado == EstadoComanda.Abierta)
            .Select(c => new { c.MesaId, c.Id, c.Total })
            .ToListAsync(ct).ConfigureAwait(false);
        var porMesa = abiertas.GroupBy(a => a.MesaId).ToDictionary(g => g.Key, g => g.First());

        return mesas.Select(m =>
        {
            porMesa.TryGetValue(m.Id, out var abierta);
            return MesaDto.Desde(m, abierta is not null, abierta?.Id, abierta?.Total ?? 0m);
        }).ToList();
    }
}

internal sealed class RepositorioComandas : IRepositorioComandas, IConsultaComandas
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioComandas(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<Comanda?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Comandas.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<Comanda?> ObtenerAbiertaPorMesaAsync(Guid mesaId, CancellationToken ct = default) =>
        _contexto.Comandas.SingleOrDefaultAsync(c => c.MesaId == mesaId && c.Estado == EstadoComanda.Abierta, ct);

    public void Agregar(Comanda comanda) => _contexto.Comandas.Add(comanda);

    public async Task<ComandaDto?> ObtenerAsync(Guid comandaId, CancellationToken ct = default)
    {
        var comanda = await _contexto.Comandas.SingleOrDefaultAsync(c => c.Id == comandaId, ct).ConfigureAwait(false);
        return comanda is null ? null : ComandaDto.Desde(comanda);
    }

    public async Task<IReadOnlyList<ComandaResumen>> ListarAbiertasAsync(Guid empresaId, CancellationToken ct = default)
    {
        var consulta =
            from c in _contexto.Comandas
            where c.EmpresaId == empresaId && c.Estado == EstadoComanda.Abierta
            join m in _contexto.Mesas on c.MesaId equals m.Id into ms
            from m in ms.DefaultIfEmpty()
            orderby c.AbiertaEn
            select new ComandaResumen(c.Id, c.MesaId, m != null ? m.Nombre : string.Empty, c.Estado.ToString(), c.AbiertaEn, c.Lineas.Count, c.Total);

        return await consulta.ToListAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class HosteleriaDbContextFactory : IDesignTimeDbContextFactory<HosteleriaDbContext>
{
    public HosteleriaDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<HosteleriaDbContext>().UseNpgsql(conexion).Options;
        return new HosteleriaDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

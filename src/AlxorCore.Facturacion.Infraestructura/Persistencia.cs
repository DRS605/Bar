using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Facturacion.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Facturacion.Infraestructura;

/// <summary>Contexto de persistencia del módulo Facturación.</summary>
public sealed class FacturacionDbContext : DbContextEmpresaBase, IUnidadDeTrabajoFacturacion
{
    public FacturacionDbContext(DbContextOptions<FacturacionDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "facturacion";

    public DbSet<Factura> Facturas => Set<Factura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FacturacionDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionFactura : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.ToTable("factura");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.EmpresaId).HasColumnName("empresa_id").IsRequired();

        builder.Property(f => f.Prefijo).HasColumnName("prefijo").HasMaxLength(10).IsRequired();
        builder.Property(f => f.Ejercicio).HasColumnName("ejercicio").IsRequired();
        builder.Property(f => f.Numero).HasColumnName("numero").IsRequired();
        builder.Property(f => f.NumeroCompleto).HasColumnName("numero_completo").HasMaxLength(30).IsRequired();
        builder.HasIndex(f => new { f.EmpresaId, f.Prefijo, f.Ejercicio, f.Numero })
            .IsUnique().HasDatabaseName("ux_factura_numero");

        builder.Property(f => f.FechaEmision).HasColumnName("fecha_emision").IsRequired();
        builder.Property(f => f.FechaOperacion).HasColumnName("fecha_operacion").IsRequired();

        builder.Property(f => f.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(f => f.ClienteNombre).HasColumnName("cliente_nombre").HasMaxLength(200).IsRequired();
        builder.Property(f => f.ClienteNif).HasColumnName("cliente_nif").HasMaxLength(20);
        builder.Property(f => f.ClienteCalle).HasColumnName("cliente_calle").HasMaxLength(200);
        builder.Property(f => f.ClienteCodigoPostal).HasColumnName("cliente_cp").HasMaxLength(10);
        builder.Property(f => f.ClientePoblacion).HasColumnName("cliente_poblacion").HasMaxLength(120);
        builder.Property(f => f.ClienteProvincia).HasColumnName("cliente_provincia").HasMaxLength(120);
        builder.Property(f => f.Pais).HasColumnName("pais").HasMaxLength(2).IsRequired();

        builder.Property(f => f.BaseImponible).HasColumnName("base_imponible").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.PorcentajeIrpf).HasColumnName("porcentaje_irpf").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(f => f.RetencionIrpf).HasColumnName("retencion_irpf").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(f => f.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();

        builder.Property(f => f.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(f => f.TipoFactura).HasColumnName("tipo_factura").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(f => f.RectificaFacturaId).HasColumnName("rectifica_factura_id");

        builder.Property(f => f.CreadoEn).HasColumnName("creado_en").IsRequired();

        // Campos VeriFactu/SII reservados (nullable, sin lógica en el MVP).
        builder.Property(f => f.Huella).HasColumnName("huella").HasMaxLength(128);
        builder.Property(f => f.HuellaAnterior).HasColumnName("huella_anterior").HasMaxLength(128);
        builder.Property(f => f.IdRegistro).HasColumnName("id_registro").HasMaxLength(64);
        builder.Property(f => f.TipoOperacion).HasColumnName("tipo_operacion").HasMaxLength(20);
        builder.Property(f => f.EstadoEnvioAeat).HasColumnName("estado_envio_aeat").HasMaxLength(20);

        builder.Ignore(f => f.EventosDominio);

        builder.OwnsMany(f => f.Lineas, linea =>
        {
            linea.ToTable("linea_factura");
            linea.WithOwner().HasForeignKey("factura_id");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id");
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id");
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.PorcentajeDescuento).HasColumnName("descuento").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
        });
    }
}

internal sealed class RepositorioFacturas : IRepositorioFacturas, IConsultaFacturas
{
    private readonly FacturacionDbContext _contexto;

    public RepositorioFacturas(FacturacionDbContext contexto) => _contexto = contexto;

    public Task<Factura?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Facturas.SingleOrDefaultAsync(f => f.Id == id, ct);

    public void Agregar(Factura factura) => _contexto.Facturas.Add(factura);

    public async Task<FacturaDto?> ObtenerAsync(Guid facturaId, CancellationToken ct = default)
    {
        var factura = await _contexto.Facturas.SingleOrDefaultAsync(f => f.Id == facturaId, ct).ConfigureAwait(false);
        return factura is null ? null : FacturaDto.Desde(factura);
    }

    public async Task<IReadOnlyList<FacturaResumen>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var facturas = await _contexto.Facturas
            .Where(f => f.EmpresaId == empresaId)
            .OrderByDescending(f => f.FechaEmision).ThenByDescending(f => f.Numero)
            .ToListAsync(ct).ConfigureAwait(false);

        return facturas
            .Select(f => new FacturaResumen(
                f.Id, f.NumeroCompleto, f.FechaEmision, f.ClienteNombre, f.ClienteNif, f.BaseImponible, f.CuotaIva, f.Total, f.Estado.ToString()))
            .ToList();
    }
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class FacturacionDbContextFactory : IDesignTimeDbContextFactory<FacturacionDbContext>
{
    public FacturacionDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<FacturacionDbContext>().UseNpgsql(conexion).Options;
        return new FacturacionDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

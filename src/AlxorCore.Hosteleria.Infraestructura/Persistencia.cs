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

    public DbSet<PedidoWeb> PedidosWeb => Set<PedidoWeb>();

    public DbSet<AvisoMesa> Avisos => Set<AvisoMesa>();

    public DbSet<TraduccionCarta> Traducciones => Set<TraduccionCarta>();

    public DbSet<FichaCarta> FichasCarta => Set<FichaCarta>();

    public DbSet<ConfiguracionCarta> ConfiguracionesCarta => Set<ConfiguracionCarta>();

    public DbSet<SuscripcionBar> Suscripciones => Set<SuscripcionBar>();

    public DbSet<MenuDia> MenusDia => Set<MenuDia>();

    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();

    public DbSet<GrupoOpcion> GruposOpcion => Set<GrupoOpcion>();

    public DbSet<ZonaProducto> ZonasProducto => Set<ZonaProducto>();

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
        builder.Property(m => m.TokenCarta).HasColumnName("token_carta").IsRequired();
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
        builder.Property(c => c.DescuentoPorcentaje).HasColumnName("descuento_porcentaje").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(c => c.MetodoCobro).HasColumnName("metodo_cobro").HasMaxLength(20).HasConversion<string>();
        builder.Property(c => c.FacturaId).HasColumnName("factura_id");
        builder.Property(c => c.NumeroTicket).HasColumnName("numero_ticket").HasMaxLength(30);
        builder.Property(c => c.UsuarioId).HasColumnName("usuario_id");
        builder.Property(c => c.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(60);

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
            linea.Property(l => l.Nota).HasColumnName("nota").HasMaxLength(LineaComanda.LongitudMaximaDescripcion);
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(14,4)").IsRequired();
            linea.Property(l => l.CodigoIva).HasColumnName("codigo_iva").HasMaxLength(10).IsRequired();
            linea.Property(l => l.PorcentajeIva).HasColumnName("porcentaje_iva").HasColumnType("numeric(5,2)").IsRequired();
            linea.Property(l => l.Base).HasColumnName("base").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CuotaIva).HasColumnName("cuota_iva").HasColumnType("numeric(14,2)").IsRequired();
            linea.Property(l => l.CantidadEnviadaCocina).HasColumnName("cantidad_enviada_cocina").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.CantidadServida).HasColumnName("cantidad_servida").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.CantidadCobrada).HasColumnName("cantidad_cobrada").HasColumnType("numeric(14,3)").IsRequired();
            linea.Ignore(l => l.Total);
            linea.Ignore(l => l.CantidadPendienteCocina);
            linea.Ignore(l => l.CantidadPendienteServir);
            linea.Ignore(l => l.CantidadPendienteCobro);
            linea.Ignore(l => l.BasePendiente);
            linea.Ignore(l => l.CuotaIvaPendiente);
            linea.Ignore(l => l.TotalPendiente);
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

internal sealed class RepositorioComandas : IRepositorioComandas, IConsultaComandas, IConsultaCocina
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

    public async Task<IReadOnlyList<VentasCamareroDto>> VentasPorCamareroAsync(Guid empresaId, DateOnly dia, CancellationToken ct = default)
    {
        var desde = new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var hasta = desde.AddDays(1);

        var filas = await _contexto.Comandas
            .Where(c => c.EmpresaId == empresaId && c.Estado == EstadoComanda.Cobrada
                && c.CerradaEn >= desde && c.CerradaEn < hasta)
            .GroupBy(c => new { c.UsuarioId, c.UsuarioNombre })
            .Select(g => new { g.Key.UsuarioId, g.Key.UsuarioNombre, Comandas = g.Count(), Total = g.Sum(x => x.Total) })
            .ToListAsync(ct).ConfigureAwait(false);

        return filas
            .Select(f => new VentasCamareroDto(f.UsuarioId, string.IsNullOrWhiteSpace(f.UsuarioNombre) ? "Sin asignar" : f.UsuarioNombre!, f.Comandas, f.Total))
            .OrderByDescending(f => f.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<ItemCocinaDto>> PendientesAsync(Guid empresaId, ZonaPreparacion? zona = null, CancellationToken ct = default)
    {
        // Las comandas abiertas son pocas y sus líneas (propiedad poseída) se cargan con el agregado.
        var comandas = await _contexto.Comandas
            .Where(c => c.EmpresaId == empresaId && c.Estado == EstadoComanda.Abierta)
            .ToListAsync(ct).ConfigureAwait(false);
        var zonas = await _contexto.ZonasProducto
            .Where(z => z.EmpresaId == empresaId)
            .ToDictionaryAsync(z => z.ProductoId, z => z.Zona, ct).ConfigureAwait(false);
        var mesas = await _contexto.Mesas
            .Where(m => m.EmpresaId == empresaId)
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, ct).ConfigureAwait(false);

        var items = new List<ItemCocinaDto>();
        foreach (var c in comandas)
        {
            foreach (var l in c.Lineas)
            {
                if (l.CantidadPendienteServir <= 0)
                {
                    continue;
                }

                var z = zonas.TryGetValue(l.ProductoId, out var zz) ? zz : ZonaPreparacion.Cocina;
                if (zona is not null && z != zona.Value)
                {
                    continue;
                }

                items.Add(new ItemCocinaDto(
                    c.Id, l.Id, mesas.TryGetValue(c.MesaId, out var nombre) ? nombre : "Mesa",
                    l.Descripcion, l.CantidadPendienteServir, z.ToString(), l.Nota, c.AbiertaEn));
            }
        }

        return items.OrderBy(i => i.Desde).ToList();
    }
}

internal sealed class ConfiguracionPedidoWeb : IEntityTypeConfiguration<PedidoWeb>
{
    public void Configure(EntityTypeBuilder<PedidoWeb> builder)
    {
        builder.ToTable("pedido_web");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(p => p.MesaId).HasColumnName("mesa_id").IsRequired();
        builder.Property(p => p.Idioma).HasColumnName("idioma").HasMaxLength(2).HasConversion<string>().IsRequired();
        builder.Property(p => p.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(p => p.RecibidoEn).HasColumnName("recibido_en").IsRequired();
        builder.Property(p => p.ResueltoEn).HasColumnName("resuelto_en");
        builder.Property(p => p.ComandaId).HasColumnName("comanda_id");

        builder.HasIndex(p => new { p.EmpresaId, p.Estado, p.RecibidoEn }).HasDatabaseName("ix_pedido_web_empresa_estado");
        builder.Ignore(p => p.EventosDominio);

        builder.OwnsMany(p => p.Lineas, linea =>
        {
            linea.ToTable("linea_pedido_web");
            linea.WithOwner().HasForeignKey("PedidoWebId");
            linea.HasKey(l => l.Id);
            linea.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
            linea.Property(l => l.PedidoWebId).HasColumnName("pedido_web_id").IsRequired();
            linea.Property(l => l.EmpresaId).HasColumnName("empresa_id").IsRequired();
            linea.Property(l => l.ProductoId).HasColumnName("producto_id").IsRequired();
            linea.Property(l => l.Descripcion).HasColumnName("descripcion").HasMaxLength(LineaPedidoWeb.LongitudMaximaNota).IsRequired();
            linea.Property(l => l.Cantidad).HasColumnName("cantidad").HasColumnType("numeric(14,3)").IsRequired();
            linea.Property(l => l.Nota).HasColumnName("nota").HasMaxLength(LineaPedidoWeb.LongitudMaximaNota);
            linea.HasIndex("PedidoWebId").HasDatabaseName("ix_linea_pedido_web_pedido");
        });
    }
}

internal sealed class ConfiguracionAvisoMesa : IEntityTypeConfiguration<AvisoMesa>
{
    public void Configure(EntityTypeBuilder<AvisoMesa> builder)
    {
        builder.ToTable("aviso_mesa");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(a => a.MesaId).HasColumnName("mesa_id").IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(a => a.RecibidoEn).HasColumnName("recibido_en").IsRequired();
        builder.Property(a => a.AtendidoEn).HasColumnName("atendido_en");

        builder.HasIndex(a => new { a.EmpresaId, a.AtendidoEn }).HasDatabaseName("ix_aviso_mesa_empresa_atendido");
        builder.Ignore(a => a.Pendiente);
        builder.Ignore(a => a.EventosDominio);
    }
}

internal sealed class ConfiguracionTraduccionCarta : IEntityTypeConfiguration<TraduccionCarta>
{
    public void Configure(EntityTypeBuilder<TraduccionCarta> builder)
    {
        builder.ToTable("traduccion_carta");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(t => t.Ambito).HasColumnName("ambito").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(t => t.Clave).HasColumnName("clave").HasMaxLength(TraduccionCarta.LongitudMaximaClave).IsRequired();
        builder.Property(t => t.Idioma).HasColumnName("idioma").HasMaxLength(2).HasConversion<string>().IsRequired();
        builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(TraduccionCarta.LongitudMaximaNombre).IsRequired();
        builder.Property(t => t.Descripcion).HasColumnName("descripcion").HasMaxLength(TraduccionCarta.LongitudMaximaDescripcion);
        builder.Property(t => t.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(t => new { t.EmpresaId, t.Ambito, t.Clave, t.Idioma }).IsUnique().HasDatabaseName("ux_traduccion_carta");
        builder.Ignore(t => t.EventosDominio);
    }
}

internal sealed class RepositorioPedidosWeb : IRepositorioPedidosWeb, IConsultaPedidosWeb
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioPedidosWeb(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<PedidoWeb?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.PedidosWeb.SingleOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(PedidoWeb pedido) => _contexto.PedidosWeb.Add(pedido);

    public async Task<IReadOnlyList<PedidoWebResumen>> ListarPendientesAsync(Guid empresaId, CancellationToken ct = default)
    {
        var pedidos = await _contexto.PedidosWeb
            .Where(p => p.EmpresaId == empresaId && p.Estado == EstadoPedidoWeb.Pendiente)
            .OrderBy(p => p.RecibidoEn)
            .ToListAsync(ct).ConfigureAwait(false);

        if (pedidos.Count == 0)
        {
            return Array.Empty<PedidoWebResumen>();
        }

        var mesaIds = pedidos.Select(p => p.MesaId).Distinct().ToList();
        var nombres = await _contexto.Mesas
            .Where(m => mesaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Nombre })
            .ToListAsync(ct).ConfigureAwait(false);
        var nombrePorMesa = nombres.ToDictionary(x => x.Id, x => x.Nombre);

        return pedidos.Select(p => new PedidoWebResumen(
            p.Id, p.MesaId, nombrePorMesa.TryGetValue(p.MesaId, out var n) ? n : string.Empty,
            p.Idioma.ToString(), p.RecibidoEn,
            p.Lineas.Select(l => new LineaPedidoWebResumen(l.ProductoId, l.Descripcion, l.Cantidad, l.Nota)).ToList())).ToList();
    }
}

internal sealed class RepositorioAvisos : IRepositorioAvisos, IConsultaAvisos
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioAvisos(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<AvisoMesa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Avisos.SingleOrDefaultAsync(a => a.Id == id, ct);

    public void Agregar(AvisoMesa aviso) => _contexto.Avisos.Add(aviso);

    public async Task<IReadOnlyList<AvisoMesaDto>> ListarPendientesAsync(Guid empresaId, CancellationToken ct = default)
    {
        var consulta =
            from a in _contexto.Avisos
            where a.EmpresaId == empresaId && a.AtendidoEn == null
            join m in _contexto.Mesas on a.MesaId equals m.Id into ms
            from m in ms.DefaultIfEmpty()
            orderby a.RecibidoEn
            select new AvisoMesaDto(a.Id, a.MesaId, m != null ? m.Nombre : string.Empty, a.Tipo.ToString(), a.RecibidoEn);

        return await consulta.ToListAsync(ct).ConfigureAwait(false);
    }
}

internal sealed class ConfiguracionFichaCarta : IEntityTypeConfiguration<FichaCarta>
{
    public void Configure(EntityTypeBuilder<FichaCarta> builder)
    {
        builder.ToTable("ficha_carta");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(f => f.ProductoId).HasColumnName("producto_id").IsRequired();
        builder.Property(f => f.Alergenos).HasColumnName("alergenos").HasConversion<int>().IsRequired();
        builder.Property(f => f.Recomendado).HasColumnName("recomendado").IsRequired();
        builder.Property(f => f.Picante).HasColumnName("picante").IsRequired();
        builder.Property(f => f.Agotado).HasColumnName("agotado").IsRequired();
        builder.Property(f => f.Foto).HasColumnName("foto");
        builder.Property(f => f.FotoTipo).HasColumnName("foto_tipo").HasMaxLength(30);
        builder.Property(f => f.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(f => new { f.EmpresaId, f.ProductoId }).IsUnique().HasDatabaseName("ux_ficha_carta_producto");
        builder.Ignore(f => f.TieneFoto);
        builder.Ignore(f => f.EventosDominio);
    }
}

internal sealed class ConfiguracionCartaConfig : IEntityTypeConfiguration<ConfiguracionCarta>
{
    public void Configure(EntityTypeBuilder<ConfiguracionCarta> builder)
    {
        builder.ToTable("configuracion_carta");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(c => c.Tema).HasColumnName("tema").HasMaxLength(20).IsRequired();
        builder.Property(c => c.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(c => c.EmpresaId).IsUnique().HasDatabaseName("ux_configuracion_carta_empresa");
        builder.Ignore(c => c.EventosDominio);
    }
}

internal sealed class RepositorioConfiguracionCarta : IRepositorioConfiguracionCarta
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioConfiguracionCarta(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<ConfiguracionCarta?> ObtenerAsync(Guid empresaId, CancellationToken ct = default) =>
        _contexto.ConfiguracionesCarta.SingleOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

    public void Agregar(ConfiguracionCarta configuracion) => _contexto.ConfiguracionesCarta.Add(configuracion);
}

internal sealed class SuscripcionConfig : IEntityTypeConfiguration<SuscripcionBar>
{
    public void Configure(EntityTypeBuilder<SuscripcionBar> builder)
    {
        builder.ToTable("suscripcion");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(s => s.Plan).HasColumnName("plan").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(s => s.EmpresaId).IsUnique().HasDatabaseName("ux_suscripcion_empresa");
        builder.Ignore(s => s.EventosDominio);
    }
}

internal sealed class ZonaProductoConfig : IEntityTypeConfiguration<ZonaProducto>
{
    public void Configure(EntityTypeBuilder<ZonaProducto> builder)
    {
        builder.ToTable("zona_producto");
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id).HasColumnName("id");
        builder.Property(z => z.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(z => z.ProductoId).HasColumnName("producto_id").IsRequired();
        builder.Property(z => z.Zona).HasColumnName("zona").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(z => z.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(z => new { z.EmpresaId, z.ProductoId }).IsUnique().HasDatabaseName("ux_zona_producto");
        builder.Ignore(z => z.EventosDominio);
    }
}

internal sealed class RepositorioZonasProducto : IRepositorioZonasProducto
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioZonasProducto(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<ZonaProducto?> ObtenerAsync(Guid productoId, CancellationToken ct = default) =>
        _contexto.ZonasProducto.SingleOrDefaultAsync(z => z.ProductoId == productoId, ct);

    public async Task<IReadOnlyList<ZonaProducto>> ListarPorEmpresaAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.ZonasProducto.Where(z => z.EmpresaId == empresaId).ToListAsync(ct).ConfigureAwait(false);

    public void Agregar(ZonaProducto zona) => _contexto.ZonasProducto.Add(zona);
}

internal sealed class GrupoOpcionConfig : IEntityTypeConfiguration<GrupoOpcion>
{
    public void Configure(EntityTypeBuilder<GrupoOpcion> builder)
    {
        builder.ToTable("grupo_opcion");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(g => g.ProductoId).HasColumnName("producto_id").IsRequired();
        builder.Property(g => g.Nombre).HasColumnName("nombre").HasMaxLength(GrupoOpcion.LongitudMaximaNombre).IsRequired();
        builder.Property(g => g.Seleccion).HasColumnName("seleccion").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(g => g.Obligatorio).HasColumnName("obligatorio").IsRequired();
        builder.Property(g => g.Orden).HasColumnName("orden").IsRequired();

        builder.HasIndex(g => new { g.EmpresaId, g.ProductoId }).HasDatabaseName("ix_grupo_opcion_producto");
        builder.Ignore(g => g.EventosDominio);

        builder.OwnsMany(g => g.Opciones, op =>
        {
            op.ToTable("opcion_producto");
            op.WithOwner().HasForeignKey("GrupoOpcionId");
            op.HasKey(o => o.Id);
            op.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            op.Property(o => o.GrupoOpcionId).HasColumnName("grupo_opcion_id").IsRequired();
            op.Property(o => o.EmpresaId).HasColumnName("empresa_id").IsRequired();
            op.Property(o => o.Nombre).HasColumnName("nombre").HasMaxLength(OpcionProducto.LongitudMaximaNombre).IsRequired();
            op.Property(o => o.PrecioDelta).HasColumnName("precio_delta").HasColumnType("numeric(14,2)").IsRequired();
            op.Property(o => o.Orden).HasColumnName("orden").IsRequired();
            op.HasIndex("GrupoOpcionId").HasDatabaseName("ix_opcion_producto_grupo");
        });
    }
}

internal sealed class RepositorioGruposOpcion : IRepositorioGruposOpcion
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioGruposOpcion(HosteleriaDbContext contexto) => _contexto = contexto;

    public async Task<IReadOnlyList<GrupoOpcion>> ListarPorProductoAsync(Guid productoId, CancellationToken ct = default) =>
        await _contexto.GruposOpcion.Where(g => g.ProductoId == productoId).ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<GrupoOpcion>> ListarPorEmpresaAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.GruposOpcion.Where(g => g.EmpresaId == empresaId).ToListAsync(ct).ConfigureAwait(false);

    public void Agregar(GrupoOpcion grupo) => _contexto.GruposOpcion.Add(grupo);

    public void Quitar(GrupoOpcion grupo) => _contexto.GruposOpcion.Remove(grupo);
}

internal sealed class MovimientoCajaConfig : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("caja_movimiento");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(m => m.Fecha).HasColumnName("fecha").IsRequired();
        builder.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(m => m.Importe).HasColumnName("importe").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(m => m.Concepto).HasColumnName("concepto").HasMaxLength(MovimientoCaja.LongitudMaximaConcepto);
        builder.Property(m => m.UsuarioId).HasColumnName("usuario_id");
        builder.Property(m => m.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(60);
        builder.Property(m => m.Momento).HasColumnName("momento").IsRequired();

        builder.HasIndex(m => new { m.EmpresaId, m.Fecha }).HasDatabaseName("ix_caja_movimiento_empresa_fecha");
        builder.Ignore(m => m.ImporteConSigno);
        builder.Ignore(m => m.EventosDominio);
    }
}

internal sealed class RepositorioMovimientosCaja : IRepositorioMovimientosCaja
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioMovimientosCaja(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<MovimientoCaja?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.MovimientosCaja.SingleOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<MovimientoCaja>> ListarPorDiaAsync(Guid empresaId, DateOnly dia, CancellationToken ct = default) =>
        await _contexto.MovimientosCaja
            .Where(m => m.EmpresaId == empresaId && m.Fecha == dia)
            .OrderBy(m => m.Momento)
            .ToListAsync(ct).ConfigureAwait(false);

    public void Agregar(MovimientoCaja movimiento) => _contexto.MovimientosCaja.Add(movimiento);

    public void Quitar(MovimientoCaja movimiento) => _contexto.MovimientosCaja.Remove(movimiento);
}

internal sealed class MenuDiaConfig : IEntityTypeConfiguration<MenuDia>
{
    public void Configure(EntityTypeBuilder<MenuDia> builder)
    {
        builder.ToTable("menu_dia");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(m => m.Precio).HasColumnName("precio").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(m => m.Activo).HasColumnName("activo").IsRequired();
        builder.Property(m => m.Incluye).HasColumnName("incluye").HasMaxLength(MenuDia.LongitudMaximaIncluye);
        builder.Property(m => m.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(m => m.EmpresaId).IsUnique().HasDatabaseName("ux_menu_dia_empresa");
        builder.Ignore(m => m.EventosDominio);

        builder.OwnsMany(m => m.Platos, plato =>
        {
            plato.ToTable("menu_dia_plato");
            plato.WithOwner().HasForeignKey("MenuDiaId");
            plato.HasKey(p => p.Id);
            plato.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            plato.Property(p => p.MenuDiaId).HasColumnName("menu_dia_id").IsRequired();
            plato.Property(p => p.EmpresaId).HasColumnName("empresa_id").IsRequired();
            plato.Property(p => p.Seccion).HasColumnName("seccion").HasMaxLength(PlatoMenu.LongitudMaximaSeccion).IsRequired();
            plato.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(PlatoMenu.LongitudMaximaNombre).IsRequired();
            plato.Property(p => p.Orden).HasColumnName("orden").IsRequired();
            plato.HasIndex("MenuDiaId").HasDatabaseName("ix_menu_dia_plato_menu");
        });
    }
}

internal sealed class RepositorioMenuDia : IRepositorioMenuDia
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioMenuDia(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<MenuDia?> ObtenerAsync(Guid empresaId, CancellationToken ct = default) =>
        _contexto.MenusDia.SingleOrDefaultAsync(m => m.EmpresaId == empresaId, ct);

    public void Agregar(MenuDia menu) => _contexto.MenusDia.Add(menu);
}

internal sealed class RepositorioSuscripcion : IRepositorioSuscripcion, IConsultaPlanBar
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioSuscripcion(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<SuscripcionBar?> ObtenerAsync(Guid empresaId, CancellationToken ct = default) =>
        _contexto.Suscripciones.SingleOrDefaultAsync(s => s.EmpresaId == empresaId, ct);

    public void Agregar(SuscripcionBar suscripcion) => _contexto.Suscripciones.Add(suscripcion);

    public async Task<PlanBar> ObtenerPlanAsync(Guid empresaId, CancellationToken ct = default)
    {
        var plan = await _contexto.Suscripciones
            .Where(s => s.EmpresaId == empresaId)
            .Select(s => (PlanBar?)s.Plan)
            .SingleOrDefaultAsync(ct).ConfigureAwait(false);
        return plan ?? PlanesBar.PorDefecto;
    }
}

internal sealed class RepositorioFichasCarta : IRepositorioFichasCarta, IConsultaFichasCarta
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioFichasCarta(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<FichaCarta?> ObtenerPorProductoAsync(Guid productoId, CancellationToken ct = default) =>
        _contexto.FichasCarta.SingleOrDefaultAsync(f => f.ProductoId == productoId, ct);

    public void Agregar(FichaCarta ficha) => _contexto.FichasCarta.Add(ficha);

    public async Task<IReadOnlyList<FichaCartaDto>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        // No cargamos los bytes de la foto en el listado; solo si tiene.
        var filas = await _contexto.FichasCarta
            .Where(f => f.EmpresaId == empresaId)
            .Select(f => new { f.ProductoId, f.Alergenos, f.Recomendado, f.Picante, f.Agotado, TieneFoto = f.Foto != null })
            .ToListAsync(ct).ConfigureAwait(false);

        return filas.Select(f => new FichaCartaDto(f.ProductoId, Alergenos.ANombres(f.Alergenos), f.Recomendado, f.Picante, f.Agotado, f.TieneFoto)).ToList();
    }

    public async Task<FotoProducto?> ObtenerFotoAsync(Guid empresaId, Guid productoId, CancellationToken ct = default)
    {
        var f = await _contexto.FichasCarta
            .Where(x => x.EmpresaId == empresaId && x.ProductoId == productoId && x.Foto != null)
            .Select(x => new { x.Foto, x.FotoTipo })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return f?.Foto is null ? null : new FotoProducto(f.Foto, f.FotoTipo ?? "image/jpeg");
    }
}

internal sealed class RepositorioTraducciones : IRepositorioTraducciones, IConsultaTraducciones
{
    private readonly HosteleriaDbContext _contexto;

    public RepositorioTraducciones(HosteleriaDbContext contexto) => _contexto = contexto;

    public Task<TraduccionCarta?> ObtenerAsync(Guid empresaId, AmbitoTraduccion ambito, string clave, IdiomaCarta idioma, CancellationToken ct = default) =>
        _contexto.Traducciones.SingleOrDefaultAsync(
            t => t.EmpresaId == empresaId && t.Ambito == ambito && t.Clave == clave && t.Idioma == idioma, ct);

    public void Agregar(TraduccionCarta traduccion) => _contexto.Traducciones.Add(traduccion);

    public void Quitar(TraduccionCarta traduccion) => _contexto.Traducciones.Remove(traduccion);

    public async Task<IReadOnlyList<TraduccionCartaDto>> ListarAsync(Guid empresaId, CancellationToken ct = default)
    {
        var lista = await _contexto.Traducciones
            .Where(t => t.EmpresaId == empresaId)
            .ToListAsync(ct).ConfigureAwait(false);
        return lista.Select(TraduccionCartaDto.Desde).ToList();
    }

    public async Task<IReadOnlyList<TraduccionCartaDto>> ListarPorIdiomaAsync(Guid empresaId, IdiomaCarta idioma, CancellationToken ct = default)
    {
        var lista = await _contexto.Traducciones
            .Where(t => t.EmpresaId == empresaId && t.Idioma == idioma)
            .ToListAsync(ct).ConfigureAwait(false);
        return lista.Select(TraduccionCartaDto.Desde).ToList();
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

using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Persistencia;
using AlxorCore.Reservas.Aplicacion;
using AlxorCore.Reservas.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlxorCore.Reservas.Infraestructura;

/// <summary>Contexto de persistencia del módulo Reservas.</summary>
public sealed class ReservasDbContext : DbContextEmpresaBase, IUnidadDeTrabajoReservas
{
    public ReservasDbContext(DbContextOptions<ReservasDbContext> opciones, IPublicadorEventos publicador, IContextoEmpresa contexto)
        : base(opciones, publicador, contexto)
    {
    }

    public const string Esquema = "reservas";

    public DbSet<Reserva> Reservas => Set<Reserva>();

    public DbSet<Turno> Turnos => Set<Turno>();

    public DbSet<AgendaCalendario> Agendas => Set<AgendaCalendario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservasDbContext).Assembly);
        AplicarFiltroMultiempresa(modelBuilder);
    }
}

internal sealed class ConfiguracionReserva : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("reserva");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(r => r.NombreCliente).HasColumnName("nombre_cliente").HasMaxLength(Reserva.LongitudMaximaNombre).IsRequired();
        builder.Property(r => r.Telefono).HasColumnName("telefono").HasMaxLength(Reserva.LongitudMaximaTelefono);
        builder.Property(r => r.Email).HasColumnName("email").HasMaxLength(Reserva.LongitudMaximaEmail);
        builder.Property(r => r.FechaHora).HasColumnName("fecha_hora").IsRequired();
        builder.Property(r => r.DuracionMinutos).HasColumnName("duracion_minutos").IsRequired();
        builder.Property(r => r.Comensales).HasColumnName("comensales").IsRequired();
        builder.Property(r => r.MesaId).HasColumnName("mesa_id");
        builder.Property(r => r.Notas).HasColumnName("notas").HasMaxLength(Reserva.LongitudMaximaNotas);
        builder.Property(r => r.Estado).HasColumnName("estado").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(r => r.ComandaId).HasColumnName("comanda_id");
        builder.Property(r => r.CreadaEn).HasColumnName("creada_en").IsRequired();
        builder.Property(r => r.ActualizadaEn).HasColumnName("actualizada_en").IsRequired();

        builder.HasIndex(r => new { r.EmpresaId, r.FechaHora }).HasDatabaseName("ix_reserva_empresa_fecha");
        builder.Ignore(r => r.EventosDominio);
        builder.Ignore(r => r.FechaHoraFin);
        builder.Ignore(r => r.EsModificable);
    }
}

internal sealed class ConfiguracionTurno : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("turno");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(Turno.LongitudMaximaNombre).IsRequired();
        builder.Property(t => t.Dias).HasColumnName("dias").HasConversion<int>().IsRequired();
        builder.Property(t => t.HoraInicio).HasColumnName("hora_inicio").IsRequired();
        builder.Property(t => t.HoraFin).HasColumnName("hora_fin").IsRequired();
        builder.Property(t => t.AforoComensales).HasColumnName("aforo_comensales").IsRequired();
        builder.Property(t => t.Activo).HasColumnName("activo").IsRequired();
        builder.Property(t => t.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(t => t.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        builder.HasIndex(t => new { t.EmpresaId, t.Activo }).HasDatabaseName("ix_turno_empresa_activo");
        builder.Ignore(t => t.EventosDominio);
    }
}

internal sealed class ConfiguracionAgendaCalendario : IEntityTypeConfiguration<AgendaCalendario>
{
    public void Configure(EntityTypeBuilder<AgendaCalendario> builder)
    {
        builder.ToTable("agenda_calendario");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EmpresaId).HasColumnName("empresa_id").IsRequired();
        builder.Property(a => a.Token).HasColumnName("token").HasMaxLength(64).IsRequired();

        builder.HasIndex(a => a.EmpresaId).IsUnique().HasDatabaseName("ux_agenda_empresa");
        builder.HasIndex(a => a.Token).IsUnique().HasDatabaseName("ux_agenda_token");
        builder.Ignore(a => a.EventosDominio);
    }
}

internal sealed class RepositorioReservas : IRepositorioReservas, IConsultaReservas
{
    private readonly ReservasDbContext _contexto;

    public RepositorioReservas(ReservasDbContext contexto) => _contexto = contexto;

    public Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Reservas.SingleOrDefaultAsync(r => r.Id == id, ct);

    public void Agregar(Reserva reserva) => _contexto.Reservas.Add(reserva);

    public async Task<ReservaDto?> ObtenerAsync(Guid reservaId, CancellationToken ct = default)
    {
        var reserva = await _contexto.Reservas.SingleOrDefaultAsync(r => r.Id == reservaId, ct).ConfigureAwait(false);
        return reserva is null ? null : ReservaDto.Desde(reserva);
    }

    public async Task<IReadOnlyList<ReservaDto>> ListarAsync(Guid empresaId, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken ct = default)
    {
        var consulta = _contexto.Reservas.Where(r => r.EmpresaId == empresaId);
        if (desde is not null)
        {
            consulta = consulta.Where(r => r.FechaHora >= desde.Value);
        }

        if (hasta is not null)
        {
            consulta = consulta.Where(r => r.FechaHora < hasta.Value);
        }

        var reservas = await consulta.OrderBy(r => r.FechaHora).ToListAsync(ct).ConfigureAwait(false);
        return reservas.Select(ReservaDto.Desde).ToList();
    }
}

internal sealed class RepositorioTurnos : IRepositorioTurnos
{
    private readonly ReservasDbContext _contexto;

    public RepositorioTurnos(ReservasDbContext contexto) => _contexto = contexto;

    public Task<Turno?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Turnos.SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Turno>> ListarActivosAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.Turnos.Where(t => t.EmpresaId == empresaId && t.Activo).ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Turno>> ListarTodosAsync(Guid empresaId, CancellationToken ct = default) =>
        await _contexto.Turnos.Where(t => t.EmpresaId == empresaId).OrderByDescending(t => t.Activo).ThenBy(t => t.HoraInicio).ToListAsync(ct).ConfigureAwait(false);

    public void Agregar(Turno turno) => _contexto.Turnos.Add(turno);
}

internal sealed class RepositorioAgenda : IRepositorioAgenda
{
    private readonly ReservasDbContext _contexto;

    public RepositorioAgenda(ReservasDbContext contexto) => _contexto = contexto;

    public Task<AgendaCalendario?> ObtenerPorEmpresaAsync(Guid empresaId, CancellationToken ct = default) =>
        _contexto.Agendas.SingleOrDefaultAsync(a => a.EmpresaId == empresaId, ct);

    public Task<AgendaCalendario?> ObtenerPorTokenAsync(string token, CancellationToken ct = default) =>
        _contexto.Agendas.SingleOrDefaultAsync(a => a.Token == token, ct);

    public void Agregar(AgendaCalendario agenda) => _contexto.Agendas.Add(agenda);
}

/// <summary>Factoría en tiempo de diseño para migraciones.</summary>
public sealed class ReservasDbContextFactory : IDesignTimeDbContextFactory<ReservasDbContext>
{
    public ReservasDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ALXOR_MIGRACIONES_CONEXION")
            ?? "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres";
        var opciones = new DbContextOptionsBuilder<ReservasDbContext>().UseNpgsql(conexion).Options;
        return new ReservasDbContext(opciones, new PublicadorInactivo(), new ContextoVacio());
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

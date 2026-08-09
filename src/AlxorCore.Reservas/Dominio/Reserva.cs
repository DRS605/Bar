using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Reservas.Dominio;

/// <summary>Estado de una reserva a lo largo de su ciclo de vida.</summary>
public enum EstadoReserva
{
    /// <summary>Solicitada, aún sin confirmar.</summary>
    Pendiente = 1,

    /// <summary>Confirmada con el cliente.</summary>
    Confirmada = 2,

    /// <summary>El cliente ha llegado y se ha sentado (opcionalmente con comanda abierta).</summary>
    Sentada = 3,

    /// <summary>Anulada antes de llegar.</summary>
    Cancelada = 4,

    /// <summary>El cliente no se presentó.</summary>
    NoShow = 5,
}

/// <summary>Se ha creado una reserva.</summary>
public sealed record ReservaCreada(Guid ReservaId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Reserva de mesa de un local de hostelería. Guarda los datos del cliente, el momento y el tamaño
/// del grupo, y opcionalmente la mesa asignada. Al llegar el cliente se «sienta» (y puede abrirse su
/// comanda). Es la base de la agenda que se publica como calendario iCalendar.
/// </summary>
public sealed class Reserva : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 160;
    public const int LongitudMaximaTelefono = 40;
    public const int LongitudMaximaEmail = 160;
    public const int LongitudMaximaNotas = 500;
    public const int ComensalesMaximo = 1000;

    private Reserva(Guid id)
        : base(id, Guid.Empty)
    {
        NombreCliente = null!;
    }

    private Reserva(Guid id, Guid empresaId, string nombreCliente, string? telefono, string? email, DateTimeOffset fechaHora, int duracionMinutos, int comensales, Guid? mesaId, string? notas, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        NombreCliente = nombreCliente;
        Telefono = telefono;
        Email = email;
        FechaHora = fechaHora;
        DuracionMinutos = duracionMinutos;
        Comensales = comensales;
        MesaId = mesaId;
        Notas = notas;
        Estado = EstadoReserva.Pendiente;
        CreadaEn = ahora;
        ActualizadaEn = ahora;
    }

    public string NombreCliente { get; private set; }

    public string? Telefono { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Momento de la reserva (inicio).</summary>
    public DateTimeOffset FechaHora { get; private set; }

    /// <summary>Duración estimada en minutos (para el hueco del calendario).</summary>
    public int DuracionMinutos { get; private set; }

    public int Comensales { get; private set; }

    /// <summary>Mesa asignada (opcional; referencia a Hostelería).</summary>
    public Guid? MesaId { get; private set; }

    public string? Notas { get; private set; }

    public EstadoReserva Estado { get; private set; }

    /// <summary>Comanda abierta al sentar la reserva (si se abrió una); nulo en otro caso.</summary>
    public Guid? ComandaId { get; private set; }

    /// <summary>Momento en que se envió el correo de recordatorio; nulo si aún no se ha enviado.</summary>
    public DateTimeOffset? RecordatorioEnviadoEn { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    /// <summary>Momento de fin (inicio + duración), usado para el calendario.</summary>
    public DateTimeOffset FechaHoraFin => FechaHora.AddMinutes(DuracionMinutos);

    /// <summary>Una reserva solo puede editarse mientras no se ha sentado, cancelado o marcado no-show.</summary>
    public bool EsModificable => Estado is EstadoReserva.Pendiente or EstadoReserva.Confirmada;

    public static Resultado<Reserva> Crear(
        Guid empresaId, string? nombreCliente, string? telefono, string? email, DateTimeOffset fechaHora, int duracionMinutos, int comensales, Guid? mesaId, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombreCliente, ref telefono, ref email, ref duracionMinutos, comensales, ref notas);
        if (error is not null)
        {
            return Resultado.Fallo<Reserva>(error);
        }

        var reserva = new Reserva(Guid.NewGuid(), empresaId, nombreCliente!.Trim(), telefono, email, fechaHora, duracionMinutos, comensales, mesaId, notas, reloj.AhoraUtc);
        reserva.RegistrarEvento(new ReservaCreada(reserva.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(reserva);
    }

    public Resultado Actualizar(string? nombreCliente, string? telefono, string? email, DateTimeOffset fechaHora, int duracionMinutos, int comensales, Guid? mesaId, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (!EsModificable)
        {
            return Resultado.Fallo(Error.Conflicto("reserva.no_modificable", "Solo se puede editar una reserva pendiente o confirmada."));
        }

        var error = Validar(nombreCliente, ref telefono, ref email, ref duracionMinutos, comensales, ref notas);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        NombreCliente = nombreCliente!.Trim();
        Telefono = telefono;
        Email = email;
        FechaHora = fechaHora;
        DuracionMinutos = duracionMinutos;
        Comensales = comensales;
        MesaId = mesaId;
        Notas = notas;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public Resultado Confirmar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (Estado != EstadoReserva.Pendiente)
        {
            return Resultado.Fallo(Error.Conflicto("reserva.no_pendiente", "Solo se puede confirmar una reserva pendiente."));
        }

        Estado = EstadoReserva.Confirmada;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public Resultado Cancelar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (!EsModificable)
        {
            return Resultado.Fallo(Error.Conflicto("reserva.no_cancelable", "Solo se puede cancelar una reserva pendiente o confirmada."));
        }

        Estado = EstadoReserva.Cancelada;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public Resultado MarcarNoShow(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (!EsModificable)
        {
            return Resultado.Fallo(Error.Conflicto("reserva.no_noshow", "Solo se puede marcar como no presentada una reserva pendiente o confirmada."));
        }

        Estado = EstadoReserva.NoShow;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca la reserva como sentada, ligándola a la comanda abierta (si se abrió alguna).</summary>
    public Resultado Sentar(Guid? comandaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (!EsModificable)
        {
            return Resultado.Fallo(Error.Conflicto("reserva.no_sentable", "Solo se puede sentar una reserva pendiente o confirmada."));
        }

        Estado = EstadoReserva.Sentada;
        ComandaId = comandaId;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Marca que ya se ha enviado el recordatorio (para no repetirlo).</summary>
    public void MarcarRecordatorioEnviado(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        RecordatorioEnviadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, ref string? telefono, ref string? email, ref int duracionMinutos, int comensales, ref string? notas)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("reserva.nombre_vacio", "El nombre del cliente es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("reserva.nombre_largo", "El nombre del cliente es demasiado largo.");
        }

        if (comensales <= 0 || comensales > ComensalesMaximo)
        {
            return Error.Validacion("reserva.comensales_invalidos", "El número de comensales no es válido.");
        }

        if (duracionMinutos <= 0)
        {
            duracionMinutos = 120;
        }

        telefono = Normalizar(telefono, LongitudMaximaTelefono);
        email = Normalizar(email, LongitudMaximaEmail);
        if (email is not null && !email.Contains('@', StringComparison.Ordinal))
        {
            return Error.Validacion("reserva.email_invalido", "El correo no es válido.");
        }

        notas = Normalizar(notas, LongitudMaximaNotas);
        return null;
    }

    private static string? Normalizar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var v = valor.Trim();
        return v.Length > max ? v[..max] : v;
    }
}

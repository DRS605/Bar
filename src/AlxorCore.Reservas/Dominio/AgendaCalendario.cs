using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Reservas.Dominio;

/// <summary>
/// Enlace secreto que permite <b>suscribirse</b> a la agenda de reservas de una empresa como
/// calendario iCalendar (Google Calendar, Apple, Outlook…) sin necesidad de iniciar sesión. El
/// <see cref="Token"/> es la credencial: quien lo tiene puede leer el calendario, por eso se puede
/// regenerar. No es una entidad multiempresa (es el propio token el que resuelve la empresa), de modo
/// que el endpoint público pueda localizarla sin un contexto de empresa previo.
/// </summary>
public sealed class AgendaCalendario : RaizAgregado<Guid>
{
    private AgendaCalendario(Guid id)
        : base(id)
    {
        Token = null!;
    }

    private AgendaCalendario(Guid id, Guid empresaId, string token)
        : base(id)
    {
        EmpresaId = empresaId;
        Token = token;
    }

    /// <summary>Empresa a la que pertenece la agenda.</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Token secreto del enlace de suscripción.</summary>
    public string Token { get; private set; }

    public static AgendaCalendario Crear(Guid empresaId) =>
        new(Guid.NewGuid(), empresaId, NuevoToken());

    /// <summary>Genera un token nuevo (invalida el enlace anterior).</summary>
    public void Regenerar() => Token = NuevoToken();

    private static string NuevoToken() => (Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
}

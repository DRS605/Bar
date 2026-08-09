using AlxorCore.Documentos.Aplicacion;

namespace AlxorCore.IntegrationTests;

/// <summary>Implementación de <see cref="IServicioCorreo"/> que guarda los mensajes en memoria.</summary>
public sealed class CorreoDePrueba : IServicioCorreo
{
    private readonly List<MensajeCorreo> _mensajes = new();
    private readonly object _candado = new();

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        lock (_candado)
        {
            _mensajes.Add(mensaje);
        }

        return Task.CompletedTask;
    }

    /// <summary>Número total de mensajes enviados.</summary>
    public int Total
    {
        get { lock (_candado) { return _mensajes.Count; } }
    }

    /// <summary>Mensajes enviados a un destinatario concreto.</summary>
    public IReadOnlyList<MensajeCorreo> Para(string email)
    {
        lock (_candado)
        {
            return _mensajes.Where(m => string.Equals(m.Para, email, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public void Limpiar()
    {
        lock (_candado)
        {
            _mensajes.Clear();
        }
    }
}

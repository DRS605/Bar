using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Dominio.Eventos;

namespace AlxorCore.Organizacion.Dominio;

/// <summary>
/// Serie de numeración de documentos de una empresa. Garantiza la asignación de números
/// <b>correlativos y sin huecos</b> por (empresa + tipo de documento + ejercicio + prefijo).
/// La atomicidad de la asignación la asegura la persistencia (bloqueo de fila).
/// </summary>
public sealed class SerieNumeracion : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaPrefijo = 10;

    /// <summary>Prefijo de la serie de facturas por defecto (creada de forma perezosa al facturar).</summary>
    public const string PrefijoFacturaPorDefecto = "FA";

    private SerieNumeracion(Guid id)
        : base(id, Guid.Empty)
    {
        Prefijo = null!;
    }

    private SerieNumeracion(Guid id, Guid empresaId, TipoDocumento tipoDocumento, int ejercicio, string prefijo, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        TipoDocumento = tipoDocumento;
        Ejercicio = ejercicio;
        Prefijo = prefijo;
        SiguienteNumero = 1;
        CreadoEn = ahora;
    }

    public TipoDocumento TipoDocumento { get; private set; }

    public int Ejercicio { get; private set; }

    public string Prefijo { get; private set; }

    /// <summary>Próximo número a asignar (empieza en 1).</summary>
    public long SiguienteNumero { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Resultado<SerieNumeracion> Crear(
        Guid empresaId,
        TipoDocumento tipoDocumento,
        int ejercicio,
        string? prefijo,
        IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var pref = (prefijo ?? string.Empty).Trim().ToUpperInvariant();
        if (pref.Length == 0)
        {
            return Resultado.Fallo<SerieNumeracion>(Error.Validacion("serie.prefijo_vacio", "El prefijo de la serie es obligatorio."));
        }

        if (pref.Length > LongitudMaximaPrefijo)
        {
            return Resultado.Fallo<SerieNumeracion>(Error.Validacion("serie.prefijo_largo", "El prefijo de la serie es demasiado largo."));
        }

        if (ejercicio is < 2000 or > 2100)
        {
            return Resultado.Fallo<SerieNumeracion>(Error.Validacion("serie.ejercicio_invalido", "El ejercicio no es válido."));
        }

        var serie = new SerieNumeracion(Guid.NewGuid(), empresaId, tipoDocumento, ejercicio, pref, reloj.AhoraUtc);
        serie.RegistrarEvento(new SerieCreada(serie.Id, empresaId, pref, ejercicio, reloj.AhoraUtc));
        return Resultado.Ok(serie);
    }

    /// <summary>
    /// Asigna el siguiente número correlativo y avanza el contador. Debe invocarse dentro de una
    /// transacción con la fila de la serie bloqueada para garantizar la ausencia de huecos.
    /// </summary>
    public NumeroDocumento AsignarSiguiente()
    {
        var numero = new NumeroDocumento(Prefijo, Ejercicio, SiguienteNumero);
        SiguienteNumero++;
        return numero;
    }
}

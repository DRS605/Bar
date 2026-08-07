using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>
/// Mesa (o barra) de un local de hostelería. Es un elemento de configuración del salón: sobre una
/// mesa se abren <see cref="Comanda"/>s. La ocupación no se guarda aquí, se deduce de si la mesa
/// tiene una comanda abierta, para no duplicar estado entre agregados.
/// </summary>
public sealed class Mesa : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 60;
    public const int LongitudMaximaZona = 60;
    public const int CapacidadMaxima = 500;

    private Mesa(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Mesa(Guid id, Guid empresaId, string nombre, string? zona, int capacidad, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        Zona = zona;
        Capacidad = capacidad;
        Activa = true;
        CreadaEn = ahora;
        ActualizadaEn = ahora;
    }

    /// <summary>Nombre visible de la mesa (por ejemplo, «Mesa 1», «Barra», «Terraza 3»).</summary>
    public string Nombre { get; private set; }

    /// <summary>Zona o sala a la que pertenece la mesa (opcional): «Salón», «Terraza», «Barra»…</summary>
    public string? Zona { get; private set; }

    /// <summary>Número de comensales para los que está preparada la mesa.</summary>
    public int Capacidad { get; private set; }

    /// <summary>Si la mesa está en uso. Las mesas que se retiran se desactivan (no se borran).</summary>
    public bool Activa { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public static Resultado<Mesa> Crear(Guid empresaId, string? nombre, string? zona, int capacidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, ref zona, capacidad);
        if (error is not null)
        {
            return Resultado.Fallo<Mesa>(error);
        }

        return Resultado.Ok(new Mesa(Guid.NewGuid(), empresaId, nombre!.Trim(), zona, capacidad, reloj.AhoraUtc));
    }

    public Resultado Actualizar(string? nombre, string? zona, int capacidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, ref zona, capacidad);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        Zona = zona;
        Capacidad = capacidad;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        Activa = false;
        ActualizadaEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, ref string? zona, int capacidad)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("mesa.nombre_vacio", "El nombre de la mesa es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("mesa.nombre_largo", "El nombre de la mesa es demasiado largo.");
        }

        zona = string.IsNullOrWhiteSpace(zona) ? null : zona.Trim();
        if (zona is not null && zona.Length > LongitudMaximaZona)
        {
            return Error.Validacion("mesa.zona_larga", "El nombre de la zona es demasiado largo.");
        }

        if (capacidad < 0 || capacidad > CapacidadMaxima)
        {
            return Error.Validacion("mesa.capacidad_invalida", "La capacidad de la mesa no es válida.");
        }

        return null;
    }
}

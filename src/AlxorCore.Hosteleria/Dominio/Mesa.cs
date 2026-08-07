using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Forma con la que se dibuja una mesa en el plano del local.</summary>
public enum FormaMesa
{
    /// <summary>Mesa cuadrada.</summary>
    Cuadrada = 1,

    /// <summary>Mesa redonda.</summary>
    Redonda = 2,

    /// <summary>Elemento alargado: típicamente la barra.</summary>
    Rectangular = 3,
}

/// <summary>
/// Mesa (o barra) de un local de hostelería. Es un elemento de configuración del salón: sobre una
/// mesa se abren <see cref="Comanda"/>s. La ocupación no se guarda aquí, se deduce de si la mesa
/// tiene una comanda abierta, para no duplicar estado entre agregados. Guarda además su posición
/// (<see cref="PosX"/>, <see cref="PosY"/>) y su <see cref="Forma"/> para poder dibujar el plano.
/// </summary>
public sealed class Mesa : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 60;
    public const int LongitudMaximaZona = 60;
    public const int CapacidadMaxima = 500;

    /// <summary>Lado del lienzo del plano (unidades abstractas): las posiciones viven en [0, <see cref="Lienzo"/>].</summary>
    public const double Lienzo = 1000d;

    private Mesa(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
    }

    private Mesa(Guid id, Guid empresaId, string nombre, string? zona, int capacidad, FormaMesa forma, double posX, double posY, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        Zona = zona;
        Capacidad = capacidad;
        Forma = forma;
        PosX = Acotar(posX);
        PosY = Acotar(posY);
        Activa = true;
        CreadaEn = ahora;
        ActualizadaEn = ahora;
    }

    /// <summary>Forma con la que se dibuja la mesa en el plano.</summary>
    public FormaMesa Forma { get; private set; }

    /// <summary>Posición horizontal en el plano (0 = izquierda; ver <see cref="Lienzo"/>).</summary>
    public double PosX { get; private set; }

    /// <summary>Posición vertical en el plano (0 = arriba; ver <see cref="Lienzo"/>).</summary>
    public double PosY { get; private set; }

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

    public static Resultado<Mesa> Crear(Guid empresaId, string? nombre, string? zona, int capacidad, IReloj reloj, FormaMesa forma = FormaMesa.Cuadrada, double posX = 0, double posY = 0)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, ref zona, capacidad);
        if (error is not null)
        {
            return Resultado.Fallo<Mesa>(error);
        }

        return Resultado.Ok(new Mesa(Guid.NewGuid(), empresaId, nombre!.Trim(), zona, capacidad, forma, posX, posY, reloj.AhoraUtc));
    }

    public Resultado Actualizar(string? nombre, string? zona, int capacidad, IReloj reloj, FormaMesa forma = FormaMesa.Cuadrada)
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
        Forma = forma;
        ActualizadaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    /// <summary>Coloca la mesa en el plano (las coordenadas se acotan al lienzo).</summary>
    public void Colocar(double posX, double posY, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        PosX = Acotar(posX);
        PosY = Acotar(posY);
        ActualizadaEn = reloj.AhoraUtc;
    }

    private static double Acotar(double valor) => double.IsFinite(valor) ? Math.Clamp(valor, 0d, Lienzo) : 0d;

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

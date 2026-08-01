using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Terceros.Dominio;

/// <summary>Se ha creado un cliente.</summary>
public sealed record ClienteCreado(Guid ClienteId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Cliente de una empresa. Guarda los datos fiscales necesarios para facturarle. El identificador
/// fiscal (<see cref="NifFiscal"/>) es opcional y se acepta como texto: un cliente puede ser
/// extranjero y no tener NIF español.
/// </summary>
public sealed class Cliente : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const decimal IrpfMaximo = 60m;

    private Cliente(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
        Direccion = Direccion.Vacia;
    }

    private Cliente(Guid id, Guid empresaId, string nombre, string? nifFiscal, string? email, Direccion direccion, decimal irpf, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        NifFiscal = nifFiscal;
        Email = email;
        Direccion = direccion;
        PorcentajeIrpfDefecto = irpf;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public string? NifFiscal { get; private set; }

    public string? Email { get; private set; }

    public Direccion Direccion { get; private set; }

    /// <summary>Retención de IRPF por defecto (0–60 %). Se prerrellena al facturar.</summary>
    public decimal PorcentajeIrpfDefecto { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Cliente> Crear(
        Guid empresaId,
        string? nombre,
        string? nifFiscal,
        string? email,
        Direccion direccion,
        decimal porcentajeIrpfDefecto,
        IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, porcentajeIrpfDefecto);
        if (error is not null)
        {
            return Resultado.Fallo<Cliente>(error);
        }

        var cliente = new Cliente(
            Guid.NewGuid(), empresaId, nombre!.Trim(), Normalizar(nifFiscal), Normalizar(email), direccion, porcentajeIrpfDefecto, reloj.AhoraUtc);
        cliente.RegistrarEvento(new ClienteCreado(cliente.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(cliente);
    }

    public Resultado Actualizar(string? nombre, string? nifFiscal, string? email, Direccion direccion, decimal porcentajeIrpfDefecto, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, porcentajeIrpfDefecto);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Nombre = nombre!.Trim();
        NifFiscal = Normalizar(nifFiscal);
        Email = Normalizar(email);
        Direccion = direccion;
        PorcentajeIrpfDefecto = porcentajeIrpfDefecto;
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, decimal irpf)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("cliente.nombre_vacio", "El nombre del cliente es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("cliente.nombre_largo", "El nombre del cliente es demasiado largo.");
        }

        if (irpf is < 0 or > IrpfMaximo)
        {
            return Error.Validacion("cliente.irpf_invalido", "El porcentaje de IRPF no es válido.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

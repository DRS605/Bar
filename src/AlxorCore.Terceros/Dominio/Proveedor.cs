using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Terceros.Dominio;

/// <summary>Se ha creado un proveedor.</summary>
public sealed record ProveedorCreado(Guid ProveedorId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Forma de pago habitual a un proveedor.</summary>
public enum FormaPago
{
    /// <summary>Sin especificar.</summary>
    NoIndicada = 0,

    /// <summary>Transferencia bancaria.</summary>
    Transferencia = 1,

    /// <summary>Domiciliación bancaria (recibo).</summary>
    Domiciliacion = 2,

    /// <summary>Efectivo.</summary>
    Efectivo = 3,

    /// <summary>Tarjeta.</summary>
    Tarjeta = 4,

    /// <summary>Pagaré.</summary>
    Pagare = 5,

    /// <summary>Confirming.</summary>
    Confirming = 6,

    /// <summary>Otra forma de pago.</summary>
    Otro = 7,
}

/// <summary>
/// Proveedor de una empresa: a quién se le compra o de quién se reciben gastos. Guarda sus datos
/// fiscales, incluida la retención de IRPF por defecto (habitual en proveedores autónomos).
/// </summary>
public sealed class Proveedor : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;
    public const decimal IrpfMaximo = 60m;

    private Proveedor(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
        Direccion = Direccion.Vacia;
    }

    private Proveedor(Guid id, Guid empresaId, string nombre, string? nifFiscal, string? email, Direccion direccion, decimal irpf, FormaPago formaPago, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Nombre = nombre;
        NifFiscal = nifFiscal;
        Email = email;
        Direccion = direccion;
        PorcentajeIrpfDefecto = irpf;
        FormaPago = formaPago;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string Nombre { get; private set; }

    public string? NifFiscal { get; private set; }

    public string? Email { get; private set; }

    public Direccion Direccion { get; private set; }

    /// <summary>Retención de IRPF por defecto (0–60 %). Se prerrellena al registrar un gasto suyo.</summary>
    public decimal PorcentajeIrpfDefecto { get; private set; }

    /// <summary>Forma de pago habitual a este proveedor.</summary>
    public FormaPago FormaPago { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Proveedor> Crear(
        Guid empresaId, string? nombre, string? nifFiscal, string? email, Direccion direccion, decimal porcentajeIrpfDefecto, FormaPago formaPago, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, porcentajeIrpfDefecto);
        if (error is not null)
        {
            return Resultado.Fallo<Proveedor>(error);
        }

        var proveedor = new Proveedor(
            Guid.NewGuid(), empresaId, nombre!.Trim(), Normalizar(nifFiscal), Normalizar(email), direccion, porcentajeIrpfDefecto, formaPago, reloj.AhoraUtc);
        proveedor.RegistrarEvento(new ProveedorCreado(proveedor.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(proveedor);
    }

    public Resultado Actualizar(string? nombre, string? nifFiscal, string? email, Direccion direccion, decimal porcentajeIrpfDefecto, FormaPago formaPago, IReloj reloj)
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
        FormaPago = formaPago;
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
            return Error.Validacion("proveedor.nombre_vacio", "El nombre del proveedor es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("proveedor.nombre_largo", "El nombre del proveedor es demasiado largo.");
        }

        if (irpf is < 0 or > IrpfMaximo)
        {
            return Error.Validacion("proveedor.irpf_invalido", "El porcentaje de IRPF no es válido.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

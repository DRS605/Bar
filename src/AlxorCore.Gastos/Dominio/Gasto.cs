using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Gastos.Dominio;

/// <summary>Estado de un gasto.</summary>
public enum EstadoGasto
{
    Registrado = 1,
    Anulado = 2,
}

/// <summary>Se ha registrado un gasto.</summary>
public sealed record GastoRegistrado(Guid GastoId, Guid EmpresaId, decimal Total, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Gasto (factura recibida simplificada) de una empresa. El proveedor se guarda como texto libre
/// (en el MVP no hay entidad Proveedor). Calcula el IVA soportado y la retención de IRPF.
/// </summary>
public sealed class Gasto : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaConcepto = 200;
    public const decimal IrpfMaximo = 60m;

    private Gasto(Guid id)
        : base(id, Guid.Empty)
    {
        Concepto = null!;
        CodigoIva = null!;
    }

    private Gasto(Guid id, Guid empresaId, Guid? proveedorId, string? proveedorTexto, string concepto, DateOnly fecha, decimal baseImponible, string codigoIva, decimal porcentajeIva, decimal porcentajeIrpf, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        ProveedorId = proveedorId;
        ProveedorTexto = proveedorTexto;
        Concepto = concepto;
        Fecha = fecha;
        BaseImponible = baseImponible;
        CodigoIva = codigoIva;
        PorcentajeIva = porcentajeIva;
        CuotaIva = Redondeo.Dos(baseImponible * porcentajeIva / 100m);
        PorcentajeIrpf = porcentajeIrpf;
        RetencionIrpf = Redondeo.Dos(baseImponible * porcentajeIrpf / 100m);
        Total = Redondeo.Dos(BaseImponible + CuotaIva - RetencionIrpf);
        Estado = EstadoGasto.Registrado;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Proveedor asociado (opcional; permite gastos rápidos sin proveedor fijo).</summary>
    public Guid? ProveedorId { get; private set; }

    /// <summary>Nombre del proveedor (copia del proveedor asociado, o texto libre).</summary>
    public string? ProveedorTexto { get; private set; }

    public string Concepto { get; private set; }

    public DateOnly Fecha { get; private set; }

    public decimal BaseImponible { get; private set; }

    public string CodigoIva { get; private set; }

    public decimal PorcentajeIva { get; private set; }

    public decimal CuotaIva { get; private set; }

    public decimal PorcentajeIrpf { get; private set; }

    public decimal RetencionIrpf { get; private set; }

    public decimal Total { get; private set; }

    public EstadoGasto Estado { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Gasto> Registrar(
        Guid empresaId, Guid? proveedorId, string? proveedorTexto, string? concepto, DateOnly fecha, decimal baseImponible, string? codigoIva, decimal porcentajeIrpf, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (string.IsNullOrWhiteSpace(concepto))
        {
            return Resultado.Fallo<Gasto>(Error.Validacion("gasto.concepto_vacio", "El concepto es obligatorio."));
        }

        if (concepto.Trim().Length > LongitudMaximaConcepto)
        {
            return Resultado.Fallo<Gasto>(Error.Validacion("gasto.concepto_largo", "El concepto es demasiado largo."));
        }

        if (baseImponible < 0)
        {
            return Resultado.Fallo<Gasto>(Error.Validacion("gasto.base_negativa", "La base no puede ser negativa."));
        }

        if (porcentajeIrpf is < 0 or > IrpfMaximo)
        {
            return Resultado.Fallo<Gasto>(Error.Validacion("gasto.irpf_invalido", "El porcentaje de IRPF no es válido."));
        }

        var impuesto = Impuesto.PorCodigoImpuesto(string.IsNullOrWhiteSpace(codigoIva) ? Impuesto.IvaGeneral.Codigo : codigoIva);
        if (impuesto.EsFallo)
        {
            return Resultado.Fallo<Gasto>(impuesto.Error);
        }

        var gasto = new Gasto(
            Guid.NewGuid(), empresaId, proveedorId, Normalizar(proveedorTexto), concepto.Trim(), fecha, Redondeo.Dos(baseImponible),
            impuesto.Valor.Codigo, impuesto.Valor.Porcentaje, porcentajeIrpf, reloj.AhoraUtc);
        gasto.RegistrarEvento(new GastoRegistrado(gasto.Id, empresaId, gasto.Total, reloj.AhoraUtc));
        return Resultado.Ok(gasto);
    }

    public void Anular(IReloj reloj)
    {
        Estado = EstadoGasto.Anulado;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

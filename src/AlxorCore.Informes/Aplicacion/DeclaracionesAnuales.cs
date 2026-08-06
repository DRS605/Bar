using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Terceros.Aplicacion;

namespace AlxorCore.Informes.Aplicacion;

/// <summary>
/// Resumen del <b>modelo 390</b> (declaración-resumen anual del IVA): es la suma de los cuatro
/// trimestres del modelo 303 del ejercicio. IVA devengado (repercutido en las facturas emitidas del
/// año) menos IVA deducible (soportado en los gastos del año).
/// </summary>
public sealed record Modelo390Dto(
    int Anio,
    decimal IvaDevengadoBase, decimal IvaDevengadoCuota,
    decimal IvaDeducibleBase, decimal IvaDeducibleCuota,
    decimal Resultado);

/// <summary>Un tercero (cliente o proveedor) con su volumen anual de operaciones para el modelo 347.</summary>
public sealed record Modelo347LineaDto(string Clave, string Nombre, string? Nif, string Sentido, decimal ImporteAnual);

/// <summary>
/// Resumen del <b>modelo 347</b> (declaración anual de operaciones con terceros): relación de
/// clientes y proveedores con los que el volumen de operaciones del año (IVA incluido) ha superado
/// el umbral legal de <b>3.005,06 €</b>.
/// </summary>
public sealed record Modelo347Dto(
    int Anio, decimal Umbral,
    IReadOnlyList<Modelo347LineaDto> Clientes,
    IReadOnlyList<Modelo347LineaDto> Proveedores);

/// <summary>Declaraciones anuales de una empresa: modelo 390 (IVA) y modelo 347 (operaciones con terceros).</summary>
public sealed record DeclaracionAnualDto(Modelo390Dto Modelo390, Modelo347Dto Modelo347);

/// <summary>
/// Caso de uso: calcula las declaraciones anuales (390 y 347) a partir de las facturas emitidas, los
/// gastos y los datos de los proveedores. Es una <b>ayuda informativa</b> para preparar la
/// declaración con la gestoría, no un envío oficial a la AEAT.
/// </summary>
public sealed class GenerarDeclaracionAnual
{
    /// <summary>Umbral legal del modelo 347: 3.005,06 € (IVA incluido) de volumen anual con un tercero.</summary>
    public const decimal Umbral347 = 3005.06m;

    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;
    private readonly IConsultaProveedores _proveedores;

    public GenerarDeclaracionAnual(IConsultaFacturas facturas, IConsultaGastos gastos, IConsultaProveedores proveedores)
    {
        _facturas = facturas;
        _gastos = gastos;
        _proveedores = proveedores;
    }

    public async Task<DeclaracionAnualDto> EjecutarAsync(Guid empresaId, int anio, CancellationToken ct = default)
    {
        var desde = new DateOnly(anio, 1, 1);
        var hasta = new DateOnly(anio, 12, 31);

        var facturas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var proveedores = await _proveedores.ListarAsync(empresaId, true, ct).ConfigureAwait(false);

        // Solo cuentan las facturas realmente emitidas del ejercicio (se excluyen anuladas y ya
        // rectificadas, cuya rectificativa aporta los importes corregidos).
        var emitidas = facturas
            .Where(f => f.Estado == "Emitida" && f.FechaEmision >= desde && f.FechaEmision <= hasta)
            .ToList();
        var gastosAnio = gastos.Where(g => g.Fecha >= desde && g.Fecha <= hasta).ToList();

        return new DeclaracionAnualDto(
            Calcular390(anio, emitidas, gastosAnio),
            Calcular347(anio, emitidas, gastosAnio, proveedores));
    }

    private static Modelo390Dto Calcular390(int anio, IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<GastoDto> gastos)
    {
        var devBase = Redondeo.Dos(facturas.Sum(f => f.BaseImponible));
        var devCuota = Redondeo.Dos(facturas.Sum(f => f.CuotaIva));
        var dedBase = Redondeo.Dos(gastos.Sum(g => g.BaseImponible));
        var dedCuota = Redondeo.Dos(gastos.Sum(g => g.CuotaIva));
        return new Modelo390Dto(anio, devBase, devCuota, dedBase, dedCuota, Redondeo.Dos(devCuota - dedCuota));
    }

    private static Modelo347Dto Calcular347(
        int anio, IReadOnlyList<FacturaResumen> facturas, IReadOnlyList<GastoDto> gastos, IReadOnlyList<ProveedorDto> proveedores)
    {
        // Clientes: se agrupan por NIF si lo hay; si no, por nombre (las facturas congelan ambos).
        var clientes = facturas
            .GroupBy(f => f.ClienteNif is { Length: > 0 } nif ? "nif:" + nif : "nom:" + f.ClienteNombre)
            .Select(g =>
            {
                var primera = g.First();
                return new Modelo347LineaDto(g.Key, primera.ClienteNombre, primera.ClienteNif, "Cliente", Redondeo.Dos(g.Sum(f => f.Total)));
            })
            .Where(l => l.ImporteAnual > Umbral347)
            .OrderByDescending(l => l.ImporteAnual)
            .ToList();

        // Proveedores: se agrupan por su id (resolviendo nombre y NIF del maestro de proveedores);
        // los gastos sin proveedor asociado se agrupan por el texto libre.
        var mapa = proveedores.ToDictionary(p => p.Id);
        var proveedoresLinea = gastos
            .GroupBy(g => g.ProveedorId is { } id ? "id:" + id : "txt:" + (g.ProveedorTexto ?? "Sin proveedor"))
            .Select(g =>
            {
                var primero = g.First();
                string nombre;
                string? nif;
                if (primero.ProveedorId is { } id && mapa.TryGetValue(id, out var prov))
                {
                    nombre = prov.Nombre;
                    nif = prov.NifFiscal;
                }
                else
                {
                    nombre = primero.ProveedorTexto ?? "Sin proveedor";
                    nif = null;
                }

                return new Modelo347LineaDto(g.Key, nombre, nif, "Proveedor", Redondeo.Dos(g.Sum(x => x.Total)));
            })
            .Where(l => l.ImporteAnual > Umbral347)
            .OrderByDescending(l => l.ImporteAnual)
            .ToList();

        return new Modelo347Dto(anio, Umbral347, clientes, proveedoresLinea);
    }
}

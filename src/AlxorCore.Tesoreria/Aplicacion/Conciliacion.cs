using System.Globalization;
using AlxorCore.Facturacion.Aplicacion;
using AlxorCore.Gastos.Aplicacion;
using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Tesoreria.Dominio;

namespace AlxorCore.Tesoreria.Aplicacion;

/// <summary>Un apunte del extracto bancario (fecha, importe con signo y concepto).</summary>
public sealed record ApunteExtracto(DateOnly Fecha, decimal Importe, string Concepto);

/// <summary>Extracto bancario leído de un fichero Norma 43 (Cuaderno 43 / CSB43).</summary>
public sealed record ExtractoBancario(
    string Cuenta, DateOnly? Desde, DateOnly? Hasta, decimal? SaldoInicial, decimal? SaldoFinal,
    IReadOnlyList<ApunteExtracto> Apuntes);

/// <summary>
/// Lector del formato bancario español <b>Norma 43</b> (AEB/CSB Cuaderno 43): registros de 80
/// caracteres, importes en 14 dígitos (2 decimales implícitos) y fechas <c>AAMMDD</c>. Extrae la
/// cabecera de cuenta (11), los apuntes (22) con sus conceptos ampliados (23) y el saldo final (33).
/// </summary>
public static class ParserNorma43
{
    public static Resultado<ExtractoBancario> Parsear(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return Resultado.Fallo<ExtractoBancario>(Error.Validacion("n43.vacio", "El fichero está vacío."));
        }

        var lineas = contenido.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string cuenta = string.Empty;
        DateOnly? desde = null, hasta = null;
        decimal? saldoInicial = null, saldoFinal = null;
        var apuntes = new List<ApunteExtracto>();
        var conceptos = new List<string>();
        var hayApunte = false;
        DateOnly fechaApunte = default;
        decimal importeApunte = 0m;
        string conceptoComun = string.Empty;

        void CerrarApunte()
        {
            if (!hayApunte)
            {
                return;
            }

            var concepto = conceptos.Count > 0
                ? string.Join(" ", conceptos).Trim()
                : conceptoComun;
            apuntes.Add(new ApunteExtracto(fechaApunte, importeApunte, string.IsNullOrWhiteSpace(concepto) ? "Movimiento bancario" : concepto));
            conceptos.Clear();
            hayApunte = false;
        }

        foreach (var linea in lineas)
        {
            if (linea.Length < 2)
            {
                continue;
            }

            var codigo = linea[..2];
            switch (codigo)
            {
                case "11":
                    if (linea.Length >= 47)
                    {
                        cuenta = Trozo(linea, 2, 18); // entidad(4)+oficina(4)+cuenta(10)
                        desde = LeerFecha(linea, 20);
                        hasta = LeerFecha(linea, 26);
                        saldoInicial = LeerImporte(linea, 32, 33);
                    }

                    break;

                case "22":
                    CerrarApunte();
                    if (linea.Length >= 42)
                    {
                        hayApunte = true;
                        fechaApunte = LeerFecha(linea, 10) ?? hasta ?? desde ?? default;
                        importeApunte = LeerImporte(linea, 27, 28) ?? 0m;
                        conceptoComun = Trozo(linea, 22, 2);
                    }

                    break;

                case "23":
                    if (hayApunte && linea.Length > 4)
                    {
                        var texto = Trozo(linea, 4, Math.Min(76, linea.Length - 4));
                        if (!string.IsNullOrWhiteSpace(texto))
                        {
                            conceptos.Add(texto);
                        }
                    }

                    break;

                case "33":
                    CerrarApunte();
                    if (linea.Length >= 59)
                    {
                        // Registro fin de cuenta: clave saldo final en la posición 59 (índice 58),
                        // importe del saldo final en 60-73 (índice 59).
                        saldoFinal = LeerImporte(linea, 58, 59);
                    }

                    break;

                case "88":
                    CerrarApunte();
                    break;

                default:
                    break;
            }
        }

        CerrarApunte();

        if (apuntes.Count == 0)
        {
            return Resultado.Fallo<ExtractoBancario>(Error.Validacion("n43.sin_apuntes", "El fichero no contiene apuntes reconocibles (formato Norma 43)."));
        }

        return Resultado.Ok(new ExtractoBancario(cuenta.Trim(), desde, hasta, saldoInicial, saldoFinal, apuntes));
    }

    private static string Trozo(string linea, int inicio, int longitud) =>
        inicio >= linea.Length ? string.Empty : linea.Substring(inicio, Math.Min(longitud, linea.Length - inicio)).Trim();

    private static DateOnly? LeerFecha(string linea, int inicio)
    {
        var t = Trozo(linea, inicio, 6);
        if (t.Length != 6 || !int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return null;
        }

        var anio = 2000 + int.Parse(t.AsSpan(0, 2), CultureInfo.InvariantCulture);
        var mes = int.Parse(t.AsSpan(2, 2), CultureInfo.InvariantCulture);
        var dia = int.Parse(t.AsSpan(4, 2), CultureInfo.InvariantCulture);
        if (mes is < 1 or > 12 || dia is < 1 or > 31)
        {
            return null;
        }

        return new DateOnly(anio, mes, dia);
    }

    /// <summary>
    /// Lee un importe de 14 dígitos (2 decimales implícitos) precedido de la clave debe/haber
    /// (1 = debe/cargo = negativo; 2 = haber/abono = positivo).
    /// </summary>
    private static decimal? LeerImporte(string linea, int inicioClave, int inicioImporte)
    {
        var claveTxt = Trozo(linea, inicioClave, 1);
        var importeTxt = Trozo(linea, inicioImporte, 14);
        if (importeTxt.Length == 0 || !long.TryParse(importeTxt, NumberStyles.None, CultureInfo.InvariantCulture, out var bruto))
        {
            return null;
        }

        var valor = bruto / 100m;
        return claveTxt == "1" ? -valor : valor;
    }
}

/// <summary>Sugerencia de conciliación de un apunte con un documento pendiente.</summary>
public sealed record SugerenciaConciliacion(string Tipo, Guid DocumentoId, string Documento, decimal Pendiente);

/// <summary>Apunte del extracto con su posible casación automática.</summary>
public sealed record ApunteConciliadoDto(DateOnly Fecha, decimal Importe, string Concepto, SugerenciaConciliacion? Sugerencia);

/// <summary>Resultado de conciliar un extracto: sus apuntes con las casaciones sugeridas.</summary>
public sealed record ConciliacionDto(
    string Cuenta, DateOnly? Desde, DateOnly? Hasta, decimal? SaldoInicial, decimal? SaldoFinal,
    IReadOnlyList<ApunteConciliadoDto> Apuntes);

/// <summary>
/// Caso de uso: lee un extracto Norma 43 y propone, para cada apunte, la factura (abono) o el gasto
/// (cargo) pendiente cuyo importe pendiente coincide exactamente. El usuario confirma cada casación
/// registrando el cobro o el pago con los endpoints habituales.
/// </summary>
public sealed class ConciliarExtracto
{
    private readonly IConsultaFacturas _facturas;
    private readonly IConsultaGastos _gastos;
    private readonly IRepositorioMovimientos _movimientos;

    public ConciliarExtracto(IConsultaFacturas facturas, IConsultaGastos gastos, IRepositorioMovimientos movimientos)
    {
        _facturas = facturas;
        _gastos = gastos;
        _movimientos = movimientos;
    }

    public async Task<Resultado<ConciliacionDto>> EjecutarAsync(Guid empresaId, string? contenido, CancellationToken ct = default)
    {
        var extracto = ParserNorma43.Parsear(contenido);
        if (extracto.EsFallo)
        {
            return Resultado.Fallo<ConciliacionDto>(extracto.Error);
        }

        var pendientesCobro = await PendientesFacturasAsync(empresaId, ct).ConfigureAwait(false);
        var pendientesPago = await PendientesGastosAsync(empresaId, ct).ConfigureAwait(false);

        var apuntes = new List<ApunteConciliadoDto>();
        foreach (var apunte in extracto.Valor.Apuntes)
        {
            SugerenciaConciliacion? sugerencia = null;
            if (apunte.Importe > 0)
            {
                var idx = pendientesCobro.FindIndex(p => p.Pendiente == Redondeo.Dos(apunte.Importe));
                if (idx >= 0)
                {
                    var f = pendientesCobro[idx];
                    sugerencia = new SugerenciaConciliacion("Cobro", f.Id, f.Nombre, f.Pendiente);
                    pendientesCobro.RemoveAt(idx); // no reutilizar el mismo documento para dos apuntes
                }
            }
            else if (apunte.Importe < 0)
            {
                var idx = pendientesPago.FindIndex(p => p.Pendiente == Redondeo.Dos(-apunte.Importe));
                if (idx >= 0)
                {
                    var g = pendientesPago[idx];
                    sugerencia = new SugerenciaConciliacion("Pago", g.Id, g.Nombre, g.Pendiente);
                    pendientesPago.RemoveAt(idx);
                }
            }

            apuntes.Add(new ApunteConciliadoDto(apunte.Fecha, apunte.Importe, apunte.Concepto, sugerencia));
        }

        var e = extracto.Valor;
        return Resultado.Ok(new ConciliacionDto(e.Cuenta, e.Desde, e.Hasta, e.SaldoInicial, e.SaldoFinal, apuntes));
    }

    private async Task<List<(Guid Id, string Nombre, decimal Pendiente)>> PendientesFacturasAsync(Guid empresaId, CancellationToken ct)
    {
        var facturas = await _facturas.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var pendientes = new List<(Guid, string, decimal)>();
        foreach (var f in facturas.Where(f => f.Estado == "Emitida"))
        {
            var liquidado = await _movimientos.SumaAsync(TipoDocumentoTesoreria.Factura, f.Id, ct).ConfigureAwait(false);
            var pendiente = Redondeo.Dos(f.Total - liquidado);
            if (pendiente > 0)
            {
                pendientes.Add((f.Id, $"{f.NumeroCompleto} · {f.ClienteNombre}", pendiente));
            }
        }

        return pendientes;
    }

    private async Task<List<(Guid Id, string Nombre, decimal Pendiente)>> PendientesGastosAsync(Guid empresaId, CancellationToken ct)
    {
        var gastos = await _gastos.ListarAsync(empresaId, ct).ConfigureAwait(false);
        var pendientes = new List<(Guid, string, decimal)>();
        foreach (var g in gastos)
        {
            var liquidado = await _movimientos.SumaAsync(TipoDocumentoTesoreria.Gasto, g.Id, ct).ConfigureAwait(false);
            var pendiente = Redondeo.Dos(g.Total - liquidado);
            if (pendiente > 0)
            {
                pendientes.Add((g.Id, $"{g.Concepto}{(g.ProveedorTexto is { Length: > 0 } p ? " · " + p : string.Empty)}", pendiente));
            }
        }

        return pendientes;
    }
}

using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Estado de una comanda a lo largo de su ciclo de vida.</summary>
public enum EstadoComanda
{
    /// <summary>Abierta: se pueden añadir o quitar líneas. Es el estado de trabajo.</summary>
    Abierta = 1,

    /// <summary>Cobrada: se ha emitido el ticket y la mesa queda libre. Es inmutable.</summary>
    Cobrada = 2,

    /// <summary>Anulada: la comanda se cerró sin cobrar (error, mesa que se marcha sin consumir…).</summary>
    Anulada = 3,

    /// <summary>Juntada: se fundió en otra comanda (sus líneas pasaron a ella); libera su mesa sin ticket propio.</summary>
    Juntada = 4,
}

/// <summary>Forma de cobro de una comanda.</summary>
public enum MetodoCobro
{
    /// <summary>Pago en efectivo.</summary>
    Efectivo = 1,

    /// <summary>Pago con tarjeta.</summary>
    Tarjeta = 2,

    /// <summary>Otro medio (transferencia, vale…).</summary>
    Otro = 3,
}

/// <summary>Se ha abierto una comanda en una mesa.</summary>
public sealed record ComandaAbierta(Guid ComandaId, Guid EmpresaId, Guid MesaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Se ha cobrado una comanda (con el ticket generado).</summary>
public sealed record ComandaCobrada(Guid ComandaId, Guid EmpresaId, Guid MesaId, Guid FacturaId, decimal Total, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Una comanda se ha movido a otra mesa.</summary>
public sealed record ComandaMovida(Guid ComandaId, Guid EmpresaId, Guid MesaAnteriorId, Guid MesaNuevaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Una comanda se ha juntado en otra (sus líneas pasaron a la comanda destino).</summary>
public sealed record ComandaJuntada(Guid ComandaId, Guid EmpresaId, Guid MesaId, Guid ComandaDestinoId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Artículo (y cantidad) que se cobra en un ticket parcial al repartir la cuenta.</summary>
public sealed record ItemCobroParcial(Guid LineaId, decimal Cantidad);

/// <summary>Línea resuelta para emitir el ticket de un cobro parcial (precio e IVA congelados de la comanda).</summary>
public sealed record LineaCobroTicket(Guid ProductoId, string Descripcion, decimal Cantidad, decimal PrecioUnitario, string CodigoIva, decimal PorcentajeIva);

/// <summary>
/// Comanda (cuenta abierta) de una mesa. Es el agregado central del módulo Hostelería: acumula las
/// líneas de lo consumido mientras está <see cref="EstadoComanda.Abierta"/> y, al cobrarse, se
/// convierte en un ticket (factura simplificada) del módulo Facturación. Sus importes se recalculan
/// en cada cambio de línea y se congelan al cobrar.
/// </summary>
public sealed class Comanda : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNotas = 500;

    private readonly List<LineaComanda> _lineas = [];

    private Comanda(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private Comanda(Guid id, Guid empresaId, Guid mesaId, string? notas, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        MesaId = mesaId;
        Notas = notas;
        Estado = EstadoComanda.Abierta;
        AbiertaEn = ahora;
    }

    public Guid MesaId { get; private set; }

    public EstadoComanda Estado { get; private set; }

    /// <summary>Notas de la comanda (alergias, «sin hielo», comensal, etc.).</summary>
    public string? Notas { get; private set; }

    public DateTimeOffset AbiertaEn { get; private set; }

    /// <summary>Instante en el que se cobró o anuló la comanda; nulo mientras está abierta.</summary>
    public DateTimeOffset? CerradaEn { get; private set; }

    /// <summary>Base imponible acumulada de las líneas.</summary>
    public decimal BaseImponible { get; private set; }

    /// <summary>Cuota de IVA acumulada de las líneas.</summary>
    public decimal CuotaIva { get; private set; }

    /// <summary>Total a pagar (base + IVA, ya con el descuento aplicado si lo hay).</summary>
    public decimal Total { get; private set; }

    /// <summary>Descuento global sobre la cuenta, en porcentaje (0–100). Reduce base e IVA.</summary>
    public decimal DescuentoPorcentaje { get; private set; }

    /// <summary>Forma de cobro empleada; nulo mientras no se ha cobrado.</summary>
    public MetodoCobro? MetodoCobro { get; private set; }

    /// <summary>Factura (ticket) generada al cobrar; nulo mientras no se ha cobrado.</summary>
    public Guid? FacturaId { get; private set; }

    /// <summary>Número completo del ticket generado al cobrar (para mostrarlo sin cruzar módulos).</summary>
    public string? NumeroTicket { get; private set; }

    /// <summary>Líneas de la comanda (solo lectura; se manipulan con los métodos del agregado).</summary>
    public IReadOnlyList<LineaComanda> Lineas => _lineas.AsReadOnly();

    public static Comanda Abrir(Guid empresaId, Guid mesaId, string? notas, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var comanda = new Comanda(Guid.NewGuid(), empresaId, mesaId, Normalizar(notas), reloj.AhoraUtc);
        comanda.RegistrarEvento(new ComandaAbierta(comanda.Id, empresaId, mesaId, reloj.AhoraUtc));
        return comanda;
    }

    /// <summary>
    /// Añade una línea a la comanda a partir de un producto ya resuelto (nombre, precio e IVA
    /// «congelados» en el momento de pedirlo). Solo es posible mientras la comanda está abierta.
    /// </summary>
    public Resultado<LineaComanda> AgregarLinea(Guid productoId, string? descripcion, decimal cantidad, decimal precioUnitario, string codigoIva, decimal porcentajeIva, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.no_abierta", "Solo se pueden añadir líneas a una comanda abierta."));
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.linea_sin_descripcion", "Cada línea necesita una descripción."));
        }

        if (cantidad <= 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.cantidad_invalida", "La cantidad debe ser mayor que cero."));
        }

        if (precioUnitario < 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.precio_negativo", "El precio no puede ser negativo."));
        }

        // Si ya se pidió el mismo producto al mismo precio e IVA, se acumula en su línea (una comanda
        // muestra «Caña ×3», no tres líneas de «Caña»). Un precio distinto (tarifa cambiada) abre línea.
        var existente = _lineas.FirstOrDefault(
            l => l.ProductoId == productoId && l.PrecioUnitario == precioUnitario && l.CodigoIva == codigoIva);
        if (existente is not null)
        {
            existente.Incrementar(cantidad);
            Recalcular(reloj);
            return Resultado.Ok(existente);
        }

        var linea = new LineaComanda(EmpresaId, Id, productoId, descripcion!.Trim(), cantidad, precioUnitario, codigoIva, porcentajeIva);
        _lineas.Add(linea);
        Recalcular(reloj);
        return Resultado.Ok(linea);
    }

    /// <summary>Quita una línea de la comanda. Solo es posible mientras está abierta.</summary>
    public Resultado QuitarLinea(Guid lineaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se pueden quitar líneas de una comanda abierta."));
        }

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId);
        if (linea is null)
        {
            return Resultado.Fallo(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
        }

        if (linea.CantidadCobrada > 0)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.linea_cobrada", "No se puede quitar una línea que ya se ha cobrado en parte."));
        }

        _lineas.Remove(linea);
        Recalcular(reloj);
        return Resultado.Ok();
    }

    /// <summary>
    /// Fija la cantidad de una línea (ajuste directo con los botones +/− del TPV). Solo mientras la
    /// comanda está abierta. Para dejarla en cero, usa <see cref="QuitarLinea"/>.
    /// </summary>
    public Resultado<LineaComanda> FijarCantidadLinea(Guid lineaId, decimal cantidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.no_abierta", "Solo se pueden ajustar líneas de una comanda abierta."));
        }

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId);
        if (linea is null)
        {
            return Resultado.Fallo<LineaComanda>(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
        }

        if (cantidad <= 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.cantidad_invalida", "La cantidad debe ser mayor que cero; para eliminar la línea, quítala."));
        }

        if (cantidad < linea.CantidadCobrada)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.cantidad_menor_cobrada", "No se puede dejar la cantidad por debajo de lo ya cobrado en esta línea."));
        }

        linea.FijarCantidad(cantidad);
        Recalcular(reloj);
        return Resultado.Ok(linea);
    }

    /// <summary>
    /// Cambia el precio unitario de una línea (hacer precio a mano, o 0 para invitar). Solo mientras la
    /// comanda está abierta y la línea no se haya cobrado ya en parte. Recalcula base, IVA y total.
    /// </summary>
    public Resultado<LineaComanda> CambiarPrecioLinea(Guid lineaId, decimal precioUnitario, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.no_abierta", "Solo se puede cambiar el precio en una comanda abierta."));
        }

        var linea = _lineas.SingleOrDefault(l => l.Id == lineaId);
        if (linea is null)
        {
            return Resultado.Fallo<LineaComanda>(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
        }

        if (precioUnitario < 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Validacion("comanda.precio_negativo", "El precio no puede ser negativo."));
        }

        if (linea.CantidadCobrada > 0)
        {
            return Resultado.Fallo<LineaComanda>(Error.Conflicto("comanda.linea_cobrada", "No se puede cambiar el precio de una línea ya cobrada en parte."));
        }

        linea.FijarPrecio(precioUnitario);
        Recalcular(reloj);
        return Resultado.Ok(linea);
    }

    /// <summary>
    /// Aplica un descuento global a la cuenta, en porcentaje (0–100). Reduce base e IVA por igual y el
    /// ticket lo refleja como descuento por línea. Solo mientras la comanda está abierta.
    /// </summary>
    public Resultado AplicarDescuento(decimal porcentaje, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede aplicar un descuento a una comanda abierta."));
        }

        if (porcentaje is < 0m or > 100m)
        {
            return Resultado.Fallo(Error.Validacion("comanda.descuento_invalido", "El descuento debe estar entre 0 y 100%."));
        }

        DescuentoPorcentaje = porcentaje;
        Recalcular(reloj);
        return Resultado.Ok();
    }

    /// <summary>
    /// Envía a cocina/barra la parte **pendiente** de la comanda: por cada línea, la cantidad que aún no
    /// se había enviado (así, al pedir más de un producto ya enviado, solo va lo nuevo). Devuelve los
    /// artículos que se envían ahora (vacío si no hay nada nuevo). Solo mientras la comanda está abierta.
    /// </summary>
    public Resultado<IReadOnlyList<ArticuloCocina>> EnviarACocina()
    {
        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<IReadOnlyList<ArticuloCocina>>(Error.Conflicto("comanda.no_abierta", "Solo se puede enviar a cocina una comanda abierta."));
        }

        var articulos = new List<ArticuloCocina>();
        foreach (var linea in _lineas)
        {
            var nueva = linea.MarcarEnviadaCocina();
            if (nueva > 0)
            {
                articulos.Add(new ArticuloCocina(linea.Descripcion, nueva));
            }
        }

        return Resultado.Ok<IReadOnlyList<ArticuloCocina>>(articulos);
    }

    /// <summary>Actualiza las notas de la comanda mientras está abierta.</summary>
    public Resultado ActualizarNotas(string? notas)
    {
        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se pueden editar las notas de una comanda abierta."));
        }

        Notas = Normalizar(notas);
        return Resultado.Ok();
    }

    /// <summary>
    /// Marca la comanda como cobrada, congelando el ticket generado y la forma de cobro. La emisión
    /// del ticket la orquesta el caso de uso; aquí solo se asienta el resultado en el agregado.
    /// </summary>
    public Resultado MarcarCobrada(Guid facturaId, string numeroTicket, MetodoCobro metodo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede cobrar una comanda abierta."));
        }

        if (_lineas.Count == 0)
        {
            return Resultado.Fallo(Error.Validacion("comanda.sin_lineas", "No se puede cobrar una comanda vacía."));
        }

        Estado = EstadoComanda.Cobrada;
        FacturaId = facturaId;
        NumeroTicket = numeroTicket;
        MetodoCobro = metodo;
        CerradaEn = reloj.AhoraUtc;
        RegistrarEvento(new ComandaCobrada(Id, EmpresaId, MesaId, facturaId, Total, reloj.AhoraUtc));
        return Resultado.Ok();
    }

    /// <summary>¿Se ha cobrado ya alguna parte de la comanda (hay un reparto de cuenta en curso)?</summary>
    public bool TieneCobroParcial => _lineas.Any(l => l.CantidadCobrada > 0);

    /// <summary>Importe todavía pendiente de cobro (con IVA) sumando la parte no cobrada de cada línea.</summary>
    public decimal TotalPendienteCobro => Redondeo.Dos(_lineas.Sum(l => l.TotalPendiente));

    /// <summary>¿Está toda la comanda cobrada (ninguna línea con cantidad pendiente)?</summary>
    public bool EstaTotalmentePagada => _lineas.Count > 0 && _lineas.All(l => l.CantidadPendienteCobro == 0);

    /// <summary>
    /// Valida (sin mutar) que se pueden cobrar los artículos indicados y devuelve las líneas resueltas
    /// para emitir el ticket parcial. Cada ítem debe referirse a una línea existente y no superar su
    /// cantidad pendiente de cobro. Es el primer paso del reparto de cuenta por artículos.
    /// </summary>
    public Resultado<IReadOnlyList<LineaCobroTicket>> ValidarCobroParcial(IReadOnlyList<ItemCobroParcial> items)
    {
        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo<IReadOnlyList<LineaCobroTicket>>(Error.Conflicto("comanda.no_abierta", "Solo se puede cobrar una comanda abierta."));
        }

        if (items is null || items.Count == 0)
        {
            return Resultado.Fallo<IReadOnlyList<LineaCobroTicket>>(Error.Validacion("comanda.cobro_parcial_vacio", "Indica al menos un artículo que cobrar."));
        }

        var billing = new List<LineaCobroTicket>();
        foreach (var grupo in items.GroupBy(i => i.LineaId))
        {
            var linea = _lineas.SingleOrDefault(l => l.Id == grupo.Key);
            if (linea is null)
            {
                return Resultado.Fallo<IReadOnlyList<LineaCobroTicket>>(Error.NoEncontrado("comanda.linea_no_encontrada", "La línea no existe en la comanda."));
            }

            var cantidad = grupo.Sum(i => i.Cantidad);
            if (cantidad <= 0)
            {
                return Resultado.Fallo<IReadOnlyList<LineaCobroTicket>>(Error.Validacion("comanda.cantidad_invalida", "La cantidad a cobrar debe ser mayor que cero."));
            }

            if (cantidad > linea.CantidadPendienteCobro)
            {
                return Resultado.Fallo<IReadOnlyList<LineaCobroTicket>>(Error.Conflicto("comanda.cobro_excede_pendiente", $"«{linea.Descripcion}» no tiene tantas unidades pendientes de cobro."));
            }

            billing.Add(new LineaCobroTicket(linea.ProductoId, linea.Descripcion, cantidad, linea.PrecioUnitario, linea.CodigoIva, linea.PorcentajeIva));
        }

        return Resultado.Ok<IReadOnlyList<LineaCobroTicket>>(billing);
    }

    /// <summary>
    /// Aplica un cobro parcial ya facturado: descuenta las cantidades cobradas de cada línea y, si con
    /// esto queda toda la comanda pagada, la cierra (congela el último ticket y libera la mesa). Segundo
    /// paso del reparto de cuenta, tras haber emitido el ticket de los artículos validados.
    /// </summary>
    public Resultado AplicarCobroParcial(IReadOnlyList<ItemCobroParcial> items, Guid facturaId, string numeroTicket, MetodoCobro metodo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var validacion = ValidarCobroParcial(items);
        if (validacion.EsFallo)
        {
            return Resultado.Fallo(validacion.Error);
        }

        foreach (var grupo in items.GroupBy(i => i.LineaId))
        {
            var linea = _lineas.Single(l => l.Id == grupo.Key);
            linea.RegistrarCobrado(grupo.Sum(i => i.Cantidad));
        }

        if (EstaTotalmentePagada)
        {
            Estado = EstadoComanda.Cobrada;
            FacturaId = facturaId;
            NumeroTicket = numeroTicket;
            MetodoCobro = metodo;
            CerradaEn = reloj.AhoraUtc;
            RegistrarEvento(new ComandaCobrada(Id, EmpresaId, MesaId, facturaId, Total, reloj.AhoraUtc));
        }

        return Resultado.Ok();
    }

    /// <summary>
    /// Mueve la comanda a otra mesa (los clientes se cambian de sitio). Solo mientras está abierta; el
    /// caso de uso comprueba antes que la mesa destino exista, esté activa y libre.
    /// </summary>
    public Resultado CambiarMesa(Guid nuevaMesaId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede mover una comanda abierta."));
        }

        if (nuevaMesaId == MesaId)
        {
            return Resultado.Fallo(Error.Validacion("comanda.misma_mesa", "La comanda ya está en esa mesa."));
        }

        var anterior = MesaId;
        MesaId = nuevaMesaId;
        RegistrarEvento(new ComandaMovida(Id, EmpresaId, anterior, nuevaMesaId, reloj.AhoraUtc));
        return Resultado.Ok();
    }

    /// <summary>
    /// Absorbe las líneas de <paramref name="otra"/> comanda en esta (juntar dos cuentas en una). Ambas
    /// deben estar abiertas y ninguna puede tener un cobro parcial en curso. Las consumiciones repetidas
    /// se acumulan como al pedir. No cierra la otra comanda: eso lo hace <see cref="MarcarJuntada"/>.
    /// </summary>
    public Resultado AbsorberDe(Comanda otra, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(otra);
        ArgumentNullException.ThrowIfNull(reloj);

        if (otra.Id == Id)
        {
            return Resultado.Fallo(Error.Validacion("comanda.juntar_misma", "No se puede juntar una comanda consigo misma."));
        }

        if (Estado != EstadoComanda.Abierta || otra.Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se pueden juntar comandas abiertas."));
        }

        if (TieneCobroParcial || otra.TieneCobroParcial)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.juntar_con_cobro_parcial", "No se pueden juntar cuentas con un cobro parcial en curso."));
        }

        foreach (var linea in otra._lineas)
        {
            var r = AgregarLinea(linea.ProductoId, linea.Descripcion, linea.Cantidad, linea.PrecioUnitario, linea.CodigoIva, linea.PorcentajeIva, reloj);
            if (r.EsFallo)
            {
                return Resultado.Fallo(r.Error);
            }
        }

        return Resultado.Ok();
    }

    /// <summary>Cierra esta comanda porque se juntó en la comanda <paramref name="destinoId"/> (libera su mesa).</summary>
    public Resultado MarcarJuntada(Guid destinoId, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede juntar una comanda abierta."));
        }

        Estado = EstadoComanda.Juntada;
        CerradaEn = reloj.AhoraUtc;
        RegistrarEvento(new ComandaJuntada(Id, EmpresaId, MesaId, destinoId, reloj.AhoraUtc));
        return Resultado.Ok();
    }

    /// <summary>Anula la comanda sin cobrarla (libera la mesa). Solo es posible si está abierta.</summary>
    public Resultado Anular(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        if (Estado != EstadoComanda.Abierta)
        {
            return Resultado.Fallo(Error.Conflicto("comanda.no_abierta", "Solo se puede anular una comanda abierta."));
        }

        Estado = EstadoComanda.Anulada;
        CerradaEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    private void Recalcular(IReloj reloj)
    {
        _ = reloj;
        // El descuento se aplica por línea (igual que en el ticket): reduce la base y el IVA se calcula
        // sobre la base ya descontada, de modo que la comanda y la factura simplificada cuadran al céntimo.
        var factor = 1m - (DescuentoPorcentaje / 100m);
        BaseImponible = Redondeo.Dos(_lineas.Sum(l => Redondeo.Dos(l.Base * factor)));
        CuotaIva = Redondeo.Dos(_lineas.Sum(l => Redondeo.Dos(Redondeo.Dos(l.Base * factor) * l.PorcentajeIva / 100m)));
        Total = Redondeo.Dos(BaseImponible + CuotaIva);
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

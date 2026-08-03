using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Catalogo.Dominio;

/// <summary>Tipo de producto.</summary>
public enum TipoProducto
{
    /// <summary>Bien físico.</summary>
    Bien = 1,

    /// <summary>Servicio.</summary>
    Servicio = 2,
}

/// <summary>Se ha creado un producto.</summary>
public sealed record ProductoCreado(Guid ProductoId, Guid EmpresaId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Producto o servicio del catálogo de una empresa. Guarda su precio y el tipo de IVA por defecto,
/// que se prerrellenan al añadirlo a una factura.
/// </summary>
public sealed class Producto : RaizAgregadoEmpresa<Guid>
{
    public const int LongitudMaximaNombre = 200;

    private Producto(Guid id)
        : base(id, Guid.Empty)
    {
        Nombre = null!;
        CodigoIva = null!;
        Unidad = null!;
    }

    private Producto(Guid id, Guid empresaId, string? referencia, string nombre, TipoProducto tipo, decimal precio, decimal precioCompra, string codigoIva, string unidad, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        Referencia = referencia;
        Nombre = nombre;
        Tipo = tipo;
        PrecioUnitario = precio;
        PrecioCompra = precioCompra;
        CodigoIva = codigoIva;
        Unidad = unidad;
        Activo = true;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public string? Referencia { get; private set; }

    public string Nombre { get; private set; }

    public TipoProducto Tipo { get; private set; }

    /// <summary>Precio de venta unitario.</summary>
    public decimal PrecioUnitario { get; private set; }

    /// <summary>Precio de compra/coste unitario (para el cálculo de márgenes). 0 si no se conoce.</summary>
    public decimal PrecioCompra { get; private set; }

    /// <summary>Código del IVA por defecto (del catálogo <see cref="Impuesto"/>).</summary>
    public string CodigoIva { get; private set; }

    public string Unidad { get; private set; }

    public bool Activo { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    public static Resultado<Producto> Crear(
        Guid empresaId, string? referencia, string? nombre, TipoProducto tipo, decimal precioUnitario, decimal precioCompra, string? codigoIva, string? unidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, precioUnitario, precioCompra, ref codigoIva);
        if (error is not null)
        {
            return Resultado.Fallo<Producto>(error);
        }

        var producto = new Producto(
            Guid.NewGuid(), empresaId, Normalizar(referencia), nombre!.Trim(), tipo, precioUnitario, precioCompra, codigoIva!, NormalizarUnidad(unidad), reloj.AhoraUtc);
        producto.RegistrarEvento(new ProductoCreado(producto.Id, empresaId, reloj.AhoraUtc));
        return Resultado.Ok(producto);
    }

    public Resultado Actualizar(string? referencia, string? nombre, TipoProducto tipo, decimal precioUnitario, decimal precioCompra, string? codigoIva, string? unidad, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var error = Validar(nombre, precioUnitario, precioCompra, ref codigoIva);
        if (error is not null)
        {
            return Resultado.Fallo(error);
        }

        Referencia = Normalizar(referencia);
        Nombre = nombre!.Trim();
        Tipo = tipo;
        PrecioUnitario = precioUnitario;
        PrecioCompra = precioCompra;
        CodigoIva = codigoIva!;
        Unidad = NormalizarUnidad(unidad);
        ActualizadoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }

    public void Desactivar(IReloj reloj)
    {
        Activo = false;
        ActualizadoEn = reloj.AhoraUtc;
    }

    private static Error? Validar(string? nombre, decimal precio, decimal precioCompra, ref string? codigoIva)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Error.Validacion("producto.nombre_vacio", "El nombre del producto es obligatorio.");
        }

        if (nombre.Trim().Length > LongitudMaximaNombre)
        {
            return Error.Validacion("producto.nombre_largo", "El nombre del producto es demasiado largo.");
        }

        if (precio < 0)
        {
            return Error.Validacion("producto.precio_negativo", "El precio no puede ser negativo.");
        }

        if (precioCompra < 0)
        {
            return Error.Validacion("producto.precio_compra_negativo", "El precio de compra no puede ser negativo.");
        }

        var codigo = string.IsNullOrWhiteSpace(codigoIva) ? Impuesto.IvaGeneral.Codigo : codigoIva.Trim();
        var impuesto = Impuesto.PorCodigoImpuesto(codigo);
        if (impuesto.EsFallo)
        {
            return impuesto.Error;
        }

        codigoIva = impuesto.Valor.Codigo;
        return null;
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarUnidad(string? unidad) => string.IsNullOrWhiteSpace(unidad) ? "ud" : unidad.Trim();
}

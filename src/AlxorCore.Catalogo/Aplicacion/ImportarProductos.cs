using AlxorCore.Catalogo.Dominio;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Catalogo.Aplicacion;

/// <summary>Una fila de la importación de productos, con su número de línea en el CSV.</summary>
public sealed record FilaImportacionProducto(int Fila, DatosProducto Datos);

/// <summary>
/// Caso de uso: importar productos/artículos por lotes (desde CSV). Valida cada fila con las reglas
/// del dominio; en previsualización no persiste, y al confirmar da de alta las filas correctas en
/// una sola transacción. Las filas con error se devuelven con su número de línea y motivo.
/// </summary>
public sealed class ImportarProductos
{
    private readonly IRepositorioProductos _productos;
    private readonly IRepositorioHistoricoPrecios _historico;
    private readonly IUnidadDeTrabajoCatalogo _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ImportarProductos(IRepositorioProductos productos, IRepositorioHistoricoPrecios historico, IUnidadDeTrabajoCatalogo unidadDeTrabajo, IReloj reloj)
    {
        _productos = productos;
        _historico = historico;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<ResultadoImportacion> EjecutarAsync(
        Guid empresaId, IReadOnlyList<FilaImportacionProducto> filas, bool previsualizar, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filas);

        var errores = new List<ErrorFila>();
        var validos = new List<Producto>();

        foreach (var fila in filas)
        {
            var d = fila.Datos;
            var producto = Producto.Crear(empresaId, d.Referencia, d.Nombre, d.Tipo, d.PrecioUnitario, d.PrecioCompra, d.CodigoIva, d.Unidad, _reloj);
            if (producto.EsFallo)
            {
                errores.Add(new ErrorFila(fila.Fila, producto.Error.Mensaje));
            }
            else
            {
                validos.Add(producto.Valor);
            }
        }

        if (!previsualizar && validos.Count > 0)
        {
            foreach (var producto in validos)
            {
                _productos.Agregar(producto);
                _historico.Agregar(HistoricoPrecio.Registrar(empresaId, producto.Id, producto.PrecioUnitario, producto.PrecioCompra, _reloj.AhoraUtc));
            }

            await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        return new ResultadoImportacion(filas.Count, validos.Count, previsualizar ? 0 : validos.Count, previsualizar, errores);
    }
}

# Módulo Catálogo

Gestión de **productos y servicios** y catálogo de **impuestos (IVA)**.

## Impuestos

Los tipos de IVA españoles (21 %, 10 %, 4 %, 0 %) se modelan como **catálogo de código**
(`AlxorCore.Nucleo.Comun.Impuesto`), no como datos editables: son tipos nacionales y estables. Las
facturas guardarán una **copia del porcentaje** aplicado, de modo que un cambio futuro de tipos no
altere las facturas ya emitidas. `GET /impuestos` los expone.

## Productos

`Producto` { Referencia (opcional), Nombre, Tipo (Bien/Servicio), **PrecioUnitario** (venta),
**PrecioCompra** (coste, para el margen; 0 si no aplica), CodigoIva por defecto (validado contra el
catálogo), Unidad, **Categoria** (opcional, p. ej. «Cervezas», «Tapas»; agrupa los artículos en el TPV
de barra/salón), **ProveedorHabitualId** (proveedor habitual del artículo; referencia opcional a
Terceros), Activo }. Multiempresa (RLS por empresa).

Al añadir un producto a una factura se prerrellenan su precio de venta, su IVA y también su
**precio de compra**, que la factura **congela por línea** (`coste_unitario`) para que el margen del
informe de beneficio sea fiel aunque el coste cambie después.

## Histórico de precios

Cada alta de producto y cada **cambio de precio** (de venta o de compra) añade una fila a
`historico_precio` { ProductoId, PrecioVenta, PrecioCompra, RegistradoEn } (RLS por empresa,
solo-inserción). Permite ver la **evolución de precios** de un artículo en el tiempo.
`GET /productos/{id}/precios` la expone (más reciente primero).

## Stock (existencias)

Un producto puede llevar **control de stock** (`ControlarStock`). Los servicios normalmente no; una
tienda sí. Cuando está activo, el artículo tiene existencias (`Stock`) y cada variación queda
registrada en `movimiento_stock` { ProductoId, Tipo, Cantidad (con signo), StockResultante, Motivo,
CreadoEn } (RLS por empresa, histórico inmutable).

- **Movimientos manuales**: `Entrada` (compra/reposición), `Salida` (merma, rotura) y `Ajuste`
  (fija el stock al valor contado en un recuento).
- **Descuento automático por venta**: al emitir una **factura** o un **ticket**, Facturación llama al
  puerto `IStockVentas` (implementado por Catálogo), que registra un movimiento de tipo `Venta` por
  cada línea con producto que lleve control de stock. Es **mejor esfuerzo**: los servicios y los
  artículos sin control se ignoran, y la factura —verdad fiscal— ya está emitida.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/impuestos` | JWT | Tipos de IVA disponibles. |
| `GET` | `/productos` | JWT + empresa | Lista de productos activos. |
| `GET` | `/productos/{id}` | JWT + empresa | Obtiene un producto. |
| `GET` | `/productos/{id}/precios` | JWT + empresa | Histórico de precios del producto. |
| `POST` | `/productos` | permiso `producto.gestionar` | Crea un producto. **201** |
| `PUT` | `/productos/{id}` | permiso `producto.gestionar` | Actualiza un producto. |
| `GET` | `/productos/{id}/stock` | JWT + empresa | Histórico de movimientos de stock. |
| `POST` | `/productos/{id}/stock` | permiso `producto.gestionar` | Registra un movimiento de stock. |
| `GET` | `/carta/{empresaId}/datos` | **anónimo** | Carta pública del local: categorías, artículos y precios. |
| `GET` | `/carta/{empresaId}/qr.svg` | **anónimo** | Código QR (SVG) que enlaza a la carta pública. |

La importación CSV admite una columna opcional de **precio de compra** (`precio compra`, `coste`,
`compra`).

## Carta pública (menú con QR)

El bar puede publicar su **carta** para que el cliente la vea en el móvil escaneando un **QR**. La página
pública `carta.html?e={empresaId}` (sin cuenta) pinta los productos activos con precio, agrupados por
categoría, a partir de `GET /carta/{empresaId}/datos`. Es un endpoint **anónimo** que acota la lectura al
local indicado (fija el contexto de empresa, con lo que el filtro global y la RLS solo dejan ver ese
local) y **solo expone lo público**: nombre, categoría y precio —nunca coste, stock ni datos internos—.
El **QR** (`/carta/{empresaId}/qr.svg`, SVG con QRCoder) se genera apuntando a esa página. En la interfaz,
la sección **«Carta con QR»** muestra el enlace, el QR y un **cartel imprimible** para las mesas; la carta
se **actualiza sola** al cambiar los productos (no hay que reimprimir el QR).

## Persistencia

Esquema **`catalogo`**, tablas `producto`, `historico_precio` y `movimiento_stock` (RLS por empresa).
El repositorio ofrece escritura (`IRepositorioProductos`, `IRepositorioHistoricoPrecios`,
`IRepositorioMovimientosStock`) y consultas (`IConsultaProductos`, `IConsultaHistoricoPrecios`,
`IConsultaMovimientosStock`), que consumirán **Facturación** e **Informes**.

## Tests

- **Unitarios**: catálogo de IVA y validaciones de `Producto` (nombre, precio, precio de compra, IVA) y
  normalización/edición de la **categoría**.
- **Integración**: listar impuestos, CRUD de productos, **categoría** (alta y reasignación), histórico
  de precios (alta + cambios), aislamiento por empresa y **carta pública** (acceso anónimo con
  categorías y precios, QR en SVG y 404 si el local no existe).

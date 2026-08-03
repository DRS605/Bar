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
catálogo), Unidad, **ProveedorHabitualId** (proveedor habitual del artículo; referencia opcional a
Terceros), Activo }. Multiempresa (RLS por empresa).

Al añadir un producto a una factura se prerrellenan su precio de venta, su IVA y también su
**precio de compra**, que la factura **congela por línea** (`coste_unitario`) para que el margen del
informe de beneficio sea fiel aunque el coste cambie después.

## Histórico de precios

Cada alta de producto y cada **cambio de precio** (de venta o de compra) añade una fila a
`historico_precio` { ProductoId, PrecioVenta, PrecioCompra, RegistradoEn } (RLS por empresa,
solo-inserción). Permite ver la **evolución de precios** de un artículo en el tiempo.
`GET /productos/{id}/precios` la expone (más reciente primero).

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/impuestos` | JWT | Tipos de IVA disponibles. |
| `GET` | `/productos` | JWT + empresa | Lista de productos activos. |
| `GET` | `/productos/{id}` | JWT + empresa | Obtiene un producto. |
| `GET` | `/productos/{id}/precios` | JWT + empresa | Histórico de precios del producto. |
| `POST` | `/productos` | permiso `producto.gestionar` | Crea un producto. **201** |
| `PUT` | `/productos/{id}` | permiso `producto.gestionar` | Actualiza un producto. |

La importación CSV admite una columna opcional de **precio de compra** (`precio compra`, `coste`,
`compra`).

## Persistencia

Esquema **`catalogo`**, tablas `producto` e `historico_precio` (RLS por empresa). El repositorio
ofrece escritura (`IRepositorioProductos`, `IRepositorioHistoricoPrecios`) y consultas
(`IConsultaProductos`, `IConsultaHistoricoPrecios`), que consumirá **Facturación** e **Informes**.

## Tests

- **Unitarios**: catálogo de IVA y validaciones de `Producto` (nombre, precio, precio de compra, IVA).
- **Integración**: listar impuestos, CRUD de productos, histórico de precios (alta + cambios) y
  aislamiento por empresa.

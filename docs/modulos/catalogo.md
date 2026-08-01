# Módulo Catálogo

Gestión de **productos y servicios** y catálogo de **impuestos (IVA)**.

## Impuestos

Los tipos de IVA españoles (21 %, 10 %, 4 %, 0 %) se modelan como **catálogo de código**
(`AlxorCore.Nucleo.Comun.Impuesto`), no como datos editables: son tipos nacionales y estables. Las
facturas guardarán una **copia del porcentaje** aplicado, de modo que un cambio futuro de tipos no
altere las facturas ya emitidas. `GET /impuestos` los expone.

## Productos

`Producto` { Referencia (opcional), Nombre, Tipo (Bien/Servicio), PrecioUnitario, CodigoIva por
defecto (validado contra el catálogo), Unidad, Activo }. Multiempresa (RLS por empresa).

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/impuestos` | JWT | Tipos de IVA disponibles. |
| `GET` | `/productos` | JWT + empresa | Lista de productos activos. |
| `GET` | `/productos/{id}` | JWT + empresa | Obtiene un producto. |
| `POST` | `/productos` | permiso `producto.gestionar` | Crea un producto. **201** |
| `PUT` | `/productos/{id}` | permiso `producto.gestionar` | Actualiza un producto. |

## Persistencia

Esquema **`catalogo`**, tabla `producto` (RLS por empresa). El repositorio ofrece escritura
(`IRepositorioProductos`) y consultas (`IConsultaProductos`), que consumirá **Facturación**.

## Tests

- **Unitarios**: catálogo de IVA y validaciones de `Producto` (nombre, precio, IVA).
- **Integración**: listar impuestos, CRUD de productos y aislamiento por empresa.

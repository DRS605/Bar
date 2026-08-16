# Módulo Hostelería

TPV de **barra y salón** para bares y restaurantes: **mesas** y **comandas** (cuentas abiertas) que
al cobrarse se convierten en un **ticket** del módulo Facturación. Es el módulo que da nombre al
software de gestión de bar sobre ALXOR Core.

## Mesas

`Mesa` { Nombre («Mesa 1», «Barra», «Terraza 3»), Zona (opcional), Capacidad, **Forma**
(Cuadrada/Redonda/Rectangular), **PosX/PosY** (posición en el plano), Activa }. Multiempresa
(RLS por empresa). Es un elemento de configuración del salón: no guarda su ocupación, que se **deduce**
de si tiene una comanda abierta, para no duplicar estado entre agregados. Una mesa con una comanda
abierta no se puede retirar.

La **forma** y la **posición** permiten **dibujar el plano del local**: la barra es simplemente una
mesa de forma `Rectangular`, y la zona (Salón/Terraza/Barra) agrupa las mesas. Las coordenadas viven
en un lienzo abstracto acotado a `[0, Mesa.Lienzo]` y se recolocan con `PUT /mesas/{id}/posicion`.

## Comandas

`Comanda` { MesaId, Estado (Abierta/Cobrada/Anulada), AbiertaEn, CerradaEn, Notas, BaseImponible,
CuotaIva, Total, MetodoCobro, FacturaId, NumeroTicket } con sus **líneas** `LineaComanda`
{ ProductoId, Descripcion, Cantidad, PrecioUnitario, CodigoIva, PorcentajeIva, Base, CuotaIva }.
Multiempresa (RLS por empresa; la comanda es la raíz del agregado y sus líneas cuelgan de ella).

Ciclo de vida:

1. **Abrir** una comanda en una mesa **libre** (una mesa no puede tener dos comandas abiertas a la vez).
2. **Añadir / ajustar / quitar líneas** mientras está abierta. Cada línea se toma de un **producto del
   catálogo**; su precio y su IVA se **congelan** en ese momento, de modo que un cambio de tarifa
   posterior no altere una cuenta ya en marcha. Pedir **el mismo producto** (al mismo precio e IVA) se
   **acumula en su línea** (una comanda muestra «Caña ×3», no tres líneas); un precio distinto abre
   línea nueva. Se puede **fijar la cantidad** de una línea (botones +/− del TPV). Los totales se
   recalculan en cada cambio.
3. **Cobrar**: emite un **ticket** (factura simplificada, serie `T`) con las líneas congeladas y elige
   la forma de cobro (Efectivo/Tarjeta/Otro). La comanda queda **Cobrada** e inmutable y la mesa libre.
4. **Anular**: cierra una comanda abierta sin cobrarla (la mesa se libera). No se puede anular una ya
   cobrada.

## Cobro = ticket (integración con Facturación y Catálogo)

Al cobrar, el caso de uso reutiliza `EmitirTicket` de **Facturación**, que:

- asigna el **número correlativo** de la serie de tickets (`T{año}/NNNNNN`),
- deja el **registro VeriFactu** (encadenado de huellas), y
- descuenta **existencias** por cada línea con producto que lleve control de stock (puerto
  `IStockVentas` de **Catálogo**), como cualquier venta del TPV.

Así una consumición de barra acaba en la contabilidad exactamente igual que un ticket normal, sin
duplicar la lógica fiscal. La comanda solo guarda la referencia al ticket generado (`FacturaId`,
`NumeroTicket`) y la forma de cobro.

> Al tratarse de facturas simplificadas, una comanda que supere el tope legal del ticket
> (3.000 €, art. 4 RD 1619/2012) se rechaza al cobrar; en ese caso debe emitirse factura ordinaria.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/mesas` | JWT + empresa | Lista de mesas con su ocupación (comanda y total). |
| `POST` | `/mesas` | permiso `hosteleria.gestionar` | Crea una mesa. **201** |
| `PUT` | `/mesas/{id}` | permiso `hosteleria.gestionar` | Actualiza una mesa (incluida su forma). |
| `PUT` | `/mesas/{id}/posicion` | permiso `hosteleria.gestionar` | Recoloca una mesa en el plano. |
| `DELETE` | `/mesas/{id}` | permiso `hosteleria.gestionar` | Retira (desactiva) una mesa. **204** |
| `GET` | `/comandas` | JWT + empresa | Comandas abiertas de la empresa. |
| `GET` | `/comandas/{id}` | JWT + empresa | Comanda con sus líneas. |
| `POST` | `/comandas` | permiso `hosteleria.gestionar` | Abre una comanda en una mesa. **201** |
| `POST` | `/comandas/{id}/lineas` | permiso `hosteleria.gestionar` | Añade un producto (acumula si se repite). |
| `PUT` | `/comandas/{id}/lineas/{lineaId}` | permiso `hosteleria.gestionar` | Fija la cantidad de una línea (+/−). |
| `DELETE` | `/comandas/{id}/lineas/{lineaId}` | permiso `hosteleria.gestionar` | Quita una línea. |
| `POST` | `/comandas/{id}/cobrar` | permiso `hosteleria.gestionar` | Cobra emitiendo el ticket. |
| `POST` | `/comandas/{id}/anular` | permiso `hosteleria.gestionar` | Anula la comanda. **204** |

El permiso **`hosteleria.gestionar`** lo tienen los roles *Propietario* y *Usuario*.

## Persistencia

Esquema **`hosteleria`**, tablas `mesa`, `comanda` y `linea_comanda` (RLS por empresa). El repositorio
ofrece escritura (`IRepositorioMesas`, `IRepositorioComandas`) y consultas (`IConsultaMesas`,
`IConsultaComandas`); estas últimas resuelven la ocupación de cada mesa a partir de su comanda abierta.

## Interfaz web

Sección **«Barra / Salón»**: rejilla de mesas (libres/ocupadas con su total) y **TPV de mesa** rápido —
una **rejilla de productos** (un toque = pedir, con búsqueda/escáner y **filtros por categoría** cuando
los artículos la tienen —«Cervezas», «Tapas»…— más «Otros» para los que no) y la **comanda en vivo** con
selectores **+/−** por línea y total al instante. Los toques se reflejan de inmediato (optimista) y se
sincronizan en una cola (una operación a la vez, para no chocar); la respuesta del servidor manda. Desde
ahí se anula o se cobra eligiendo la forma de pago, con opción de **imprimir el ticket** en la impresora
térmica (ver módulo Documentos).

Sección **«Plano del local»**: lienzo donde se **dibujan y arrastran** las mesas (por forma y estado)
sobre las zonas (Salón, Terraza, Barra), se toca una mesa para abrir/ver su comanda y se **descarga el
dibujo** del plano en SVG para imprimirlo.

## Tests

- **Unitarios**: validaciones de `Mesa` (incluidas forma y posición/`Colocar` con acotado al lienzo) y
  ciclo de vida de `Comanda` (abrir, recalcular totales con IVA al añadir/quitar líneas, **acumular el
  mismo producto en una línea** y **abrir línea nueva a distinto precio**, no cobrar vacía, congelar el
  ticket al cobrar, no modificar tras cobrar, anular).
- **Integración**: flujo completo abrir → pedir → cobrar (genera ticket, libera la mesa y descuenta
  stock), crear barra con forma y recolocarla en el plano, una sola comanda por mesa, listado de
  abiertas, acumular el mismo producto en una línea, fijar la cantidad de una línea (y rechazar cero),
  quitar línea, no cobrar vacía, anular y exigencia de empresa activa.

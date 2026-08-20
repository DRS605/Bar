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
   línea nueva. Se puede **fijar la cantidad** de una línea (botones +/− del TPV) y **cambiar su precio
   a mano** («hacer precio», o 0 para **invitar**). Los totales se recalculan en cada cambio. No se puede
   cambiar el precio de una línea ya cobrada en parte.
3. **Cobrar**: emite un **ticket** (factura simplificada, serie `T`) con las líneas congeladas y elige
   la forma de cobro (Efectivo/Tarjeta/Otro). Admite un **descuento global** (%) que **recalcula base e
   IVA por línea** —de modo que el ticket cuadra— y queda asentado en la comanda. La comanda queda
   **Cobrada** e inmutable y la mesa libre.
4. **Repartir la cuenta por artículos** (cobro parcial): cobra **parte** de la comanda emitiendo un
   ticket solo por los artículos y cantidades indicados. Cada línea lleva la cuenta de lo ya cobrado
   (`CantidadCobrada`), así que la mesa **sigue abierta** con lo que falta hasta que el último pago la
   salda —momento en el que se cierra y libera igual que un cobro normal—. No se puede quitar ni bajar
   la cantidad de una línea por debajo de lo ya cobrado.
5. **Cuenta previa (pre-ticket)**: imprime lo consumido con sus importes y el total **sin cobrar ni
   cerrar la mesa** —lo que el cliente pide para revisar antes de pagar—. Va marcada como **documento
   sin valor fiscal** («No es una factura»); el ticket fiscal se emite al cobrar.
6. **Mover de mesa**: pasa una comanda abierta a otra mesa **libre y activa** (los clientes se cambian
   de sitio); la mesa de origen queda libre. No se puede mover a una mesa ocupada.
7. **Juntar mesas**: funde una comanda de origen en otra de destino —sus líneas pasan a la de destino
   (acumulando repetidos)— y cierra la de origen como **Juntada**, liberando su mesa. Ambas deben estar
   abiertas y sin cobros parciales en curso.
8. **Anular**: cierra una comanda abierta sin cobrarla (la mesa se libera). No se puede anular una ya
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

El **reparto por artículos** usa el mismo `EmitirTicket` en dos pasos por seguridad: primero
`ValidarCobroParcial` comprueba (sin mutar) que las cantidades caben en lo pendiente y resuelve las
líneas; luego, con el ticket ya emitido, `AplicarCobroParcial` descuenta esas cantidades y cierra la
comanda si no queda nada por cobrar. Cada ticket parcial se registra en Tesorería por su forma de pago,
de modo que **cada comensal aparece con su propio cobro** en el cierre de caja.

Además, al cobrar se **registra el cobro en Tesorería** (`RegistrarCobro`) con la **forma de pago**, de
modo que la venta de barra figura en el **cierre de caja del día** (`GET /informes/cierre-caja`) junto
al resto de cobros. Es *mejor esfuerzo*: la comanda ya queda cobrada en su propia transacción; si el
registro del cobro fallara, se avisa en el log sin deshacer el cobro. La sección **«Barra / Salón»**
incluye el botón **«🧾 Cierre de caja»**.

> Al tratarse de facturas simplificadas, una comanda que supere el tope legal del ticket
> (3.000 €, art. 4 RD 1619/2012) se rechaza al cobrar; en ese caso debe emitirse factura ordinaria.

## Comanda de cocina/barra

`POST /comandas/{id}/cocina` envía a cocina la parte **pendiente** de la comanda: por cada línea, la
cantidad que **aún no se había enviado** (así, al pedir más de un producto ya enviado, solo va lo
nuevo). Cada línea guarda su `CantidadEnviadaCocina`, de modo que el reenvío es incremental e
idempotente. `Comanda.EnviarACocina()` marca lo pendiente y devuelve los artículos nuevos.

El endpoint imprime la **comanda de cocina** (`GeneradorComandaCocinaEscPos`, en Documentos): mesa,
hora y artículos **en grande y sin precios** —distinta del ticket de cobro—, en la impresora
configurada (**mejor esfuerzo**: un fallo de impresión no interrumpe el pedido; sin impresora, solo se
marca lo enviado). En el editor de comanda, el botón **«🍳 Cocina»** lo dispara.

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
| `PUT` | `/comandas/{id}/lineas/{lineaId}/precio` | permiso `hosteleria.gestionar` | Cambia el precio de una línea (hacer precio o invitar con 0). |
| `DELETE` | `/comandas/{id}/lineas/{lineaId}` | permiso `hosteleria.gestionar` | Quita una línea. |
| `POST` | `/comandas/{id}/cocina` | permiso `hosteleria.gestionar` | Envía a cocina los artículos nuevos (marca e imprime). |
| `GET` | `/comandas/{id}/cuenta.escpos` | permiso `hosteleria.gestionar` | Descarga la cuenta previa (pre-ticket, sin valor fiscal) en ESC/POS. |
| `POST` | `/comandas/{id}/cuenta/imprimir` | permiso `hosteleria.gestionar` | Imprime la cuenta previa en la impresora térmica. |
| `POST` | `/comandas/{id}/mover` | permiso `hosteleria.gestionar` | Mueve la comanda a otra mesa libre. |
| `POST` | `/comandas/{id}/juntar` | permiso `hosteleria.gestionar` | Junta otra comanda en esta (funde las cuentas). |
| `POST` | `/comandas/{id}/cobrar` | permiso `hosteleria.gestionar` | Cobra emitiendo el ticket. |
| `POST` | `/comandas/{id}/cobrar-parcial` | permiso `hosteleria.gestionar` | Reparte la cuenta: cobra los artículos indicados con su ticket; cierra la mesa al saldar lo último. |
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
selectores **+/−** por línea, **precio editable** por línea (tocar el precio para «hacer precio» o
invitar) y total al instante. Los toques se reflejan de inmediato (optimista) y se
sincronizan en una cola (una operación a la vez, para no chocar); la respuesta del servidor manda. Desde
ahí se anula o se **cobra**: forma de pago con botones grandes, **teclado numérico** que calcula el
**cambio a devolver** en efectivo, **descuento** (%, con «Otro…» libre; recalcula base e IVA y **sí va
en el ticket**), **propina** (5 %, 10 % o redondear; se suma a lo que se cobra y al cambio, pero **no va
en el ticket**), **dividir a escote** con el **importe por comensal** a la vista, y **«Repartir»** la
cuenta **por artículos** —elegir lo que paga cada uno y emitir su ticket, dejando la mesa abierta hasta
el último pago—, con opción de **imprimir el ticket** en la impresora térmica (ver módulo Documentos).
El botón **«🧾 Cuenta»** imprime la **cuenta previa** (pre-ticket, sin valor fiscal) para que el cliente
la revise antes de pagar, sin cerrar la mesa. El menú **«Mesa ▾»** agrupa las operaciones de sala:
**mover** la comanda a otra mesa libre, **juntar** otra cuenta en esta y **anular**.

Sección **«Plano del local»**: lienzo donde se **dibujan y arrastran** las mesas (por forma y estado)
sobre las zonas (Salón, Terraza, Barra), se toca una mesa para abrir/ver su comanda y se **descarga el
dibujo** del plano en SVG para imprimirlo.

## Tests

- **Unitarios**: validaciones de `Mesa` (incluidas forma y posición/`Colocar` con acotado al lienzo) y
  ciclo de vida de `Comanda` (abrir, recalcular totales con IVA al añadir/quitar líneas, **acumular el
  mismo producto en una línea** y **abrir línea nueva a distinto precio**, **enviar a cocina solo lo
  nuevo** de forma incremental e idempotente, no cobrar vacía, congelar el ticket al cobrar, no
  modificar tras cobrar, anular; y el **reparto por artículos**: validar/resolver las líneas del ticket
  parcial, rechazar cobrar más de lo pendiente, no cerrar mientras quede pendiente, **cerrar al saldar
  lo último**, y no poder quitar ni bajar una línea por debajo de lo ya cobrado; **mover** de mesa y
  rechazo de la misma mesa; **juntar** acumulando líneas y cerrando la de origen, y rechazo si hay un
  cobro parcial en curso; **cambiar el precio** de una línea —incluido invitar con 0— y rechazo si ya
  está cobrada; y **aplicar descuento** recalculando base e IVA, con rechazo fuera de rango).
- **Integración**: flujo completo abrir → pedir → cobrar (genera ticket, libera la mesa y descuenta
  stock), crear barra con forma y recolocarla en el plano, una sola comanda por mesa, listado de
  abiertas, acumular el mismo producto en una línea, fijar la cantidad de una línea (y rechazar cero),
  quitar línea, **cambiar el precio de una línea** (recalcula el total), **cobro con descuento** (el
  ticket y la caja llevan el importe con descuento), **enviar a cocina los artículos nuevos sin
  repetirlos**, cobro que figura en el cierre
  de caja, **reparto por artículos** (un ticket por comensal, la mesa sigue abierta hasta el último pago
  y cada cobro figura en caja; y rechazo del cobro por encima de lo pendiente), **cuenta previa** en
  ESC/POS (se descarga sin emitir factura ni cerrar la mesa; aviso al imprimir sin impresora), **mover
  de mesa** (libera la de origen y ocupa la destino; rechazo si la destino está ocupada), **juntar
  mesas** (funde las cuentas acumulando repetidos y libera la mesa de origen), no cobrar vacía, anular y
  exigencia de empresa activa.

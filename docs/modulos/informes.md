# Módulo Informes

Lecturas agregadas para el **panel principal**, los **libros de IVA** y la **exportación para la
gestoría**. No tiene persistencia: compone las consultas de Facturación, Gastos y Tesorería.

## Dashboard

`GET /informes/dashboard` devuelve: facturado y gastado del **mes en curso**, número de facturas del
mes, y **pendiente de cobro** / **pendiente de pago** (total de documentos menos lo liquidado en
Tesorería).

## Libros de IVA

`GET /informes/libro-iva?tipo=Repercutido|Soportado&desde=&hasta=` devuelve los asientos del periodo
con sus totales:

- **Repercutido**: a partir de las facturas emitidas (fecha, número, cliente, NIF, base, cuota IVA).
- **Soportado**: a partir de los gastos (fecha, concepto, proveedor, base, cuota IVA).

> Simplificación del MVP: un asiento por documento con su base y cuota totales. El desglose por tipo
> de IVA dentro de un mismo documento es una mejora futura.

## Exportación para la gestoría

`GET /informes/libro-iva/csv?tipo=&desde=&hasta=` descarga el libro en **CSV** (separador `;`,
decimales con coma, formato español), listo para la gestoría. Requiere el permiso `datos.exportar`.

## Resúmenes fiscales trimestrales (303 y 130)

`GET /informes/resumen-trimestral?anio=&trimestre=1..4` calcula, a partir de las facturas emitidas y
los gastos, los dos modelos que un autónomo en estimación directa presenta cada trimestre. Es una
**ayuda informativa** para prepararlos con la gestoría, **no** un envío oficial a la AEAT.

- **Modelo 303 (IVA)** — por **trimestre**: `IVA repercutido` (cuota de las facturas del trimestre)
  menos `IVA soportado` (cuota de los gastos). El **resultado** positivo es *a ingresar*, negativo
  *a compensar/devolver*.
- **Modelo 130 (IRPF)** — **acumulado** desde el 1 de enero: sobre el rendimiento neto acumulado
  (`ingresos − gastos`, en base imponible) se aplica el **20 %**, del que se descuentan las
  **retenciones soportadas** (el IRPF que los clientes retuvieron en tus facturas) y los **pagos
  fraccionados de los trimestres anteriores**. Nunca resulta negativo (mínimo 0).

Solo cuentan las facturas en estado **Emitida**: se excluyen las **anuladas** y las ya
**rectificadas** (sustituidas por su rectificativa, que aporta los importes corregidos). Requiere el
permiso `informe.leer`.

## Declaraciones anuales (390 y 347)

`GET /informes/declaracion-anual?anio=` calcula, para el ejercicio indicado, los dos resúmenes
anuales. Como los trimestrales, es **ayuda informativa** para la gestoría, no un envío a la AEAT.

- **Modelo 390 (resumen anual de IVA)** — es la suma de los cuatro `303` del año: IVA repercutido
  (facturas emitidas del ejercicio) menos IVA soportado (gastos del ejercicio).
- **Modelo 347 (operaciones con terceros)** — relación de **clientes** y **proveedores** con los que
  el volumen de operaciones del año (**IVA incluido**) supera el umbral legal de **3.005,06 €**. Los
  clientes se agrupan por NIF (o por nombre si la factura no lo llevaba) sumando el total de sus
  facturas; los proveedores se agrupan por el maestro de proveedores (resolviendo nombre y NIF) o,
  en su defecto, por el texto libre del gasto. Requiere el permiso `informe.leer`.

## Beneficio (margen bruto y neto)

`GET /informes/beneficio?desde=&hasta=` calcula el beneficio del periodo a partir del **margen por
línea** de las facturas emitidas (venta − coste congelado) y de los gastos:

- **Margen bruto** = `Σ ingresos de venta − Σ coste (precio de compra)`.
- **Beneficio neto** = `margen bruto − gastos genéricos del periodo`.
- **Desglose por artículo/concepto**: unidades, ingresos, coste y margen, ordenado por margen.

El coste sale del **precio de compra congelado** en cada línea al emitir (Catálogo → Facturación),
de modo que el margen no cambia aunque después varíe el coste del producto. Requiere `informe.leer`.

A partir de ese desglose por artículo, la interfaz muestra un **ranking de artículos**: los **más
rentables** (mayor margen), el **rey de las ventas** (mayores ingresos) y **dónde se gana menos**
(menor margen), con el margen en % sobre ingresos. Es una vista derivada, sin endpoint propio.

## Cierre de caja (arqueo diario)

`GET /informes/cierre-caja?dia=` devuelve el **cierre de caja** de un día a partir de los movimientos
de Tesorería: **total cobrado** desglosado por **método de pago** (efectivo, tarjeta, Bizum…), total
pagado (salidas) y **neto**. Pensado para cuadrar la caja de una tienda al cerrar; accesible desde el
botón *Cierre de caja* del TPV.

## Ventas del bar (informe comercial)

`GET /informes/ventas?desde=&hasta=` devuelve el **informe comercial de ventas** del periodo, calculado
sobre las facturas emitidas (tickets incluidos): **número de tickets**, **venta total** y **ticket
medio**; el reparto de la venta **por día de la semana** (para ver los días fuertes, con la fecha de
emisión —sin ambigüedad de zona horaria—); y los artículos **más vendidos por unidades** (con su
importe y margen). Es distinto del *beneficio* (que ordena por margen): responde a «¿qué se vende, qué
días y cuánto deja cada ticket?». En la interfaz es el panel **«Ventas del bar»** con KPIs, un gráfico
de barras por día y el ranking por unidades.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/informes/dashboard` | permiso `informe.leer` | Panel principal. |
| `GET` | `/informes/libro-iva` | permiso `informe.leer` | Libro de IVA del periodo. |
| `GET` | `/informes/libro-iva/csv` | permiso `datos.exportar` | Exportación CSV. |
| `GET` | `/informes/resumen-trimestral` | permiso `informe.leer` | Resúmenes 303 (IVA) y 130 (IRPF) del trimestre. |
| `GET` | `/informes/declaracion-anual` | permiso `informe.leer` | Declaraciones anuales 390 (IVA) y 347 (terceros). |
| `GET` | `/informes/beneficio` | permiso `informe.leer` | Beneficio del periodo (margen bruto y neto). |
| `GET` | `/informes/cierre-caja?dia=` | permiso `informe.leer` | Cierre de caja de un día (cobrado por método, pagado, neto). |
| `GET` | `/informes/ventas` | permiso `informe.leer` | Informe comercial: tickets, ticket medio, ventas por día de la semana y más vendidos por unidades. |

## Tests

- **Unitarios**: exportador CSV (cabecera, asientos, totales, escapado); resúmenes fiscales (303
  repercutido − soportado por trimestre; 130 acumulado con el 20 %, retenciones, pagos anteriores y
  suelo en 0; exclusión de facturas anuladas/rectificadas; trimestre fuera de rango); **informe de
  ventas** (tickets/venta/ticket medio ignorando anuladas y fuera de rango, reparto L..D siempre
  completo, y top de productos ordenado por unidades).
- **Integración**: dashboard (facturado/gastado/pendientes y su actualización tras un cobro), libro
  de IVA repercutido, exportación CSV, resumen trimestral (303 y 130), beneficio y **informe de ventas**
  (tickets, ticket medio, más vendidos por unidades y reparto por día de la semana).

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

## Beneficio (margen bruto y neto)

`GET /informes/beneficio?desde=&hasta=` calcula el beneficio del periodo a partir del **margen por
línea** de las facturas emitidas (venta − coste congelado) y de los gastos:

- **Margen bruto** = `Σ ingresos de venta − Σ coste (precio de compra)`.
- **Beneficio neto** = `margen bruto − gastos genéricos del periodo`.
- **Desglose por artículo/concepto**: unidades, ingresos, coste y margen, ordenado por margen.

El coste sale del **precio de compra congelado** en cada línea al emitir (Catálogo → Facturación),
de modo que el margen no cambia aunque después varíe el coste del producto. Requiere `informe.leer`.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/informes/dashboard` | permiso `informe.leer` | Panel principal. |
| `GET` | `/informes/libro-iva` | permiso `informe.leer` | Libro de IVA del periodo. |
| `GET` | `/informes/libro-iva/csv` | permiso `datos.exportar` | Exportación CSV. |
| `GET` | `/informes/resumen-trimestral` | permiso `informe.leer` | Resúmenes 303 (IVA) y 130 (IRPF) del trimestre. |
| `GET` | `/informes/beneficio` | permiso `informe.leer` | Beneficio del periodo (margen bruto y neto). |

## Tests

- **Unitarios**: exportador CSV (cabecera, asientos, totales, escapado); resúmenes fiscales (303
  repercutido − soportado por trimestre; 130 acumulado con el 20 %, retenciones, pagos anteriores y
  suelo en 0; exclusión de facturas anuladas/rectificadas; trimestre fuera de rango).
- **Integración**: dashboard (facturado/gastado/pendientes y su actualización tras un cobro), libro
  de IVA repercutido, exportación CSV y resumen trimestral (303 y 130).

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

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/informes/dashboard` | permiso `informe.leer` | Panel principal. |
| `GET` | `/informes/libro-iva` | permiso `informe.leer` | Libro de IVA del periodo. |
| `GET` | `/informes/libro-iva/csv` | permiso `datos.exportar` | Exportación CSV. |

## Tests

- **Unitarios**: exportador CSV (cabecera, asientos, totales, escapado).
- **Integración**: dashboard (facturado/gastado/pendientes y su actualización tras un cobro), libro
  de IVA repercutido y exportación CSV.

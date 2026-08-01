# Módulo Tesorería

Registro de **cobros** (contra facturas) y **pagos** (contra gastos), totales o parciales, y cálculo
del **saldo** de cada documento.

## Reglas (invariantes)

- **P1 — Sin sobrepago**: la suma de movimientos de un documento no puede superar su total. El caso
  de uso lo comprueba antes de registrar (devuelve 409).
- **P2 — Estado derivado**: el estado (`Pendiente` / `Parcial` / `Liquidado`) se **calcula** a partir
  del total y de lo liquidado, nunca se fija a mano.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/cobros` | permiso `cobro.registrar` | Registra un cobro de una factura. |
| `POST` | `/pagos` | permiso `pago.registrar` | Registra un pago de un gasto. |
| `GET` | `/facturas/{id}/saldo` | permiso `factura.leer` | Total, liquidado, pendiente y estado. |
| `GET` | `/gastos/{id}/saldo` | permiso `gasto.leer` | Total, liquidado, pendiente y estado. |

## Composición

Tesorería consulta los totales de los documentos a **Facturación** (`IConsultaFacturas`) y **Gastos**
(`IConsultaGastos`); no duplica esa información. Guarda solo los movimientos.

## Persistencia

Esquema **`tesoreria`**, tabla `movimiento` (RLS por empresa). Un movimiento referencia el documento
por tipo (Factura/Gasto) e id.

## Tests

- **Unitarios**: validación de importe y derivación del estado de saldo.
- **Integración**: cobro parcial → total (Parcial → Liquidado), rechazo de sobrepago (409),
  consulta de saldo y pago de un gasto.

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
| `POST` | `/tesoreria/conciliacion` | permiso `cobro.registrar` | Lee un extracto Norma 43 y propone casaciones. |
| `POST` | `/tesoreria/remesa` | permiso `cobro.registrar` | Genera una remesa de adeudos SEPA (pain.008 / Norma 19). |

## Conciliación bancaria (Norma 43)

`POST /tesoreria/conciliacion` recibe el contenido de un extracto bancario en formato **Norma 43**
(Cuaderno 43 / AEB43, el estándar que exportan los bancos españoles) y lo interpreta con
`ParserNorma43`: registros de 80 caracteres, importes de 14 dígitos con 2 decimales implícitos y
fechas `AAMMDD`. Para cada apunte propone una **casación automática**:

- **Abono** (haber, importe positivo) → la **factura emitida** cuyo importe **pendiente** coincide.
- **Cargo** (debe, importe negativo) → el **gasto** cuyo importe pendiente coincide.

La casación es solo una sugerencia (por importe exacto, sin reutilizar un documento para dos
apuntes); el usuario la confirma registrando el cobro o el pago con los endpoints habituales
(`/cobros`, `/pagos`). No se persiste el extracto: es una ayuda de conciliación, no un libro contable.

## Remesas de adeudos SEPA (Norma 19)

`POST /tesoreria/remesa` recibe una lista de facturas y genera un fichero **pain.008.001.02** (el
adeudo directo SEPA, equivalente a la Norma 19) para cobrarlas por **domiciliación**. Requiere:

- En la **empresa** (Organización): IBAN de ingreso e **identificador de acreedor** SEPA
  (Ajustes → Datos de cobro).
- En cada **cliente** (Terceros): IBAN, **referencia de mandato** y fecha de firma del mandato.

`GenerarRemesaSepa` cobra el **importe pendiente** de cada factura (consulta los movimientos), **omite
informando** las que no se pueden domiciliar (cliente sin mandato o factura ya cobrada) y compone el
XML con `System.Xml.Linq` (una `PmtInf` con secuencia `OOFF`). Devuelve el fichero, el número de
adeudos, el total y la lista de omitidas; el usuario lo descarga y lo sube a su banco. No se registra
el cobro automáticamente (se hará al confirmarse el adeudo en el banco).

## Composición

Tesorería consulta los totales de los documentos a **Facturación** (`IConsultaFacturas`) y **Gastos**
(`IConsultaGastos`); no duplica esa información. Guarda solo los movimientos. Para las remesas SEPA
también lee **Terceros** (`IConsultaClientes`, para el IBAN y el mandato) y **Organización**
(`IConsultaEmpresas`, para los datos de cobro del acreedor).

## Persistencia

Esquema **`tesoreria`**, tabla `movimiento` (RLS por empresa). Un movimiento referencia el documento
por tipo (Factura/Gasto) e id.

## Tests

- **Unitarios**: validación de importe y derivación del estado de saldo.
- **Integración**: cobro parcial → total (Parcial → Liquidado), rechazo de sobrepago (409),
  consulta de saldo y pago de un gasto.

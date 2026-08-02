# Módulo Facturación

El corazón fiscal de ALXOR Core: **emisión de facturas**. Compone clientes (Terceros),
productos/impuestos (Catálogo) y la numeración correlativa (Organización).

## Flujo estrella

`POST /facturas` con el cliente y las líneas emite la factura en un paso: calcula IVA por línea y la
retención de IRPF, congela los datos del cliente y de las líneas, y asigna el número correlativo.

Cada línea puede indicar un `ProductoId` (toma nombre, precio e IVA por defecto del producto) o
escribirse a mano (`Descripcion`, `PrecioUnitario`, `CodigoIva`). El IRPF por defecto se toma del
cliente si no se indica.

## Cálculo de importes

- Por línea: `base = redondeo(cantidad × precio × (1 − descuento%))`, `cuota = redondeo(base × IVA%)`.
- Totales: `base_imponible = Σ bases`, `cuota_iva = Σ cuotas`,
  `retención = redondeo(base_imponible × IRPF%)`, `total = base_imponible + cuota_iva − retención`.
- Redondeo a 2 decimales, mitad hacia arriba (`AlxorCore.Nucleo.Comun.Redondeo`).

## Invariantes (probadas)

- **F1 — Numeración correlativa sin huecos**: `IServicioNumeracion` asigna el número con un
  `UPDATE … RETURNING` atómico; se asigna como último paso, tras validar todo.
- **F2 — Inmutabilidad**: una factura emitida no expone métodos de modificación ni borrado.
- **F3 — Cuadre de importes**: cálculo por línea con redondeo consistente.
- **F4 — Datos congelados**: la factura guarda copia del cliente (nombre, NIF, dirección) y de las
  líneas (descripción, precio, IVA).
- **F5 — Fechas coherentes**: `fecha_operación ≤ fecha_emisión`; el ejercicio se deriva de la emisión.

## Campos VeriFactu/SII reservados

`factura` incluye columnas `huella`, `huella_anterior`, `id_registro`, `tipo_operacion`,
`estado_envio_aeat` (nullable, sin lógica en el MVP): permitirán activar el registro de facturación
y el envío a la AEAT sin rehacer el núcleo.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/facturas` | permiso `factura.emitir` | Emite una factura. **201** |
| `GET` | `/facturas` | permiso `factura.leer` | Lista de facturas de la empresa. |
| `GET` | `/facturas/{id}` | permiso `factura.leer` | Factura con sus líneas. |

## Tickets (factura simplificada) — TPV

Un **ticket** es una **factura simplificada** (art. 4/7 RD 1619/2012): misma tabla `factura` con
`tipo_factura = Simplificada`, **sin retención de IRPF**, con el **destinatario opcional** (si no se
identifica se congela como *"Cliente de contado"* y `cliente_id` queda nulo) y con **tope de importe**
(`Factura.TicketImporteMaximo` = 3.000 €; por encima obliga a factura ordinaria). Usa su propia
**serie** (`T` por defecto) y, al ser una factura más, aparece en listados, cobros y libros de IVA.

El caso de uso `EmitirTicket` comparte con la emisión ordinaria la resolución de líneas y la
numeración correlativa. El TPV de la interfaz añade artículos por **código de barras** (cámara del
móvil vía `BarcodeDetector`, o lector USB / buscador) y cobra en un toque.

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/tickets` | permiso `factura.emitir` | Emite un ticket (factura simplificada). **201** |

## Facturación automática periódica

Una **factura recurrente** (`factura_recurrente`) es la plantilla de una suscripción/contrato:
cliente, líneas, **periodicidad** (semanal, mensual, trimestral, semestral o anual), fecha de la
próxima emisión, fecha de fin opcional e IRPF. No es una factura fiscal: cuando llega su fecha, el
sistema **emite automáticamente** una factura ordinaria real (con su número correlativo y todos los
invariantes F1–F5) reutilizando el caso de uso de emisión.

- **Proceso en segundo plano** (`ServicioFacturacionRecurrente`): recorre a diario **todas las
  empresas** con recurrencias vencidas; para cada una abre su ámbito con la empresa fijada
  (`IContextoEmpresaMutable`, aislamiento multiempresa) y emite. Tolerante a fallos: un error en una
  empresa no detiene al resto. Se configura en la sección `FacturacionRecurrente`
  (`Activo`, `RetardoInicial`, `Intervalo`).
- **Sin backfill sorpresa**: cada pasada emite **una sola** factura por recurrencia y avanza la
  próxima fecha a la siguiente ocurrencia posterior a hoy (no genera de golpe los periodos pasados).

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/facturas-recurrentes` | permiso `factura.leer` | Lista las recurrencias de la empresa. |
| `GET` | `/facturas-recurrentes/{id}` | permiso `factura.leer` | Recurrencia con su plantilla. |
| `POST` | `/facturas-recurrentes` | permiso `factura.emitir` | Crea una recurrencia. **201** |
| `PUT` | `/facturas-recurrentes/{id}` | permiso `factura.emitir` | Actualiza una recurrencia. |
| `POST` | `/facturas-recurrentes/{id}/estado` | permiso `factura.emitir` | Activa o pausa. |
| `POST` | `/facturas-recurrentes/procesar` | permiso `factura.emitir` | Emite ahora las vencidas. |

## Persistencia

- Esquema **`facturacion`**: `factura` y `linea_factura`; `factura_recurrente` y `linea_recurrente`
  para las suscripciones (todas con RLS por empresa).
- Índice único de número: `(empresa_id, prefijo, ejercicio, numero)`.
- Índice de barrido de vencidas: `(empresa_id, activa, proxima_emision)`.
- El repositorio ofrece escritura (`IRepositorioFacturas`) y consultas (`IConsultaFacturas`), que
  usarán Tesorería e Informes.

## Compromiso conocido (numeración)

El número se confirma en su propia transacción (la del servicio de numeración). Si la factura no se
guardara después, podría quedar un hueco. Se asigna como último paso para minimizar la ventana; la
numeración 100 % sin huecos ante fallos (misma transacción factura + serie) es una mejora futura.

## Tests

- **Unitarios**: cálculo de base/IVA/IRPF, redondeo, descuentos, varias líneas, y las
  validaciones (sin líneas, fechas, cantidad, IRPF), congelado de cliente. En recurrentes:
  validaciones, avance de la próxima fecha por periodicidad, autodesactivación al superar el fin y
  paso "vencida".
- **Integración**: emisión de extremo a extremo, numeración correlativa, IRPF del cliente,
  listar/obtener, cliente inexistente (404) y aislamiento por empresa. En recurrentes: crear/listar,
  `procesar` emite una factura y avanza la fecha, recurrencia pausada no emite, y aislamiento por
  empresa.

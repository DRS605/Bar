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

## Persistencia

- Esquema **`facturacion`**: `factura` y `linea_factura` (ambas con RLS por empresa).
- Índice único de número: `(empresa_id, prefijo, ejercicio, numero)`.
- El repositorio ofrece escritura (`IRepositorioFacturas`) y consultas (`IConsultaFacturas`), que
  usarán Tesorería e Informes.

## Compromiso conocido (numeración)

El número se confirma en su propia transacción (la del servicio de numeración). Si la factura no se
guardara después, podría quedar un hueco. Se asigna como último paso para minimizar la ventana; la
numeración 100 % sin huecos ante fallos (misma transacción factura + serie) es una mejora futura.

## Tests

- **Unitarios** (11): cálculo de base/IVA/IRPF, redondeo, descuentos, varias líneas, y las
  validaciones (sin líneas, fechas, cantidad, IRPF), congelado de cliente.
- **Integración**: emisión de extremo a extremo, numeración correlativa, IRPF del cliente,
  listar/obtener, cliente inexistente (404) y aislamiento por empresa.

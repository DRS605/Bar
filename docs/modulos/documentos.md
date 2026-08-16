# Módulo Documentos

Generación del **PDF** de facturas y **envío por correo**. No tiene persistencia propia: compone la
factura (Facturación) y los datos del emisor (Organización).

## PDF

`GET /facturas/{id}/pdf` devuelve el PDF de la factura (cabecera con la empresa emisora, datos del
cliente, líneas, base/IVA/IRPF y total). Se genera con **QuestPDF** (licencia Community). El importe
se formatea en español (coma decimal) sin depender de la cultura instalada.

## Correo

`POST /facturas/{id}/enviar` con `{ "email": "..." }` envía la factura con el PDF adjunto. El envío
va por **SMTP real** (`ServicioCorreoSmtp`) en cuanto se configura un servidor en la sección `Correo`
(compartida con el correo de cuenta de Identidad: `Host`, `Puerto`, `UsarStartTls`, `Usuario`, `Clave`,
`Remitente`, `RemitenteNombre`); si la sección está vacía, se usa el **stub** que registra en el log.
En ambos casos el puerto `IServicioCorreo` no cambia. El tipo de contenido del adjunto se deduce de su
extensión (`.pdf` → `application/pdf`, `.ics` → `text/calendar`).

## Ticket para impresora térmica (ESC/POS)

`GeneradorTicketEscPos` compone el ticket de una factura en **ESC/POS**, el lenguaje de las impresoras
térmicas de barra (papel de 80 mm ≈ 48 columnas): cabecera del local, líneas, base/IVA, **total**
destacado, pie y corte de papel. El texto usa la página de códigos **858** (Europa occidental, con «€»)
y así se lo indica a la impresora (`ESC t 19`).

- `GET /facturas/{id}/ticket.escpos` descarga los bytes ESC/POS (para enviarlos a mano a una impresora
  de red o por USB).
- `POST /facturas/{id}/imprimir` los envía a la **impresora configurada**. Con host en la sección
  `Impresora` (`Host`, `Puerto` 9100 por defecto, `TiempoEsperaMs`) se usa `ImpresoraTicketsRed` (socket
  TCP RAW/JetDirect); sin host, `ImpresoraTicketsNula` y el endpoint responde `impresora.no_configurada`
  (400) para que la operación siga siendo legible. El puerto `IImpresoraTickets` no cambia.

Desde el TPV de mesa, la casilla **«Imprimir ticket»** del cobro envía el ticket tras cobrar.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/facturas/{id}/pdf` | permiso `factura.leer` | Descarga el PDF. |
| `POST` | `/facturas/{id}/enviar` | permiso `factura.leer` | Envía la factura por correo. |
| `GET` | `/facturas/{id}/ticket.escpos` | permiso `factura.leer` | Descarga el ticket en ESC/POS. |
| `POST` | `/facturas/{id}/imprimir` | permiso `factura.leer` | Imprime el ticket en la impresora térmica. |

## Puertos

- `IGeneradorPdfFactura` → implementado con QuestPDF.
- `IServicioCorreo` → `ServicioCorreoSmtp` (SMTP real) si hay servidor configurado; si no, el stub.
- `IGeneradorTicketEscPos` → `GeneradorTicketEscPos` (ESC/POS, página de códigos 858).
- `IImpresoraTickets` → `ImpresoraTicketsRed` (TCP) si hay host configurado; si no, `ImpresoraTicketsNula`.

## Tests

- **Unitarios**: `ConstructorMensajeSmtp` (remitente/destinatario/asunto, cuerpo HTML, presencia y
  tipo de contenido del adjunto según su extensión, y el caso sin adjunto); `GeneradorTicketEscPos`
  (inicialización `ESC @`, selección de página de códigos, corte final, y presencia de local, número,
  líneas, «TOTAL» y «€»).
- **Integración**: descarga del PDF (200, `application/pdf`, cabecera `%PDF`), envío por correo (204),
  descarga del ticket ESC/POS (200, `application/octet-stream`, `ESC @`…corte) e impresión sin impresora
  configurada (400 `impresora.no_configurada`).

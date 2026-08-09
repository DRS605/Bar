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

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/facturas/{id}/pdf` | permiso `factura.leer` | Descarga el PDF. |
| `POST` | `/facturas/{id}/enviar` | permiso `factura.leer` | Envía la factura por correo. |

## Puertos

- `IGeneradorPdfFactura` → implementado con QuestPDF.
- `IServicioCorreo` → `ServicioCorreoSmtp` (SMTP real) si hay servidor configurado; si no, el stub.

## Tests

- **Unitarios**: `ConstructorMensajeSmtp` (remitente/destinatario/asunto, cuerpo HTML, presencia y
  tipo de contenido del adjunto según su extensión, y el caso sin adjunto).
- **Integración**: descarga del PDF (200, `application/pdf`, cabecera `%PDF`) y envío por correo (204).

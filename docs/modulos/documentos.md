# Módulo Documentos

Generación del **PDF** de facturas y **envío por correo**. No tiene persistencia propia: compone la
factura (Facturación) y los datos del emisor (Organización).

## PDF

`GET /facturas/{id}/pdf` devuelve el PDF de la factura (cabecera con la empresa emisora, datos del
cliente, líneas, base/IVA/IRPF y total). Se genera con **QuestPDF** (licencia Community). El importe
se formatea en español (coma decimal) sin depender de la cultura instalada.

## Correo

`POST /facturas/{id}/enviar` con `{ "email": "..." }` envía la factura con el PDF adjunto. En el MVP
el envío es un **stub** (registra en el log); el proveedor real (SMTP/servicio) se añadirá sin
cambiar el puerto `IServicioCorreo`.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/facturas/{id}/pdf` | permiso `factura.leer` | Descarga el PDF. |
| `POST` | `/facturas/{id}/enviar` | permiso `factura.leer` | Envía la factura por correo. |

## Puertos

- `IGeneradorPdfFactura` → implementado con QuestPDF.
- `IServicioCorreo` → stub que se sustituirá por el proveedor real.

## Tests

- **Integración**: descarga del PDF (200, `application/pdf`, cabecera `%PDF`) y envío por correo (204).

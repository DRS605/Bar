# Módulo Reservas

Agenda de **reservas** del local y su publicación como **calendario iCalendar** (para Google
Calendar, Apple Calendar y Outlook). Se apoya en Hostelería: al «sentar» una reserva puede abrir la
comanda de su mesa.

## Reserva

`Reserva` { NombreCliente, Telefono, Email, **FechaHora** (+ DuracionMinutos → fin), Comensales,
MesaId (opcional), Notas, Estado, ComandaId }. Multiempresa (RLS por empresa).

Ciclo de vida (máquina de estados):

- **Pendiente** → `Confirmar` → **Confirmada**
- Pendiente/Confirmada → `Sentar` → **Sentada** (si tiene mesa, abre su comanda y guarda `ComandaId`)
- Pendiente/Confirmada → `Cancelar` → **Cancelada**
- Pendiente/Confirmada → `MarcarNoShow` → **NoShow** (no se presentó)

Solo se puede **editar** una reserva mientras está Pendiente o Confirmada. Sentar reutiliza
`AbrirComanda` de Hostelería, de modo que la mesa queda ocupada y lista para pedir.

## Turnos y horarios

`Turno` { Nombre («Comida», «Cena»…), **Dias** (banderas de la semana), **HoraInicio/HoraFin**,
**AforoComensales** (0 = sin límite), Activo }. Multiempresa (RLS por empresa). Un turno define a la
vez el **horario** en que el local acepta reservas y, si se indica, el **aforo** de comensales.
`Turno.Aplica(fechaHora)` decide si cubre un momento (día + hora, admite cruzar la medianoche).

Al crear o editar una reserva se comprueba la disponibilidad (`DisponibilidadTurnos`, función pura):

- **Sin turnos definidos** → reserva **libre** a cualquier hora (comportamiento por defecto).
- **Con turnos** → la reserva debe caer en un turno abierto (si no, `reserva.fuera_de_horario`, 400) y
  no superar su aforo sumando el resto de reservas activas de ese turno y día (si lo supera,
  `reserva.aforo_completo`, 409). Las canceladas y no-shows no cuentan.

`GET /reservas/disponibilidad?dia=` devuelve, por turno, el aforo, lo **reservado** y lo **libre** de
esa fecha.

## Calendario iCalendar (conexión con Google y otras apps)

`GeneradorICal` produce documentos **iCalendar (RFC 5545)** —un `VEVENT` por reserva— que entienden
todos los calendarios. Hay dos formas de uso:

- **Descargar una reserva** (`GET /reservas/{id}/ical`): archivo `.ics` para importarla en el
  calendario del cliente o del local.
- **Suscribirse a la agenda** (`GET /agenda/{token}.ics`): un feed que se **actualiza solo** en
  Google/Apple/Outlook. La credencial es un **token secreto** por empresa (enlace de solo lectura):
  se obtiene con `GET /reservas/agenda` y se puede **regenerar** para invalidar el anterior.

El feed es **público por diseño** (los calendarios se suscriben sin iniciar sesión), por eso la tabla
`agenda_calendario` no lleva el filtro multiempresa: es el propio token el que resuelve la empresa, y
entonces se fija el contexto para leer sus reservas con el aislamiento habitual (RLS incluida).

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/reservas?desde=&hasta=` | JWT + empresa | Lista de reservas (opcionalmente por rango). |
| `GET` | `/reservas/{id}` | JWT + empresa | Obtiene una reserva. |
| `POST` | `/reservas` | permiso `reserva.gestionar` | Crea una reserva. **201** |
| `PUT` | `/reservas/{id}` | permiso `reserva.gestionar` | Edita una reserva pendiente/confirmada. |
| `POST` | `/reservas/{id}/confirmar` | permiso `reserva.gestionar` | Confirma. |
| `POST` | `/reservas/{id}/cancelar` | permiso `reserva.gestionar` | Cancela. |
| `POST` | `/reservas/{id}/no-show` | permiso `reserva.gestionar` | Marca no presentada. |
| `POST` | `/reservas/{id}/sentar` | permiso `reserva.gestionar` | Sienta (y abre comanda si hay mesa). |
| `GET` | `/reservas/{id}/ical` | JWT + empresa | Descarga la reserva en `.ics`. |
| `GET` | `/reservas/agenda` | permiso `reserva.gestionar` | Enlace suscribible de la agenda. |
| `POST` | `/reservas/agenda/regenerar` | permiso `reserva.gestionar` | Regenera el enlace (invalida el anterior). |
| `GET` | `/agenda/{token}.ics` | **anónimo** (token) | Feed iCalendar suscribible de la empresa. |
| `GET` | `/reservas/disponibilidad?dia=` | JWT + empresa | Aforo usado/libre por turno en una fecha. |
| `GET` | `/turnos` | JWT + empresa | Lista de turnos (horarios). |
| `POST` | `/turnos` | permiso `reserva.gestionar` | Crea un turno. **201** |
| `PUT` | `/turnos/{id}` | permiso `reserva.gestionar` | Actualiza un turno. |
| `DELETE` | `/turnos/{id}` | permiso `reserva.gestionar` | Retira (desactiva) un turno. **204** |

El permiso **`reserva.gestionar`** lo tienen los roles *Propietario* y *Usuario*.

## Persistencia

Esquema **`reservas`**: `reserva` y `turno` (RLS por empresa) y `agenda_calendario` (mapa
token→empresa, sin RLS para poder resolver el feed público). Repositorios de escritura
(`IRepositorioReservas`, `IRepositorioTurnos`, `IRepositorioAgenda`) y consulta (`IConsultaReservas`).

## Interfaz web

Sección **«Reservas»**: agenda con estados y acciones (confirmar, cancelar, no-show, **sentar**),
alta/edición con mesa y duración, descarga `.ics` por reserva y botón **«Suscribir calendario»** que
muestra el enlace para pegar en Google/Apple/Outlook. Desde ahí se abre **«Turnos y horarios»** para
crear/editar/retirar turnos (días, franja horaria y aforo).

## Tests

- **Unitarios**: máquina de estados de `Reserva`; `Turno.Aplica` (día/hora, cruce de medianoche) y la
  validación de `DisponibilidadTurnos` (sin turnos, fuera de horario, aforo, canceladas, edición); y
  formato del `GeneradorICal` (VCALENDAR/VEVENT, fechas UTC, escapado, estado CANCELLED).
- **Integración**: alta y listado, transiciones, sentar que abre comanda y ocupa la mesa, descarga
  `.ics`, feed suscribible **sin sesión** con token válido/inválido, regeneración del enlace, turnos
  que imponen horario y aforo (con disponibilidad) y su desactivación, y exigencia de empresa activa.

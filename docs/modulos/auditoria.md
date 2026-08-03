# Módulo Auditoría

Registro **inmutable** de la actividad: deja constancia de **quién** hizo **qué** y **cuándo**. Es la
base del cumplimiento antifraude y de la trazabilidad exigible a un software fiscal.

## Qué se registra

Cada petición que **modifica datos** (`POST`, `PUT`, `PATCH`, `DELETE`) de un usuario autenticado con
empresa activa deja una fila en `auditoria.registro_auditoria`:

- **Usuario** (id y nombre del token), **empresa** (RLS por empresa).
- **Acción** legible (p. ej. *«Alta en clientes»*, *«Modificación en productos»*).
- **Método** y **ruta** HTTP, **código de estado** (resultado) y **fecha/hora**.

Se captura en un **middleware** (`MiddlewareAuditoria`) que corre tras la autenticación; la escritura
es tolerante a fallos: si la auditoría fallara, **nunca** interrumpe la operación del usuario. Los
endpoints de autenticación (`/auth/*`) no se auditan.

> Alcance actual: auditoría a **nivel de operación** (quién, qué acción, cuándo, con qué resultado).
> El **detalle del cambio** (diff campo a campo) mediante eventos de dominio queda como mejora futura.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `GET` | `/auditoria?limite=` | permiso `informe.leer` | Actividad reciente de la empresa (máx. 500). |

La interfaz muestra la actividad reciente en **Ajustes → Actividad reciente**.

## Persistencia

Esquema **`auditoria`**, tabla `registro_auditoria` (append-only, RLS por empresa), con índice
`(empresa_id, ocurrido_en)`.

## Tests

- **Integración**: una operación de alta queda registrada con su acción/método/ruta; aislamiento de
  la auditoría por empresa.

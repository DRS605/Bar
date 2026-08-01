# Guía para agentes (CLAUDE.md)

ALXOR Core — ERP modular en **.NET 8 + PostgreSQL**. Lee primero
`docs/diseno-tecnico-funcional.md` (visión y MVP) y `README.md` (estructura).

## Reglas del proyecto

- **Un módulo a la vez, terminado por completo** antes del siguiente: dominio · API · persistencia ·
  tests unitarios · tests de integración · documentación. No empieces el módulo N+1 con el N a medias.
- Dominio en **español** (clases, tablas, endpoints). UI/PDF/correo en español.
- **Clean Architecture ligera**: el proyecto `AlxorCore.<Modulo>` (Dominio + Aplicación) no
  referencia frameworks; la infraestructura va en `AlxorCore.<Modulo>.Infraestructura`.
- Fallos esperados con `Resultado`/`Error` (no excepciones). Multiempresa: `empresa_id` obligatorio.
- Compilación estricta: `TreatWarningsAsErrors`. Ajustes de analizadores en `.editorconfig`
  (cada exclusión está justificada). No relajes reglas sin justificar.

## Compilar y testear

```bash
dotnet build
dotnet test        # requiere PostgreSQL en localhost:5432 (postgres/postgres)
```

Los tests de integración usan la base `alxor_test` (variable `ALXOR_TEST_CONEXION` para
sobrescribir). PostgreSQL local: `docker compose up -d` o un servidor propio.

## Migraciones EF Core

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> \
  --project src/AlxorCore.<Modulo>.Infraestructura \
  --startup-project src/AlxorCore.<Modulo>.Infraestructura \
  --output-dir Persistencia/Migraciones
```

## Entorno de ejecución remoto (nota)

Si no hay SDK de .NET instalado, este repo se ha construido ejecutando el SDK vía Docker
(`mcr.microsoft.com/dotnet/sdk:8.0`) con `--network host` (para el proxy de salida) y un PostgreSQL
instalado por `apt`. Docker Hub puede estar bloqueado por política; `mcr.microsoft.com` y NuGet sí
están permitidos.

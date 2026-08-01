# ALXOR Core

El **ERP más sencillo del mercado** para autónomos y pequeñas empresas españolas. El objetivo no
es tener más funcionalidades que SAP, Odoo o Ekon, sino que **cualquier persona pueda emitir una
factura en menos de cinco minutos sin leer un manual**.

> Documento de diseño técnico y funcional: [`docs/diseno-tecnico-funcional.md`](docs/diseno-tecnico-funcional.md)

## Principios

- Simplicidad visible, complejidad interna controlada. Nada de sobrearquitectura.
- **Monolito modular** con **Clean Architecture ligera** y **DDD práctico**.
- **API First** (OpenAPI/Swagger).
- **Multiempresa** desde el diseño (`empresa_id` obligatorio; preparado para Row-Level Security).
- Seguridad y permisos por diseño. Auditoría de operaciones críticas.
- Tests automáticos desde el primer día. **Un módulo se termina por completo antes de empezar el siguiente.**

## Pila tecnológica

| Área | Elección |
|---|---|
| Lenguaje / runtime | **.NET 8 LTS (C#)** |
| Base de datos | **PostgreSQL** |
| ORM | EF Core (Npgsql) |
| Autenticación | JWT |
| Tests | xUnit + FluentAssertions; integración contra PostgreSQL real |

Los nombres del dominio, las tablas y la API están en **español**.

## Estructura

```
src/
  AlxorCore.Nucleo                     # SharedKernel: Resultado, Error, EntidadBase, eventos, IContextoEmpresa, IReloj
  AlxorCore.Identidad                  # Módulo Identidad: Dominio + Aplicación (puro, sin frameworks)
  AlxorCore.Identidad.Infraestructura  # EF Core, hasher, JWT, repositorios, migraciones
  AlxorCore.Api                        # Host ASP.NET Core: endpoints REST, JWT, OpenAPI
tests/
  AlxorCore.Identidad.Tests            # Tests unitarios (dominio + aplicación)
  AlxorCore.IntegrationTests           # Tests de integración de extremo a extremo (API + PostgreSQL)
docs/
  diseno-tecnico-funcional.md          # Diseño del producto y del MVP
  modulos/identidad.md                 # Documentación del módulo Identidad
```

## Puesta en marcha (desarrollo)

Requisitos: **.NET 8 SDK** y **PostgreSQL** (local o vía Docker).

1. Levanta PostgreSQL (opción Docker):

   ```bash
   docker compose up -d
   ```

   O usa un PostgreSQL propio y ajusta la cadena `ConnectionStrings:AlxorCore` en
   `src/AlxorCore.Api/appsettings.json`.

2. Compila y ejecuta los tests:

   ```bash
   dotnet build
   dotnet test
   ```

   Los tests de integración usan por defecto la base `alxor_test` en `localhost:5432`
   (usuario/contraseña `postgres`). Se puede sobrescribir con la variable de entorno
   `ALXOR_TEST_CONEXION`.

3. Arranca la API:

   ```bash
   dotnet run --project src/AlxorCore.Api
   ```

   En entorno *Development* la API aplica las migraciones automáticamente y publica Swagger en
   `/swagger`. Prueba de vida: `GET /salud`.

### Migraciones de base de datos

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> \
  --project src/AlxorCore.Identidad.Infraestructura \
  --startup-project src/AlxorCore.Identidad.Infraestructura \
  --output-dir Persistencia/Migraciones
```

## Estado del proyecto

| Módulo | Estado |
|---|---|
| **Identidad** (registro, login, JWT, perfil, roles/permisos) | ✅ Terminado |
| **Organización** (empresas, membresías, series, multiempresa/RLS) | ✅ Terminado |
| **Terceros** (Clientes) | ✅ Terminado |
| **Catálogo** (Productos e Impuestos) | ✅ Terminado |
| Facturación (facturas emitidas) | ⏳ Siguiente |
| Gastos, Tesorería, Documentos, Informes | 🕓 Planificado |

El desarrollo avanza **módulo a módulo**: cada uno se entrega completo antes de empezar el siguiente.

# Bar Query

**Software de gestión para bares y restaurantes.** Toma de comandas en mesa con un **TPV rápido**,
reservas con recordatorio, control de compras/stock y **tickets** — con la facturación en regla
(**VeriFactu**). Sencillo de usar y de instalar. Construido sobre el núcleo **ALXOR Core**
(.NET 8 + PostgreSQL).

> Instálalo en tu ordenador: **[INSTALACION.md](INSTALACION.md)** · Diseño técnico:
> [`docs/diseno-tecnico-funcional.md`](docs/diseno-tecnico-funcional.md)

## Qué incluye

- **TPV de barra/salón** — mesas y comandas con **rejilla táctil por categorías** (un toque = pedir),
  cobro y **ticket** con impresión térmica **ESC/POS**.
- **Carta con QR** — menú público que el cliente ve en el móvil escaneando un **QR** (con cartel
  imprimible); se actualiza sola al cambiar los productos.
- **Reservas** — agenda con **recordatorio por correo**, **turnos y aforo**, y **calendario iCal**
  (se suscribe en Google/Apple/Outlook).
- **Compras y almacén** — artículos con categorías, control de **stock**, proveedores y gastos.
- **Facturación y Hacienda** — facturas y tickets con **VeriFactu** (huella + QR), libros de IVA e
  informes de negocio.
- **Multiempresa, permisos y auditoría** de serie; interfaz y correos en **español**.

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
  AlxorCore.Hosteleria(.Infraestructura)  # Mesas y comandas (TPV de barra/salón) → ticket
  AlxorCore.Reservas(.Infraestructura)    # Agenda, turnos/aforo y calendario iCal
  AlxorCore.Catalogo / Facturacion / …    # Núcleo ALXOR: artículos, IVA, facturas (VeriFactu), etc.
  AlxorCore.Documentos.Infraestructura    # PDF, correo SMTP y ticket ESC/POS (impresora térmica)
  AlxorCore.Api                        # Host ASP.NET Core: endpoints REST, JWT, OpenAPI + SPA (wwwroot)
tests/
  AlxorCore.<Modulo>.Tests             # Tests unitarios (dominio + aplicación) por módulo
  AlxorCore.IntegrationTests           # Tests de integración de extremo a extremo (API + PostgreSQL)
docs/
  diseno-tecnico-funcional.md          # Diseño del producto y del MVP
  modulos/hosteleria.md · reservas.md  # Documentación de los módulos de hostelería
```

## Interfaz web

Bar Query sirve su **interfaz web** (SPA) en la raíz (`/`), en el mismo origen que la API. Con
`docker compose up` la tienes en `http://localhost:8080`: acceso, panel con KPIs, **barra/salón**
(mesas y comandas), **reservas** con calendario iCal, artículos, compras/gastos, facturas e informes.
Diseño limpio, táctil y con pocos clics.

## Arranque rápido (Docker)

> ¿Quieres instalarlo en tu propio ordenador paso a paso? Sigue **[INSTALACION.md](INSTALACION.md)**
> (guía para localhost en Windows, macOS o Linux, con y sin Docker).

Con Docker basta un comando para levantar la API + PostgreSQL:

```bash
docker compose up --build
```

- API: `http://localhost:8080` · Swagger: `http://localhost:8080/swagger` · Salud: `/salud`
- En *Development* la API aplica las migraciones automáticamente.

> El archivo `docker-compose.override.yml` (incluido) publica la API en
> **`http://localhost:3400`** además del 8080; Docker Compose lo aplica solo.

### Datos de demostración

Con la API arrancada, rellena una empresa con clientes, artículos, facturas repartidas por el año,
cobros (alguno parcial), gastos y una factura recurrente, para ver el panel y los informes con
contenido desde el primer momento:

**Windows (PowerShell)** — no necesitas instalar nada más:

```powershell
.\scripts\datos-demo.ps1                             # contra http://localhost:3400
.\scripts\datos-demo.ps1 -BaseUrl http://localhost:8080
```

**macOS / Linux (Python)**:

```bash
python3 scripts/datos-demo.py                       # contra http://localhost:3400
python3 scripts/datos-demo.py http://localhost:8080 # otra URL base
```

Ambos usan solo lo que ya trae el sistema (Invoke-RestMethod en Windows; la biblioteca estándar de
Python en macOS/Linux). Crean la cuenta `demo@alxorcore.es` (contraseña `Demo1234!`) y **no vuelven a
sembrar** si la empresa ya tiene facturas. Pensados para bases de datos de desarrollo/demo, no para
producción.

**Demo de bar** — para ver Bar Query como un bar en marcha (carta por categorías, mesas por zonas,
turnos con aforo, reservas de hoy y una comanda abierta):

```bash
python3 scripts/datos-demo-bar.py                       # contra http://localhost:3400
python3 scripts/datos-demo-bar.py http://localhost:8080 # otra URL base
```

Crea la cuenta `bar@barquery.es` (contraseña `Demo1234!`) con el «Bar Sol de Levante» y es
**idempotente** (no re-siembra si el bar ya tiene mesas).

## Puesta en marcha (desarrollo con SDK)

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
| **Facturación** (facturas emitidas) | ✅ Terminado |
| **Gastos** | ✅ Terminado |
| **Tesorería** (cobros y pagos) | ✅ Terminado |
| **Hostelería** (mesas y comandas de bar/restaurante → ticket) | ✅ Terminado |
| **Reservas** (agenda, turnos/horarios con aforo, calendario iCal) | ✅ Terminado |
| **Documentos** (PDF y email) | ✅ Terminado |
| **Informes** (dashboard, libros de IVA, gestoría, beneficio) | ✅ Terminado |
| **Auditoría** (registro de quién hizo qué y cuándo) | ✅ Terminado |
| **Cuenta / RGPD** (exportación y borrado de datos, páginas legales) | ✅ Terminado |

**MVP completo**: los módulos están terminados (dominio · API · persistencia · tests · docs). El
desarrollo ha avanzado **módulo a módulo**, cada uno entregado por completo antes del siguiente.

## Flujo de extremo a extremo (API)

1. `POST /auth/registro` → `POST /auth/login` (JWT).
2. `POST /empresas` → `POST /empresas/{id}/seleccionar` (token con empresa activa, rol y permisos).
3. `POST /clientes`, `POST /productos`.
4. `POST /facturas` (numeración correlativa, IVA + IRPF) → `GET /facturas/{id}/pdf` →
   `POST /facturas/{id}/enviar`.
5. `POST /cobros` / `POST /gastos` + `POST /pagos`.
6. `GET /informes/dashboard`, `GET /informes/libro-iva`, `GET /informes/libro-iva/csv`.

Documentación por módulo en [`docs/modulos/`](docs/modulos/).

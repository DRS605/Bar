# Módulo Organización

Segundo módulo de ALXOR Core. Introduce el **tenant** (empresa), las **membresías** que ligan a los
usuarios con sus empresas y roles, y las **series de numeración**. Aquí vive la **infraestructura
multiempresa** que usarán todos los módulos posteriores.

## Responsabilidades

- Crear y consultar **empresas** (el usuario que la crea es su **Propietario**).
- **Seleccionar la empresa activa**: emite un nuevo JWT con el alcance de la empresa (empresa, rol y
  permisos), que el resto de módulos usan para autorizar y aislar datos.
- Gestionar **series de numeración** y ofrecer la **numeración correlativa** a otros módulos.

## Multiempresa (cómo funciona)

1. Tras `login`, el usuario tiene un token **sin empresa**. Con `GET /empresas` ve sus empresas y con
   `POST /empresas/{id}/seleccionar` obtiene un token **con empresa activa** (claims `empresa_id`,
   `rol` y varios `permiso`).
2. `ContextoEmpresaHttp` lee `empresa_id` del token en cada petición.
3. `DbContextEmpresaBase` (proyecto `AlxorCore.Persistencia`) aplica un **filtro global** por
   `empresa_id` a toda entidad `IEntidadEmpresa`: es imposible olvidar el filtrado.
4. `InterceptorEmpresa` fija `app.empresa_actual` en la conexión para que la **Row-Level Security**
   de PostgreSQL actúe como segunda barrera.

> `empresa` y `membresia` **no** se filtran por empresa (son las tablas que definen el tenant y su
> acceso). `serie_numeracion` sí es dato multiempresa, con RLS activada.

### Nota sobre RLS y el rol de base de datos

La RLS solo surte efecto si la aplicación se conecta con un rol **sin** superusuario ni BYPASSRLS.
En desarrollo/tests se usa `postgres` (superusuario), que la ignora; ahí el aislamiento lo garantiza
el filtro global de EF Core (probado en los tests de integración). En **producción** debe usarse un
rol de aplicación restringido para que la RLS sea efectiva.

## Numeración correlativa

`IServicioNumeracion.SiguienteAsync(empresa, tipo, ejercicio, prefijo?)` asigna el siguiente número
con un `UPDATE ... RETURNING` **atómico** (bloqueo de fila), evitando duplicados y carreras. Si no se
indica `prefijo` usa la serie por defecto (`FA`); en cualquier caso crea la serie de forma
**perezosa** si no existe, de modo que cada nuevo ejercicio (y cada serie: `FA`, `R`, `T`…) obtiene
su propio contador. Lo consume Facturación: al emitir una factura se puede elegir la **serie**
(`EmitirFacturaComando.Serie`), y cada serie numera de forma correlativa e independiente.

> Compromiso conocido: el número se confirma de inmediato. Si la creación del documento fallara
> después, podría quedar un hueco. Se asigna como último paso antes de guardar para minimizar la
> ventana; una numeración 100 % sin huecos ante fallos (misma transacción documento+serie) es una
> mejora futura documentada.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/empresas` | JWT | Crea empresa (el usuario pasa a Propietario). **201** |
| `GET` | `/empresas` | JWT | Lista las empresas del usuario, con su rol. |
| `POST` | `/empresas/{id}/seleccionar` | JWT | Devuelve un token con el alcance de la empresa. |
| `GET` | `/empresas/actual` | JWT + empresa | Empresa activa. |
| `GET` | `/series` | JWT + empresa | Series de la empresa activa. |
| `POST` | `/series` | permiso `empresa.ajustes` | Crea una serie. |

## Modelo y persistencia

- Esquema **`organizacion`**: `empresa`, `membresia`, `serie_numeracion`.
- `Nif` (value object) valida DNI, NIE y CIF con su dígito/letra de control.
- Índices únicos: `empresa.nif`, `(membresia.usuario_id, empresa_id)`,
  `(serie.empresa_id, tipo_documento, ejercicio, prefijo)`.
- Migración: `MigracionInicialOrganizacion` (incluye la activación de RLS sobre `serie_numeracion`).

## Autorización compartida

Los roles y permisos viven en el Núcleo (`AlxorCore.Nucleo.Autorizacion`) porque los comparten
Identidad (emisión del token), Organización (resolución de permisos al seleccionar empresa) y la API
(policies `RequierePermiso`).

## Tests

- **Unitarios**: `Nif` (DNI/NIE/CIF válidos e inválidos), `SerieNumeracion` (correlatividad y
  formato), `Empresa` y `Membresia`.
- **Integración**: crear → listar → seleccionar → consultar empresa; crear/listar series; y
  **aislamiento multiempresa** (un usuario no puede seleccionar ni ver la empresa de otro).

# Módulo Identidad

Primer módulo de ALXOR Core. Resuelve la **autenticación** (registro e inicio de sesión), la
identidad del **usuario** y el vocabulario de **roles y permisos** de la plataforma.

> El usuario es una identidad **global**: no pertenece a una empresa. La relación usuario ↔ empresa
> ↔ rol (la *membresía*) se construirá en el módulo **Organización**, donde existe la entidad
> `Empresa`. Por eso este módulo emite un token que identifica al usuario, todavía sin empresa activa.

## Responsabilidades

- Registrar usuarios (correo, nombre, contraseña).
- Iniciar sesión y emitir un **JWT**.
- Consultar el perfil del usuario autenticado.
- **Verificar el correo** con un token de un solo uso (enlace enviado al registrarse).
- **Recuperar contraseña**: solicitar un enlace y restablecerla con un token con caducidad.
- Definir el **catálogo de permisos** y los **roles** (Propietario, Usuario, Solo lectura).

## Tokens de cuenta (verificación y restablecimiento)

Al registrarse se emite un **token de verificación** (caduca en 48 h); recuperar contraseña emite un
**token de restablecimiento** (caduca en 1 h). De cada token solo se almacena su **hash** (SHA-256):
si se filtrase la base de datos, el token en claro no queda expuesto. El envío del enlace va por el
puerto de correo (un *stub* en el MVP; en desarrollo/pruebas el token se devuelve en la respuesta
para poder continuar sin correo real, nunca en producción).

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/auth/verificar-email` | Verifica el correo con `{ token }`. |
| `POST` | `/auth/recuperar` | Solicita el enlace de restablecimiento (respuesta uniforme, no revela si el correo existe). |
| `POST` | `/auth/restablecer` | Fija la nueva contraseña con `{ token, nuevaContrasena }`. |

## Estructura (Clean Architecture ligera)

```
AlxorCore.Identidad/                 # puro, sin frameworks
  Dominio/        Usuario, Email, HashContrasena, EstadoUsuario, Rol, Permisos, Eventos/
  Aplicacion/     CasosDeUso/ (RegistrarUsuario, IniciarSesion, ObtenerPerfil, VerificarEmail)
                  Puertos/    (IRepositorioUsuarios, IHasherContrasena, IProveedorTokens, IServicioVerificacionEmail)
                  Modelos/    (PerfilUsuario, ResultadoAutenticacion)
AlxorCore.Identidad.Infraestructura/ # adaptadores
  Persistencia/   IdentidadDbContext, Configuraciones/, RepositorioUsuarios, Migraciones/
  Seguridad/      HasherContrasenaIdentity (PBKDF2), ProveedorTokensJwt, OpcionesJwt, ConfiguracionJwt
  Correo/         ServicioVerificacionEmailStub
  Eventos/        PublicadorEventosRegistro
  RegistroServicios.AgregarModuloIdentidad(...)
```

El dominio y la aplicación no dependen de EF Core ni de ASP.NET: solo del `AlxorCore.Nucleo`.

## API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/auth/registro` | anónimo | Crea una cuenta. Devuelve **201**. |
| `POST` | `/auth/login` | anónimo | Devuelve `{ token, expiraEn, usuario }`. |
| `GET`  | `/auth/perfil` | JWT | Perfil del usuario autenticado. |
| `POST` | `/auth/verificar-email` | JWT | Marca el correo como verificado (**204**). |
| `GET`  | `/salud` | anónimo | Prueba de vida. |

Los errores se devuelven como `ProblemDetails` (RFC 7807) con un `codigo` estable, mapeados desde
el `Resultado` del dominio: validación → 400, no autenticado → 401, prohibido → 403, no encontrado
→ 404, conflicto → 409.

## Reglas e invariantes

- El **correo es único** en la plataforma (índice único) y se normaliza (minúsculas, sin espacios).
- La **contraseña** exige un mínimo de 8 caracteres y se guarda **cifrada** (PBKDF2, nunca en claro).
- El registro **no bloquea** por verificación de correo (fricción mínima): el usuario queda `Activo`
  con `EmailVerificado = false`.
- Login con credenciales incorrectas devuelve **siempre el mismo error genérico** (evita enumerar
  correos existentes). Una cuenta **suspendida** no puede iniciar sesión.
- Cada operación relevante emite **eventos de dominio** que se publican tras persistir (base de la
  futura auditoría).

## Roles y permisos

Los permisos son **códigos en código** (no datos editables), lo que mantiene la autorización simple
y versionada. Roles del MVP:

- **Propietario**: todos los permisos.
- **Usuario**: operativa diaria (facturas, gastos, cobros/pagos, clientes, productos, informes,
  exportar) sin gestión de usuarios ni ajustes sensibles.
- **Solo lectura**: `*.leer` + exportar (p. ej. la gestoría).

## Persistencia

- Esquema **`identidad`**, tabla **`usuario`**.
- `IdentidadDbContext` actúa como **Unidad de Trabajo**: al guardar, confirma y publica los eventos
  de dominio.
- Migración inicial: `MigracionInicialIdentidad`.

## Configuración

`appsettings.json`:

```json
{
  "ConnectionStrings": { "AlxorCore": "Host=localhost;Port=5432;Database=alxor;Username=postgres;Password=postgres" },
  "Jwt": { "Emisor": "alxor-core", "Audiencia": "alxor-core", "ClaveSecreta": "<mínimo 32 caracteres>", "MinutosExpiracion": 60 }
}
```

En producción, la `ClaveSecreta` y la cadena de conexión deben venir de variables de entorno o de un
gestor de secretos, **nunca** del repositorio.

## Tests

- **Unitarios** (`AlxorCore.Identidad.Tests`): `Email`, `Usuario` (estados e invariantes), `Rol`/
  permisos y los casos de uso con dobles en memoria.
- **Integración** (`AlxorCore.IntegrationTests`): flujo real registro → login → perfil, duplicados
  (409), credenciales incorrectas (401), acceso sin token (401) y verificación de correo, contra un
  **PostgreSQL real**.

## Decisiones pendientes que hereda el siguiente módulo

- La **membresía** usuario↔empresa↔rol y la **empresa activa** en el token se abordan en
  **Organización**.
- La infraestructura de **Row-Level Security** por `empresa_id` se introduce con la primera tabla
  multiempresa (Organización), no antes (no habría a qué aplicarla).
- El publicador de eventos y el servicio de correo son *stubs* que el módulo de **Auditoría** y el de
  **Documentos** sustituirán sin tocar los módulos que los emiten.

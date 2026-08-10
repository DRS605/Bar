# Instalar Comandia en tu ordenador (localhost)

Guía para levantar **Comandia** en tu propio PC (Windows, macOS o Linux). Hay dos caminos: con
**Docker** (lo más sencillo, un solo comando) o con el **SDK de .NET** si ya lo tienes instalado.

> Al arrancar en modo desarrollo, la aplicación **crea y actualiza la base de datos sola** (aplica las
> migraciones) y publica la documentación de la API en `/swagger`. No tienes que preparar nada a mano.

---

## Opción A · Con Docker (recomendada)

Levanta la aplicación **y** la base de datos con un único comando. No necesitas instalar .NET ni
PostgreSQL: solo Docker.

### 1. Instala Docker Desktop
- **Windows/macOS**: descarga *Docker Desktop* de <https://www.docker.com/products/docker-desktop/> e
  instálalo (en Windows, acepta el backend WSL 2 que te propone).
- **Linux**: instala `docker` y el complemento `docker compose` desde el gestor de paquetes.

### 2. Arranca Comandia
Abre una terminal en la carpeta del proyecto (donde está `docker-compose.yml`) y ejecuta:

```bash
docker compose up --build
```

La primera vez tarda unos minutos (descarga imágenes y compila). Cuando veas que la API está escuchando,
ábrela en el navegador:

**http://localhost:3400**  ·  API/Swagger en **http://localhost:3400/swagger**

### 3. Para y arranca cuando quieras
- Detener: `Ctrl+C` en la terminal, o `docker compose down`.
- Volver a arrancar: `docker compose up` (sin `--build` si no has cambiado código).
- **Tus datos se conservan** entre reinicios (viven en el volumen `alxor-postgres-data`).
- Para empezar de cero y borrar los datos: `docker compose down -v`.

---

## Opción B · Con el SDK de .NET y PostgreSQL

Úsala si prefieres ejecutar la aplicación directamente (por ejemplo, para desarrollar).

### 1. Requisitos
- **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
- **PostgreSQL 16** en `localhost:5432` con usuario `postgres` y contraseña `postgres`, y una base de
  datos llamada `alxor`. Si prefieres no instalar PostgreSQL, levanta solo ese contenedor:
  ```bash
  docker compose up -d postgres
  ```

### 2. Arranca la API
Desde la carpeta del proyecto:

```bash
# Development activa Swagger y aplica las migraciones automáticamente.
# Linux/macOS:
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/AlxorCore.Api
# Windows (PowerShell):
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project src/AlxorCore.Api
```

Abre la URL que indique la consola (normalmente **http://localhost:5000** o el puerto que muestre) y,
para la documentación, añade `/swagger`.

La cadena de conexión por defecto está en `src/AlxorCore.Api/appsettings.json`. Si tu PostgreSQL usa
otros datos, cámbiala ahí o pásala por variable de entorno `ConnectionStrings__AlxorCore`.

---

## Primeros pasos dentro de la aplicación

1. En la pantalla de acceso, pulsa **«¿Primera vez? Crea tu cuenta →»** e introduce nombre, correo y
   contraseña. Entrarás automáticamente.
2. Te pedirá **crear tu local** (razón social y NIF). Ese es tu negocio dentro de Comandia.
3. Ya puedes usar todo: **mesas y comandas**, **reservas y turnos**, **compras/gastos**, **facturación**
   e **informes**.

> Puedes crear **varios locales** con la misma cuenta y cambiar entre ellos desde el menú superior.

---

## Correo de verdad (opcional)

Recién instalado, los correos (confirmación y recordatorio de reservas, envío de facturas) se
**registran en el log** pero no salen. Para enviarlos de verdad, configura un servidor SMTP en la
sección `Correo`. Con Docker, añade estas variables al servicio `api` de `docker-compose.override.yml`:

```yaml
    environment:
      Correo__Host: "smtp.tu-proveedor.com"
      Correo__Puerto: "587"
      Correo__UsarStartTls: "true"
      Correo__Usuario: "tu-usuario"
      Correo__Clave: "tu-contraseña"
      Correo__Remitente: "no-responder@tudominio.com"
      Correo__RemitenteNombre: "Comandia"
```

Sin SDK, con `dotnet run`, edita esos mismos valores en `appsettings.json`. En cuanto `Host` tenga
valor, Comandia envía por SMTP real sin ningún otro cambio. Sirven proveedores como Brevo, Amazon SES,
Mailgun o el correo de tu propio dominio.

---

## Problemas frecuentes

- **El puerto 3400 está ocupado**: cambia el mapeo en `docker-compose.override.yml`
  (`"3401:8080"`, por ejemplo) y abre esa URL.
- **`docker compose` no existe**: tienes una versión antigua; usa `docker-compose` (con guion) o
  actualiza Docker.
- **No conecta con la base de datos** (Opción B): comprueba que PostgreSQL está arrancado y que los
  datos de `appsettings.json` coinciden (host, puerto, usuario, contraseña, base `alxor`).

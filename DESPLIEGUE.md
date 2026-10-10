# Poner Bar Query online (producción)

Guía corta para tener Bar Query en una **dirección pública con HTTPS**, de modo que tú y los
clientes podáis usarlo desde el móvil. Pensado para un servidor barato (VPS).

> El HTTPS lo gestiona **Caddy** de forma automática (certificado gratis de Let's Encrypt). Solo
> necesitas un **servidor** y un **dominio**.

## 1. Lo que necesitas
- Un **VPS** con Ubuntu/Debian (vale uno de ~4–6 €/mes: Hetzner, DigitalOcean, Contabo…). Con 2 GB
  de RAM sobra para empezar.
- Un **dominio** o subdominio (p. ej. `carta.tubar.com`).

## 2. Apunta el dominio al servidor
En tu proveedor de dominios, crea un registro **A** con el nombre que quieras
(`carta`) apuntando a la **IP pública** del servidor. Espera unos minutos a que propague.

## 3. Instala Docker en el servidor
```bash
curl -fsSL https://get.docker.com | sh
```

## 4. Descarga el proyecto y configura
```bash
git clone https://github.com/DRS605/bar.git
cd bar
cp .env.ejemplo .env
nano .env         # pon tu DOMINIO, una contraseña de base de datos y una clave JWT
```
Para generar una clave JWT aleatoria: `openssl rand -base64 48`.

## 5. Arranca
```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```
La primera vez tarda un poco (compila la app y saca el certificado HTTPS). Cuando termine,
abre **`https://TU-DOMINIO`** en el navegador.

Comprobar que está vivo: `https://TU-DOMINIO/salud` debe responder `{"estado":"ok"}`.

## 6. Primer uso
1. Entra en `https://TU-DOMINIO`, **crea tu cuenta** y tu **empresa** (el bar).
2. Da de alta **productos** (con su precio PVP, IVA incluido), **mesas** y las **zonas**.
3. En **«Carta con QR»** elige el **tema**, sube **fotos** y marca **alérgenos / recomendado /
   picante**; traduce la carta si quieres (inglés/francés).
4. En **Barra/Salón**, botón **«📱 QR»** de cada mesa → **imprime el QR** y pégalo en la mesa.
   Los clientes escanean, ven la carta en su idioma y **piden desde el móvil**; el pedido te llega
   a Barra/Salón para **confirmarlo**.

## 7. Copias de seguridad (recomendado)
Copia diaria de la base de datos (añádelo al `crontab -e`):
```bash
0 4 * * * docker exec $(docker ps -qf name=postgres) pg_dump -U postgres alxor | gzip > ~/bar-backup-$(date +\%F).sql.gz
```

## 8. Actualizar a una versión nueva
```bash
cd bar && git pull
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```
Las migraciones de base de datos se aplican solas al arrancar (`AplicarMigraciones=true`).

## Notas
- Solo **Caddy** (puertos 80/443) está expuesto; la API y PostgreSQL quedan en la red interna.
- La API respeta el dominio/HTTPS del proxy, así que los **QR y enlaces** de la carta salen
  correctos (`https://TU-DOMINIO/carta.html?...`).
- Multiempresa: cada bar es una **empresa** aislada; un mismo servidor puede dar servicio a varios.

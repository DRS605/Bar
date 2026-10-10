# Cómo montar y operar Bar Query (nube vs on-premise)

Guía de operación para vender y dar servicio. **Dos planes por local**, sin permanencia, sin coste
por terminal, alta gratis. Hardware para el bar: **0 €** (usan sus móviles/tablets).

## Planes

| | **Essential — 12,95 €/mes** | **Pro — 29,95 €/mes** |
|---|---|---|
| Catálogo, mesas y comandas | ✅ | ✅ |
| **Formatos (media/ración) y extras** con suplemento | ✅ | ✅ |
| **Promociones y happy hour** (por producto/categoría y franja horaria) | ✅ | ✅ |
| Cobro con ticket (IVA incluido), **propinas y pago mixto** | ✅ | ✅ |
| **Cierre de caja con arqueo** (fondo, movimientos) y **ventas por camarero** | ✅ | ✅ |
| Informes y libros de IVA | ✅ | ✅ |
| Comandas a cocina y **pantalla de cocina (KDS) por zonas** | — | ✅ |
| **Carta QR con autopedido** (multiidioma, fotos, alérgenos) | — | ✅ |
| **Menú del día** online y **pedirlo desde el QR** | — | ✅ |
| Pedidos del cliente desde su móvil | — | ✅ |
| Reservas (aforo, recordatorios, calendario) y **reserva online** | — | ✅ |

- **Essential** es el gancho de entrada: «convierte tu móvil en caja registradora», pero ya con
  formatos/extras, promociones, propinas, pago mixto y un cierre de caja de verdad (arqueo y
  ventas por camarero).
- **Pro** añade la capa digital de cara al cliente (carta QR con autopedido estilo Qamarero, menú del
  día y reserva online), las comandas a cocina y la **pantalla de cocina (KDS)**.
- Instalable como app (PWA) y con **modo offline básico**: abre sin conexión; la caja necesita
  internet para cobrar y sincronizar.
- El plan se ve y se cambia en **Ajustes → Tu plan** dentro de la app. Técnicamente es el campo
  `plan` de la suscripción del local (por defecto **Pro**, para no capar a los locales ya existentes).
- El cobro de la cuota se factura por fuera (Stripe/recibo/transferencia); el plan de la app controla
  qué funciones ve el bar, no el cobro.

> El resto de la guía usa 29,95 € (Pro) como referencia; para Essential, sustituye por 12,95 €.

## Resumen de la decisión
- **Por defecto: NUBE (SaaS multiempresa).** Un servidor sirve a muchos bares. Es lo más barato de
  operar a 29,95 €, lo más rápido de dar de alta y lo natural para el autopedido por QR (necesita
  una dirección pública de todos modos).
- **On-premise:** solo para el bar que lo pida (mala conexión o no quiere cuota). Se cobra aparte
  (licencia/alta + aparato), no entra en los 29,95 €/mes.

## Opción A — Nube (recomendada)
**Arquitectura:** 1 VPS con la pila de `docker-compose.prod.yml` (PostgreSQL + API + Caddy con HTTPS
automático). Cada bar es una **empresa** aislada dentro de la misma instalación (aislamiento por
`empresa_id` + Row-Level Security). Un VPS pequeño (2 GB RAM, ~5–15 €/mes) aguanta **decenas de
bares**.

**Montaje (una vez):**
1. Un VPS (Hetzner/DigitalOcean/Contabo) + un dominio (p. ej. `app.barquery.es`).
2. Apunta el dominio a la IP, instala Docker y despliega (ver `DESPLIEGUE.md`).
3. Listo: `https://app.barquery.es`.

**Alta de cada bar (minutos):**
1. El bar entra en `https://app.barquery.es`, **crea su cuenta** y su **empresa** (su bar).
2. Da de alta productos, mesas y zonas; configura la carta (tema, fotos, alérgenos, idiomas).
3. Imprime los **QR por mesa** y empieza. El camarero abre la web en su móvil, **«añadir a inicio»**
   (app instalable) y entra una vez: la sesión queda recordada.

**Economía (ejemplo):** 20 bares × 29,95 € = **599 €/mes**; infra ~10 €/mes. El coste real es tu
**soporte y puesta en marcha**, no el servidor.

**Cobro:** 29,95 €/mes se factura por fuera (Stripe/recibo/transferencia). No hace falta integrar
pasarela en la app para empezar.

**Copias y actualizaciones:** centralizadas (las haces tú). Backup diario de PostgreSQL y
`git pull` + `up -d --build` para actualizar (ver `DESPLIEGUE.md`).

## Opción B — On-premise (caso concreto)
Un mini-PC (Intel NUC / Beelink, ~150–250 €) en el bar con el **mismo** Docker Compose. Tiene sentido
si el bar tiene **mala conexión** o no quiere cuota.

**Inconvenientes a tener claros:**
- El **QR del cliente** necesita ser accesible desde fuera → hay que montar un **túnel / DNS
  dinámico** (Cloudflare Tunnel, Tailscale Funnel…). Más complejidad.
- **Un aparato por bar** = más soporte, copias y acceso remoto uno a uno.
- Encaja como **pago único (licencia) + mantenimiento anual**, no como cuota de 29,95 €.

**Montaje:** igual que en nube pero en el mini-PC del bar; el dominio se sustituye por el túnel.

## «Que no se pare la caja»
En nube, si el bar se queda sin internet, la caja depende de la conexión. Mitigación práctica hoy:
**4G de respaldo** en el router o compartido desde un móvil (barato y suficiente para un bar). Un
**modo offline** real (la PWA encolando pedidos y sincronizando al volver la conexión) es un
desarrollo futuro, cuando varios clientes lo pidan.

## Qué necesita el bar (para el argumentario de venta)
- **Nada de hardware nuevo:** móvil/tablet que ya tienen. (Opcional: tablet Android de ~60–100 €.)
- **Nada que instalar:** es web; se «añade a la pantalla de inicio» como una app.
- **Alta gratis**, **sin permanencia**, **sin cobro por terminal**. Dos planes: **Essential** (caja,
  tickets, formatos/extras, promociones, propinas y arqueo) por **12,95 €/mes** y **Pro** (añade carta
  QR con autopedido, menú del día, reserva online, comandas a cocina y pantalla de cocina) por
  **29,95 €/mes**.
- **Pantalla de cocina (KDS):** una tablet en la cocina (`…/cocina.html`) ve los platos por zonas y los
  marca como servidos; no hace falta aparato nuevo.

// Service worker de Bar Query: hace la app instalable y da un respaldo offline del «shell»
// (abrir la app, la carta y la pantalla de cocina sin conexión). No cachea datos de la API:
// las lecturas y escrituras van siempre a la red, así la información nunca queda obsoleta ni
// se descuadra la caja. El modo offline completo (encolar comandas y cobros) es un desarrollo aparte.
const CACHE = "bq-shell-v2";
const SHELL = ["/", "/index.html", "/carta.html", "/cocina.html", "/manifest.json", "/icono-192.png", "/icono-512.png"];

self.addEventListener("install", (e) => {
  e.waitUntil(
    caches.open(CACHE)
      .then((c) => Promise.allSettled(SHELL.map((u) => c.add(u))))  // que un recurso ausente no rompa la instalación
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (e) => {
  const req = e.request;
  if (req.method !== "GET") return;                       // POST/PUT (API) van siempre a la red
  const url = new URL(req.url);
  if (url.origin !== location.origin) return;             // recursos externos, sin tocar

  // Abrir la app (navegación): red primero; sin conexión, la página cacheada que corresponda.
  if (req.mode === "navigate") {
    const p = url.pathname;
    const destino = p.indexOf("carta") !== -1 ? "/carta.html" : p.indexOf("cocina") !== -1 ? "/cocina.html" : "/index.html";
    e.respondWith(fetch(req).catch(() => caches.match(destino).then((c) => c || caches.match("/index.html"))));
    return;
  }
  // Iconos/manifest cacheados; el resto (API incluida) a la red. Nunca servimos API desde caché.
  e.respondWith(caches.match(req).then((c) => c || fetch(req)));
});

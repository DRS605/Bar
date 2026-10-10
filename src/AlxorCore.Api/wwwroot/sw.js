// Service worker mínimo de Bar Query: hace la app instalable y da un respaldo offline del «shell».
// No cachea datos de la API (siempre van a la red), así que la información nunca queda obsoleta.
const CACHE = "bq-shell-v1";
const SHELL = ["/", "/index.html", "/manifest.json", "/icono-192.png", "/icono-512.png"];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
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

  // Abrir la app (navegación): red primero; si no hay conexión, el shell cacheado.
  if (req.mode === "navigate") {
    e.respondWith(fetch(req).catch(() => caches.match("/index.html")));
    return;
  }
  // Iconos/manifest cacheados; el resto (API incluida) a la red. Nunca servimos API desde caché.
  e.respondWith(caches.match(req).then((c) => c || fetch(req)));
});

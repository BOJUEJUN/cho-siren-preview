// Retirement worker for the legacy HTML prototype. The Unity WebGL build does
// not use a service worker; this update removes stale root-scope registrations
// and caches from browsers that visited the previous site.
self.addEventListener('install', event => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys
      .filter(key => key.startsWith('cho-siren-'))
      .map(key => caches.delete(key)));
    await self.registration.unregister();
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const gameBase = new URL('./', self.location.href);
    for (const client of clients) {
      const target = new URL(client.url);
      if (target.origin === gameBase.origin && target.pathname.startsWith(gameBase.pathname)) {
        client.navigate(client.url);
      }
    }
  })());
});

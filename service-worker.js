const CACHE_VERSION = 'cho-siren-v14';
const APP_SHELL = [
  './',
  './index.html',
  './manifest.webmanifest',
  './favicon.svg',
  './styles.css',
  './generated-assets.css',
  './secondary-polish.css',
  './topbar-row.css',
  './motion.css',
  './layout-fixes.css',
  './game-core.js',
  './app.js',
  './assets/background-portrait-display.webp',
  './assets/background-square-display.webp',
  './assets/profile-avatar.webp',
  './assets/member-xingli.webp',
  './assets/member-feiyin.webp',
  './assets/member-wubai.webp',
  './assets/member-yeying.webp',
  './assets/member-chengxia.webp',
  './assets/member-xianyue.webp',
  './assets/member-hupo.webp',
  './assets/member-yaoguang.webp',
  './assets/member-chuxue.webp',
  './assets/vfx-overlay-display.webp',
  './assets/ui-emblems-display.webp',
  './assets/nav-icons-display.webp'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_VERSION)
      .then(cache => cache.addAll(APP_SHELL))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key.startsWith('cho-siren-') && key !== CACHE_VERSION).map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response.ok) caches.open(CACHE_VERSION).then(cache => cache.put('./index.html', response.clone()));
          return response;
        })
        .catch(() => caches.match('./index.html'))
    );
    return;
  }

  const isCodeRequest = ['script', 'style', 'manifest'].includes(request.destination) || Boolean(url.search);
  if (isCodeRequest) {
    event.respondWith(
      fetch(request)
        .then(async response => {
          if (response.ok) {
            const cache = await caches.open(CACHE_VERSION);
            await cache.put(request, response.clone());
          }
          return response;
        })
        .catch(async () => {
          const exact = await caches.match(request);
          return exact || caches.match(request, { ignoreSearch: true }) || Response.error();
        })
    );
    return;
  }

  event.respondWith(
    caches.match(request).then(async cached => {
      if (cached) return cached;
      try {
        const response = await fetch(request);
        if (response.ok) {
          const cache = await caches.open(CACHE_VERSION);
          await cache.put(request, response.clone());
        }
        return response;
      } catch {
        return Response.error();
      }
    })
  );
});

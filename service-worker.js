// Local cache for the Unity WebGL build's content-addressed files.
//
// Only files whose names are derived from their own bytes are cached, so a cached copy
// can never go stale: Build/<sha256:32>.* and StreamingAssets/**/<sha256:16>-*.png.
// index.html, build-versions.json, manifests, the ink sheets and all media still come
// from the network every time. This worker replaces the earlier HTML prototype's worker
// at the same scope; activation deletes that prototype's cho-siren-v* caches.
const ASSET_CACHE = 'cho-siren-assets-1';
const LEGACY_CACHE = /^cho-siren-v\d+$/;
// Hashed art left behind by earlier art passes is evicted oldest first beyond this.
const MAX_STREAMING_ENTRIES = 240;
const scope = new URL('./', self.location.href);
const buildBase = new URL('Build/', scope).href;
const IMMUTABLE = [
  /^Build\/[0-9a-f]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$/,
  /^StreamingAssets\/(?:[\w.-]+\/)+[0-9a-f]{16}-[\w.-]+\.png$/
];

self.addEventListener('install', event => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter(key => LEGACY_CACHE.test(key)).map(key => caches.delete(key)));
    await self.clients.claim();
  })());
});

// Cache key for a content-addressed game file, or null for anything else.
function assetKey(url) {
  if (url.origin !== scope.origin || !url.pathname.startsWith(scope.pathname)) return null;
  const path = url.pathname.slice(scope.pathname.length).replace(/\/{2,}/g, '/');
  if (!IMMUTABLE.some(pattern => pattern.test(path))) return null;
  // The loader's ?retry= token is the only query the game puts on these files.
  for (const name of url.searchParams.keys()) if (name !== 'retry') return null;
  return new URL(path, scope).href;
}

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET' || request.headers.has('range')) return;
  const url = new URL(request.url);
  const key = assetKey(url);
  if (!key) return;
  event.respondWith(serve(event, request, key, url.searchParams.has('retry')));
});

async function serve(event, request, key, retry) {
  let cache = null;
  try {
    cache = await caches.open(ASSET_CACHE);
    if (!retry) {
      const hit = await cache.match(key);
      if (hit) return hit;
    }
  } catch (_) {
    // Storage unavailable (private browsing, quota): behave as if there were no worker.
  }
  // A retry always goes to the network and replaces whatever copy was stored.
  const response = await fetch(request);
  if (cache && response.status === 200 && response.type === 'basic') {
    event.waitUntil(store(cache, key, response.clone()));
  }
  return response;
}

async function store(cache, key, response) {
  try {
    await cache.put(key, response);
    if (!key.includes('/StreamingAssets/')) return;
    const art = (await cache.keys()).filter(request => request.url.includes('/StreamingAssets/'));
    await Promise.all(art.slice(0, Math.max(0, art.length - MAX_STREAMING_ENTRIES))
      .map(request => cache.delete(request)));
  } catch (_) {
    // Quota or an interrupted download: the game fetches the file again next time.
  }
}

// index.html reports the Unity builds it may still request (current and previous);
// cached files of any other build are dropped.
self.addEventListener('message', event => {
  const data = event.data;
  if (!data || data.type !== 'keep-builds' || !Array.isArray(data.files)) return;
  event.waitUntil((async () => {
    const keep = new Set(data.files.map(file => new URL(String(file), buildBase).href));
    const cache = await caches.open(ASSET_CACHE);
    const stale = (await cache.keys())
      .filter(request => request.url.startsWith(buildBase) && !keep.has(request.url));
    await Promise.all(stale.map(request => cache.delete(request)));
  })());
});

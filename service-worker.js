// Local cache for the Unity WebGL build's content-addressed files.
//
// Only files whose names are derived from their own bytes are cached, so a cached copy
// can never go stale: Build/<sha256:32>.* and StreamingAssets/**/<sha256:16>-*.png.
// Character clips (media/*.webm|mp4?v=<version>) are kept too: the ?v= token changes with
// every re-encode, and older versions of a clip are dropped when a new one is stored.
// index.html, build-versions.json, manifests, the ink sheets and other media still come
// from the network every time. This worker replaces the earlier HTML prototype's worker
// at the same scope; activation deletes that prototype's cho-siren-v* caches.
const ASSET_CACHE = 'cho-siren-assets-1';
const LEGACY_CACHE = /^cho-siren-v\d+$/;
// Hashed art left behind by earlier art passes is evicted oldest first beyond this.
const MAX_STREAMING_ENTRIES = 240;
const MEDIA_CACHE = 'cho-siren-media-1';
const MEDIA = /^media\/[\w-]+(?:\.packed|\.green)?\.(?:webm|mp4)$/;
// Clips known to be stored. The video element asks for byte ranges, and only a stored
// clip is answered here; any other range request goes to the network untouched.
const storedMedia = new Set();
const mediaReady = (async () => {
  if (!(await caches.has(MEDIA_CACHE))) return;
  const keys = await (await caches.open(MEDIA_CACHE)).keys();
  keys.forEach(request => storedMedia.add(request.url));
})().catch(() => {});
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

// Cache key for a versioned character clip, or null for anything else.
function mediaKey(url) {
  if (url.origin !== scope.origin || !url.pathname.startsWith(scope.pathname)) return null;
  const path = url.pathname.slice(scope.pathname.length).replace(/\/{2,}/g, '/');
  if (!MEDIA.test(path)) return null;
  const names = [...url.searchParams.keys()];
  if (names.length !== 1 || names[0] !== 'v') return null;
  return new URL(`${path}?v=${encodeURIComponent(url.searchParams.get('v'))}`, scope).href;
}

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const media = mediaKey(new URL(request.url));
  if (media) {
    const range = request.headers.has('range') ? request.headers.get('range') : null;
    if (!range) event.respondWith(serveMedia(event, request, media));
    else if (storedMedia.has(media)) event.respondWith(serveRange(request, media, range));
    return;
  }
  if (request.headers.has('range')) return;
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

async function serveMedia(event, request, key) {
  let cache = null;
  try {
    cache = await caches.open(MEDIA_CACHE);
    const hit = await cache.match(key);
    if (hit) return hit;
  } catch (_) {}
  const response = await fetch(request);
  if (cache && response.status === 200 && response.type === 'basic') {
    event.waitUntil(storeMedia(cache, key, response.clone()));
  }
  return response;
}

async function storeMedia(cache, key, response) {
  try {
    await cache.put(key, response);
    storedMedia.add(key);
    // Older encodes of the same clip are no longer requested by any page.
    const path = key.split('?')[0];
    const stale = (await cache.keys()).filter(request => request.url !== key && request.url.split('?')[0] === path);
    await Promise.all(stale.map(request => { storedMedia.delete(request.url); return cache.delete(request); }));
  } catch (_) {}
}

// Answers a video element's byte-range request from the stored clip with a 206.
async function serveRange(request, key, range) {
  await mediaReady;
  let hit = null;
  try { hit = await (await caches.open(MEDIA_CACHE)).match(key); } catch (_) {}
  if (!hit) {
    storedMedia.delete(key);
    return fetch(request);
  }
  const blob = await hit.blob();
  const size = blob.size;
  const match = /^bytes=(\d*)-(\d*)$/.exec(range.trim());
  let start = match && match[1] !== '' ? Number(match[1]) : NaN;
  let end = match && match[2] !== '' ? Number(match[2]) : size - 1;
  if (match && match[1] === '' && match[2] !== '') { start = Math.max(0, size - Number(match[2])); end = size - 1; }
  end = Math.min(end, size - 1);
  if (!Number.isFinite(start) || start > end || start >= size) {
    return new Response(null, { status: 416, headers: { 'Content-Range': `bytes */${size}` } });
  }
  const headers = new Headers(hit.headers);
  headers.set('Content-Range', `bytes ${start}-${end}/${size}`);
  headers.set('Content-Length', String(end - start + 1));
  headers.set('Accept-Ranges', 'bytes');
  return new Response(blob.slice(start, end + 1), { status: 206, statusText: 'Partial Content', headers });
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

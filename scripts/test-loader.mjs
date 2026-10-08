import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const suffixes = ['.data.unityweb', '.framework.js.unityweb', '.wasm.unityweb', '.loader.js'];
const newer = suffixes.map((suffix, index) => String(index + 1).repeat(32) + suffix);
const embedded = [...html.matchAll(/buildAssetUrl\("([^"\r\n]+)"\)/g)].map(match => match[1]);
const flush = () => new Promise(resolve => setImmediate(resolve));
const runningVersion = html.match(/productVersion: "([^"]+)"/)[1];

async function boot({ metadata = { schemaVersion: 1, current: newer }, httpOk = true, fetchError, pending = false, unityError, pageHref = "https://example.test/cho-siren-preview/?v=old",
  controlled = true, registerFails = false, lobbyReady = true, liveVersion = runningVersion, hidden = false,
  releaseFails = false, canvasBox = null } = {}) {
  const elements = new Map(), timers = new Map(), appends = [], calls = [], requests = [], unregistered = [],
    registered = [], messages = [], documentListeners = {};
  let timerId = 0;
  function element(name) {
    if (!elements.has(name)) elements.set(name, { style: {}, textContent: '', listeners: {}, classes: [],
      addEventListener(type, handler) { this.listeners[type] = handler; },
      classList: { add: value => elements.get(name).classes.push(value) } });
    if (name === '#unity-canvas' && canvasBox) elements.get(name).getBoundingClientRect = () => canvasBox;
    return elements.get(name);
  }
  const context = vm.createContext({ URL, AbortController, console,
    document: { baseURI: pageHref, querySelector: element, hidden,
      addEventListener: (type, handler) => { documentListeners[type] = handler; },
      createElement: () => ({}), body: { appendChild: item => appends.push(item) } },
    navigator: { serviceWorker: {
      controller: controlled ? { postMessage: message => messages.push(JSON.parse(JSON.stringify(message))) } : null,
      addEventListener() {},
      register: async (url, options) => {
        if (registerFails) throw new Error('blocked');
        registered.push({ url, scope: options.scope });
        return {};
      },
      getRegistrations: async () => ['cho-siren-preview/', 'another-game/'].map(path => ({
        scope: 'https://example.test/' + path, unregister: async () => unregistered.push(path)
      })) } },
    window: { setTimeout: callback => { timers.set(++timerId, callback); return timerId; },
      clearTimeout: id => timers.delete(id), devicePixelRatio: 3,
      choSirenStage: { active: lobbyReady },
      location: { href: pageHref, replace: url => calls.push({ redirect: url }), reload: () => calls.push({ reload: true }) } },
    fetch: async (url, options) => {
      requests.push({ url, options });
      if (String(url).includes('release.json')) {
        if (releaseFails) throw new Error('offline');
        return { ok: true, json: async () => ({ version: liveVersion }) };
      }
      if (pending) await new Promise((resolve, reject) => options.signal.addEventListener('abort', () => reject(new Error('timeout'))));
      if (fetchError) throw new Error('offline');
      return { ok: httpOk, json: async () => metadata };
    },
    createUnityInstance: async (canvas, config, progress) => {
      calls.push({ config: { ...config } });
      if (unityError) throw new Error(unityError);
      progress(1);
      return {};
    }
  });
  vm.runInContext(script, context);
  await flush(); await flush();
  return { elements, timers, appends, calls, requests, unregistered, registered, messages, context, documentListeners };
}

test('cached HTML selects all four fresh assets before starting Unity', async () => {
  const run = await boot();
  assert.equal(run.appends.length, 1);
  assert.equal(run.appends[0].src, 'https://example.test/cho-siren-preview/Build/' + newer[3]);
  await run.appends[0].onload();
  const config = run.calls[0].config;
  for (const [index, key] of ['dataUrl', 'frameworkUrl', 'codeUrl'].entries()) {
    assert.equal(config[key], 'https://example.test/cho-siren-preview/Build/' + newer[index]);
  }
  assert.equal(config.devicePixelRatio, 3);
  assert.equal(run.requests[0].options.cache, 'no-store');
  assert.equal(new URL(run.requests[0].url).pathname, '/cho-siren-preview/build-versions.json');
  assert.ok(new URL(run.requests[0].url).searchParams.has('_'));
  assert.ok(run.elements.get('#loading').classes.includes('is-hidden'));
});

for (const [name, options] of Object.entries({ offline: { fetchError: true }, missing: { httpOk: false },
  incomplete: { metadata: { schemaVersion: 1, current: newer.slice(0, 3) } },
  malicious: { metadata: { schemaVersion: 1, current: ['../../secret', ...newer.slice(1)] } },
  duplicate: { metadata: { schemaVersion: 1, current: [newer[0], newer[0], ...newer.slice(2)] } },
  unsupported: { metadata: { schemaVersion: 2, current: newer } } })) {
  test(`${name} metadata falls back to the complete embedded bundle`, async () => {
    const run = await boot(options);
    assert.equal(run.appends.length, 1);
    assert.ok(run.appends[0].src.endsWith(embedded[3]));
    await run.appends[0].onload();
    for (const [index, key] of ['dataUrl', 'frameworkUrl', 'codeUrl'].entries()) {
      assert.ok(run.calls[0].config[key].endsWith(embedded[index]));
    }
    assert.equal(run.requests.length, 1);
  });
}

test('slow version check times out and starts embedded game once', async () => {
  const run = await boot({ pending: true });
  assert.equal(run.appends.length, 0);
  for (const timer of [...run.timers.values()]) timer();
  await flush(); await flush();
  assert.equal(run.appends.length, 1);
  assert.ok(run.appends[0].src.endsWith(embedded[3]));
});

test('a normal visit registers the asset worker for this game only', async () => {
  const run = await boot();
  assert.deepEqual(run.registered, [{ url: 'https://example.test/cho-siren-preview/service-worker.js',
    scope: 'https://example.test/cho-siren-preview/' }]);
  assert.deepEqual(run.unregistered, []);
  assert.deepEqual(run.messages, [{ type: 'keep-builds', files: newer }]);
  assert.match(run.elements.get('#loading-note').textContent, /150MB.*Wi-Fi/);
});

test('a first visit waits at most 1.5 s for the new worker before starting Unity', async () => {
  const run = await boot({ controlled: false });
  assert.equal(run.appends.length, 0);
  for (const timer of [...run.timers.values()]) timer();
  await flush(); await flush();
  assert.equal(run.appends.length, 1);
});

test('a browser that refuses the worker starts Unity without waiting', async () => {
  const run = await boot({ controlled: false, registerFails: true });
  assert.equal(run.appends.length, 1);
  assert.deepEqual(run.registered, []);
});

test('retry drops only this game\'s worker, not other same-origin projects', async () => {
  const run = await boot({ pageHref: 'https://example.test/cho-siren-preview/?retry=abc' });
  assert.deepEqual(run.unregistered, ['cho-siren-preview/']);
  assert.deepEqual(run.registered, []);
});

test('the worker is told to keep the current and previous builds only', async () => {
  const previous = suffixes.map((suffix, index) => String(index + 5).repeat(32) + suffix);
  const run = await boot({ metadata: { schemaVersion: 1, current: newer, previous: [...previous, '../../secret'] } });
  assert.deepEqual(run.messages, [{ type: 'keep-builds', files: [...newer, ...previous] }]);
});

test('download progress hands over to a no-traffic start-up message', async () => {
  const run = await boot();
  const notes = [];
  Object.defineProperty(run.elements.get('#loading-note'), 'textContent', { set: text => notes.push(text), get: () => notes.at(-1) });
  await run.appends[0].onload();
  assert.ok(notes.some(text => /解压.*不消耗流量/.test(text)), notes.join(' | '));
});

test('one loading screen stays up through Unity\'s own asset loading until the lobby is live', async () => {
  const run = await boot({ lobbyReady: false });
  const started = run.appends[0].onload();
  await flush(); await flush();
  const loading = run.elements.get('#loading'), bar = run.elements.get('#progress').style;
  assert.ok(!loading.classes.includes('is-hidden'));
  const engineDone = parseInt(bar.width, 10);
  assert.ok(engineDone >= 70 && engineDone < 100, bar.width);
  assert.match(run.elements.get('#loading-note').textContent, /自动进入大厅/);
  for (let i = 0; i < 20; i++) for (const timer of [...run.timers.values()]) { run.timers.clear(); timer(); }
  assert.ok(parseInt(bar.width, 10) > engineDone && parseInt(bar.width, 10) < 100, bar.width);
  assert.ok(!loading.classes.includes('is-hidden'));
  run.context.window.choSirenStage.active = true;
  for (const timer of [...run.timers.values()]) { run.timers.clear(); timer(); }
  await started;
  assert.equal(bar.width, '100%');
  assert.equal(run.elements.get('#loading-status').textContent, '正在载入舞台资源 · 100%');
  assert.ok(loading.classes.includes('is-hidden'));
});

test('the loading screen gives up waiting for the lobby after a minute', async () => {
  const run = await boot({ lobbyReady: false });
  const started = run.appends[0].onload();
  await flush(); await flush();
  for (let i = 0; i < 500 && run.timers.size; i++) for (const timer of [...run.timers.values()]) { run.timers.clear(); timer(); }
  await started;
  assert.ok(run.elements.get('#loading').classes.includes('is-hidden'));
});

test('WASM error remains actionable even if a warning or old timer follows', async () => {
  const run = await boot({ unityError: 'both async and sync fetching of the wasm failed' });
  run.context.showBanner('earlier warning', 'warning');
  const warningTimer = [...run.timers.values()][0];
  await run.appends[0].onload();
  run.context.showBanner('later warning', 'warning');
  warningTimer();
  assert.equal(run.elements.get('#warning').style.display, 'block');
  assert.equal(run.elements.get('#retry').style.display, 'block');
  assert.match(run.elements.get('#warning-text').textContent, /重新加载/);
  assert.ok(!run.elements.get('#loading').classes.includes('is-hidden'));
  run.elements.get('#retry').listeners.click();
  const retryUrl = new URL(run.calls.find(call => call.redirect).redirect);
  assert.equal(retryUrl.pathname, '/cho-siren-preview/');
  assert.ok(retryUrl.searchParams.has('retry'));
  assert.ok(html.includes('overflow-wrap: anywhere'));
});

test('loader script failure exposes a retry without starting Unity', async () => {
  const run = await boot();
  run.appends[0].onerror();
  assert.equal(run.elements.get('#retry').style.display, 'block');
  assert.equal(run.calls.length, 0);
});

// --- service-worker.js: content-addressed asset cache ---------------------------------
const gameBase = 'https://example.test/cho-siren-preview/';
const dataFile = gameBase + 'Build/' + 'a'.repeat(32) + '.data.unityweb';
const artFile = gameBase + 'StreamingAssets/Reference038/0123456789abcdef-home-layer-01.png';

function cacheStorage(initial) {
  const stores = new Map(Object.entries(initial).map(([name, entries]) => [name, new Map(entries)]));
  const url = key => typeof key === 'string' ? key : key.url;
  const open = name => {
    if (!stores.has(name)) stores.set(name, new Map());
    const map = stores.get(name);
    return {
      match: async key => map.get(url(key)),
      // Like the Cache API, a put replaces the entry and moves it to the end.
      put: async (key, response) => { map.delete(url(key)); map.set(url(key), response); },
      keys: async () => [...map.keys()].map(entry => ({ url: entry })),
      delete: async key => map.delete(url(key))
    };
  };
  return { stores, open: async name => open(name), has: async name => stores.has(name), keys: async () => [...stores.keys()], delete: async name => stores.delete(name) };
}

function assetWorker({ cached = {}, storageFails = false } = {}) {
  const events = {}, fetched = [];
  let claimed = 0;
  const storage = cacheStorage(cached);
  const caches = storageFails ? { ...storage, open: async () => { throw new Error('denied'); } } : storage;
  vm.runInNewContext(readFileSync(new URL('../service-worker.js', import.meta.url), 'utf8'), {
    URL, caches, Response, Headers,
    fetch: async request => {
      fetched.push(request.url);
      const response = { status: 200, type: 'basic', body: 'network:' + request.url };
      return { ...response, clone: () => ({ ...response, stored: true }) };
    },
    self: { location: { href: gameBase + 'service-worker.js' },
      addEventListener: (type, handler) => { events[type] = handler; },
      skipWaiting: async () => {}, clients: { claim: async () => { claimed++; } } }
  });
  async function request(url, { method = 'GET', range = false } = {}) {
    let responded = null;
    const waits = [];
    const header = range === true ? 'bytes=0-' : range;
    events.fetch({ request: { url, method, headers: { has: name => !!range && name === 'range', get: name => name === 'range' && range ? header : null } },
      respondWith: promise => { responded = promise; }, waitUntil: promise => waits.push(promise) });
    const response = responded ? await responded : null;
    await Promise.all(waits);
    return response;
  }
  async function dispatch(type, event = {}) {
    let done;
    events[type]({ ...event, waitUntil: promise => { done = promise; } });
    await done;
  }
  return { storage, fetched, request, dispatch, claimed: () => claimed };
}

test('worker activation deletes only the prototype caches and takes control', async () => {
  const worker = assetWorker({ cached: { 'cho-siren-v20': [], 'cho-siren-assets-1': [], 'other-app': [] } });
  await worker.dispatch('activate');
  assert.deepEqual([...worker.storage.stores.keys()].sort(), ['cho-siren-assets-1', 'other-app']);
  assert.equal(worker.claimed(), 1);
});

test('content-addressed build files and art download once, then come from the cache', async () => {
  const worker = assetWorker();
  assert.equal((await worker.request(dataFile)).body, 'network:' + dataFile);
  assert.equal((await worker.request(dataFile)).stored, true);
  // Unity joins StreamingAssets paths with a double slash; both spellings share one entry.
  const doubled = artFile.replace('StreamingAssets/', 'StreamingAssets//');
  await worker.request(doubled);
  assert.equal((await worker.request(artFile)).stored, true);
  assert.deepEqual(worker.fetched, [dataFile, doubled]);
});

test('pages, manifests, unhashed art, media, ranges and other sites bypass the worker', async () => {
  const worker = assetWorker();
  for (const url of [gameBase, gameBase + 'index.html', gameBase + 'build-versions.json?_=x',
    gameBase + 'service-worker.js', gameBase + 'StreamingAssets/Reference038/manifest.json',
    gameBase + 'StreamingAssets/AlbumInkR02/ink-flow-0.png', gameBase + 'StreamingAssets/Lobby/lobby-loop.mp4',
    gameBase + 'media/catalena-look.webm', gameBase + 'media/catalena-look.webm?v=1&x=2',
    gameBase + 'media/catalena-look.voice.mp3?v=1', dataFile + '?v=2', dataFile.replace('cho-siren-preview', 'another-game'),
    dataFile.replace('example.test', 'cdn.example.test')]) {
    assert.equal(await worker.request(url), null, url);
  }
  assert.equal(await worker.request(dataFile, { method: 'POST' }), null);
  assert.equal(await worker.request(dataFile, { range: true }), null);
  // A clip that is not stored yet streams straight from the network.
  assert.equal(await worker.request(clipFile, { range: true }), null);
  assert.deepEqual(worker.fetched, []);
});

const clipFile = gameBase + 'media/catalena-look.packed.webm?v=r2';

test('character clips download once, are kept, and replace their older encodes', async () => {
  const older = gameBase + 'media/catalena-look.packed.webm?v=r1';
  const worker = assetWorker({ cached: { 'cho-siren-media-1': [[older, { body: 'old' }]] } });
  assert.equal((await worker.request(clipFile)).body, 'network:' + clipFile);
  assert.equal((await worker.request(clipFile)).stored, true);
  assert.deepEqual(worker.fetched, [clipFile]);
  assert.deepEqual([...worker.storage.stores.get('cho-siren-media-1').keys()], [clipFile]);
});

test('a stored clip answers the video element\'s byte ranges with 206 slices', async () => {
  const bytes = new TextEncoder().encode('0123456789');
  // Like the Cache API, every match hands out a fresh body.
  const stored = { headers: new Headers({ 'Content-Type': 'video/webm' }), blob: async () => new Blob([bytes]) };
  const worker = assetWorker({ cached: { 'cho-siren-media-1': [[clipFile, stored]] } });
  await new Promise(resolve => setTimeout(resolve, 0));
  const read = async response => new TextDecoder().decode(await response.arrayBuffer());
  const head = await worker.request(clipFile, { range: 'bytes=0-' });
  assert.equal(head.status, 206);
  assert.equal(head.headers.get('Content-Range'), 'bytes 0-9/10');
  assert.equal(head.headers.get('Content-Type'), 'video/webm');
  assert.equal(await read(head), '0123456789');
  const middle = await worker.request(clipFile, { range: 'bytes=2-4' });
  assert.equal(middle.headers.get('Content-Length'), '3');
  assert.equal(await read(middle), '234');
  assert.equal(await read(await worker.request(clipFile, { range: 'bytes=-3' })), '789');
  assert.equal((await worker.request(clipFile, { range: 'bytes=20-' })).status, 416);
  assert.deepEqual(worker.fetched, []);
});

test('retry downloads again and replaces the stored copy', async () => {
  const worker = assetWorker({ cached: { 'cho-siren-assets-1': [[dataFile, { body: 'damaged' }]] } });
  assert.equal((await worker.request(dataFile + '?retry=abc')).body, 'network:' + dataFile + '?retry=abc');
  assert.equal((await worker.request(dataFile)).stored, true);
  assert.equal(worker.fetched.length, 1);
});

test('unavailable storage falls back to the network', async () => {
  const worker = assetWorker({ storageFails: true });
  assert.equal((await worker.request(dataFile)).body, 'network:' + dataFile);
});

test('only the builds named by the page stay cached; old art is evicted oldest first', async () => {
  const keep = 'b'.repeat(32) + '.wasm.unityweb', drop = 'c'.repeat(32) + '.wasm.unityweb';
  const art = Array.from({ length: 240 }, (_, i) =>
    [gameBase + `StreamingAssets/Reference038/${i.toString(16).padStart(16, '0')}-layer.png`, { body: i }]);
  const worker = assetWorker({ cached: { 'cho-siren-assets-1':
    [[gameBase + 'Build/' + keep, {}], [gameBase + 'Build/' + drop, {}], ...art] } });
  await worker.dispatch('message', { data: { type: 'keep-builds', files: [keep] } });
  const entries = () => [...worker.storage.stores.get('cho-siren-assets-1').keys()];
  assert.ok(entries().includes(gameBase + 'Build/' + keep));
  assert.ok(!entries().includes(gameBase + 'Build/' + drop));
  await worker.request(artFile);
  assert.equal(entries().filter(url => url.includes('/StreamingAssets/')).length, 240);
  assert.ok(!entries().includes(art[0][0]) && entries().includes(art[1][0]) && entries().includes(artFile));
});

for (const offline of [false, true]) {
  test(`retry bypasses stale asset caches with ${offline ? 'embedded' : 'fresh'} metadata`, async () => {
    const run = await boot({ pageHref: 'https://example.test/cho-siren-preview/?retry=test-retry', fetchError: offline });
    assert.equal(new URL(run.appends[0].src).searchParams.get('retry'), 'test-retry');
    await run.appends[0].onload();
    const config = run.calls[0].config;
    for (const key of ['dataUrl', 'frameworkUrl', 'codeUrl']) assert.equal(new URL(config[key]).searchParams.get('retry'), 'test-retry');
    assert.equal(config.cacheControl(config.frameworkUrl), 'no-store');
  });
}
test('browser double-click reaches the homepage gesture owner only while home is active', async () => {
  const run = await boot(), sent = [];
  run.context.window.choSirenUnityInstance = { SendMessage: (...args) => sent.push(args) };
  run.context.window.choSirenStage = { active: false };
  run.elements.get('#unity-canvas').listeners.dblclick();
  assert.equal(sent.length, 0);
  run.context.window.choSirenStage.active = true;
  run.elements.get('#unity-canvas').listeners.dblclick();
  assert.deepEqual(sent, [['PsdHome20260921', 'ToggleUiFromBrowser']]);
  assert.ok(!html.includes('id="character-dialogue"'), 'No independent dialogue UI should appear over the character');
});

test('a lost WebGL context offers a plain reload that keeps the cached game files', async () => {
  const run = await boot();
  await run.appends[0].onload();
  run.elements.get('#unity-canvas').listeners.webglcontextlost();
  assert.equal(run.elements.get('#warning').style.display, 'block');
  assert.match(run.elements.get('#warning-text').textContent, /后台/);
  assert.equal(run.elements.get('#retry').style.display, 'block');
  run.elements.get('#retry').listeners.click();
  assert.deepEqual(run.calls.at(-1), { reload: true });
  assert.ok(!run.calls.some(call => call.redirect));
});

test('a page brought back to the foreground offers a newer live release once', async () => {
  const run = await boot({ liveVersion: '9.9.9' });
  await run.appends[0].onload();
  run.documentListeners.visibilitychange();
  await flush(); await flush();
  const releaseChecks = () => run.requests.filter(request => String(request.url).includes('release.json'));
  assert.equal(releaseChecks()[0].options.cache, 'no-store');
  assert.equal(run.elements.get('#warning').style.display, 'block');
  assert.match(run.elements.get('#warning-text').textContent, /v9\.9\.9/);
  assert.equal(run.elements.get('#retry').textContent, '立即更新');
  run.documentListeners.visibilitychange(); await flush(); await flush();
  assert.equal(releaseChecks().length, 1);
  run.elements.get('#warning').listeners.click({ target: run.elements.get('#warning-text') });
  assert.equal(run.elements.get('#warning').style.display, 'none');
  run.elements.get('#retry').listeners.click();
  assert.deepEqual(run.calls.at(-1), { reload: true });
});

test('the same live release, a hidden page or a failed check show nothing', async () => {
  for (const options of [{}, { hidden: true, liveVersion: '9.9.9' }, { liveVersion: '9.9.9', releaseFails: true }]) {
    const run = await boot(options);
    await run.appends[0].onload();
    run.documentListeners.visibilitychange(); await flush(); await flush();
    assert.notEqual(run.elements.get('#warning').style.display, 'block');
  }
});

test('a loading error cannot be dismissed by tapping it', async () => {
  const run = await boot({ unityError: 'Failed to download file' });
  await run.appends[0].onload(); await flush();
  assert.equal(run.elements.get('#warning').style.display, 'block');
  run.elements.get('#warning').listeners.click({ target: run.elements.get('#warning-text') });
  assert.equal(run.elements.get('#warning').style.display, 'block');
});

test('the game renders at the screen density, within a pixel budget on very large screens', async () => {
  const phone = await boot({ canvasBox: { width: 390, height: 832 } });
  await phone.appends[0].onload();
  assert.equal(phone.calls[0].config.devicePixelRatio, 3);
  const wall = await boot({ canvasBox: { width: 1200, height: 2560 } });
  await wall.appends[0].onload();
  const ratio = wall.calls[0].config.devicePixelRatio;
  assert.ok(ratio >= 1 && ratio < 1.1, String(ratio));
  assert.ok(1200 * 2560 * ratio * ratio <= 3.2e6 + 1);
});

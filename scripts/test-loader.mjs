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

async function boot({ metadata = { schemaVersion: 1, current: newer }, httpOk = true, fetchError, pending = false, unityError } = {}) {
  const elements = new Map(), timers = new Map(), appends = [], calls = [], requests = [], unregistered = [];
  let timerId = 0;
  function element(name) {
    if (!elements.has(name)) elements.set(name, { style: {}, textContent: '', listeners: {}, classes: [],
      addEventListener(type, handler) { this.listeners[type] = handler; },
      classList: { add: value => elements.get(name).classes.push(value) } });
    return elements.get(name);
  }
  const context = vm.createContext({ URL, AbortController, console,
    document: { baseURI: 'https://example.test/cho-siren-preview/?v=old', querySelector: element,
      createElement: () => ({}), body: { appendChild: item => appends.push(item) } },
    navigator: { serviceWorker: { getRegistrations: async () => ['cho-siren-preview/', 'another-game/'].map(path => ({
      scope: 'https://example.test/' + path, unregister: async () => unregistered.push(path)
    })) } },
    window: { setTimeout: callback => { timers.set(++timerId, callback); return timerId; },
      clearTimeout: id => timers.delete(id), devicePixelRatio: 3,
      location: { href: 'https://example.test/cho-siren-preview/?v=old', replace: url => calls.push({ redirect: url }) } },
    fetch: async (url, options) => {
      requests.push({ url, options });
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
  return { elements, timers, appends, calls, requests, unregistered, context };
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
  assert.equal(config.devicePixelRatio, 2);
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

test('only the game service worker is retired, not other same-origin projects', async () => {
  const run = await boot();
  assert.deepEqual(run.unregistered, ['cho-siren-preview/']);
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

test('legacy retirement worker only reloads this game and clears its own caches', async () => {
  const events = {}, cleared = [], navigated = [];
  let unregistered = false, completion;
  const worker = readFileSync(new URL('../service-worker.js', import.meta.url), 'utf8');
  vm.runInNewContext(worker, { URL, caches: { keys: async () => ['cho-siren-v1', 'other-app'],
    delete: async key => cleared.push(key) }, self: {
    location: { href: 'https://example.test/cho-siren-preview/service-worker.js' },
    addEventListener: (type, handler) => { events[type] = handler; },
    registration: { unregister: async () => { unregistered = true; } },
    clients: { matchAll: async () => ['https://example.test/cho-siren-preview/?v=old', 'https://example.test/another-game/']
      .map(url => ({ url, navigate: async target => navigated.push(target) })) }
  } });
  events.activate({ waitUntil: promise => { completion = promise; } });
  await completion;
  assert.equal(unregistered, true);
  assert.deepEqual(cleared, ['cho-siren-v1']);
  assert.deepEqual(navigated, ['https://example.test/cho-siren-preview/?v=old']);
});

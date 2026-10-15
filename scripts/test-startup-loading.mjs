import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// CI reads the built page. A local pre-build run may explicitly select the Unity template.
const template = readFileSync(process.env.CHO_TEMPLATE_PATH || new URL('../index.html', import.meta.url), 'utf8');
function renderedScript() {
  const flags = { PROGRESSIVE_ASSET_LOADING: false, USE_WASM: true, SYMBOLS_FILENAME: false };
  const enabled = [true], branches = [];
  const rendered = template.split('\n').filter(line => {
    const branch = line.match(/^#if (\w+)/);
    if (branch) {
      assert.ok(branch[1] in flags, `test renderer needs flag ${branch[1]}`);
      branches.push(flags[branch[1]]);
      enabled.push(enabled.at(-1) && flags[branch[1]]);
      return false;
    }
    if (line.startsWith('#else')) {
      enabled[enabled.length - 1] = enabled.at(-2) && !branches.at(-1);
      return false;
    }
    if (line.startsWith('#endif')) { enabled.pop(); branches.pop(); return false; }
    return enabled.at(-1);
  }).join('\n');
  const values = {
    DATA_FILENAME: '1'.repeat(32) + '.data.unityweb',
    FRAMEWORK_FILENAME: '2'.repeat(32) + '.framework.js.unityweb',
    CODE_FILENAME: '3'.repeat(32) + '.wasm.unityweb',
    LOADER_FILENAME: '4'.repeat(32) + '.loader.js',
    'JSON.stringify(COMPANY_NAME)': '"Test"',
    'JSON.stringify(PRODUCT_NAME)': '"CHO-SIREN"',
    'JSON.stringify(PRODUCT_VERSION)': '"0.0.0-test"'
  };
  const script = rendered.match(/<script>([\s\S]*?)<\/script>/)[1];
  return script.replace(/\{\{\{\s*(.*?)\s*\}\}\}/g, (_, expression) => {
    assert.ok(expression in values, `test renderer needs macro ${expression}`);
    return values[expression];
  });
}
const script = renderedScript();
const flush = () => new Promise(resolve => setImmediate(resolve));

async function boot({ stageActive = false, enginePending = false } = {}) {
  const elements = new Map(), timers = new Map(), appended = [], redirects = [];
  let now = 1_700_000_000_000, timerId = 0, engineResolve, engineProgress;
  class TestDate extends Date { static now() { return now; } }
  const element = name => {
    if (!elements.has(name)) {
      const classes = new Set();
      elements.set(name, {
        style: {}, textContent: '', listeners: {},
        classList: { add: value => classes.add(value), remove: value => classes.delete(value), contains: value => classes.has(value) },
        addEventListener(type, callback) { this.listeners[type] = callback; }
      });
    }
    return elements.get(name);
  };
  const context = vm.createContext({
    URL, AbortController, Date: TestDate, console,
    document: { baseURI: 'https://example.test/cho-siren-preview/', hidden: false,
      querySelector: element, createElement: () => ({}), addEventListener() {},
      body: { appendChild: item => appended.push(item) } },
    navigator: {},
    window: {
      devicePixelRatio: 1,
      choSirenStage: { active: stageActive },
      location: { href: 'https://example.test/cho-siren-preview/', replace: url => redirects.push(url),
        reload: () => redirects.push('reload') },
      setTimeout(callback, delay = 0) {
        const id = ++timerId;
        timers.set(id, { callback, at: now + Math.max(1, delay) });
        return id;
      },
      clearTimeout: id => timers.delete(id)
    },
    fetch: async () => ({ ok: false }),
    createUnityInstance: (canvas, config, progress) => {
      engineProgress = progress;
      progress(1);
      const instance = { Module: {} };
      return enginePending ? new Promise(resolve => { engineResolve = () => resolve(instance); }) : Promise.resolve(instance);
    }
  });
  vm.runInContext(script, context);
  await flush(); await flush();
  assert.equal(appended.length, 1);
  async function advance(ms) {
    const target = now + ms;
    let count = 0;
    while (true) {
      const next = [...timers.entries()].filter(([, timer]) => timer.at <= target)
        .sort((a, b) => a[1].at - b[1].at)[0];
      if (!next) break;
      assert.ok(++count < 10000, 'timer loop exceeded its bound');
      now = next[1].at;
      timers.delete(next[0]);
      next[1].callback();
      await flush();
    }
    now = target;
    await flush(); await flush();
  }
  return {
    context, elements, redirects, advance,
    start: () => appended[0].onload(),
    report: (type, progress = 0, message = '') => context.window.choSirenStartup.report(type, progress, message),
    completeEngine: () => engineResolve(),
    engineProgress: value => engineProgress(value),
    ready: () => element('#loading').classList.contains('is-ready'),
    percent: () => element('#progress').style.width
  };
}

function assertFailed(run) {
  assert.equal(run.ready(), false);
  assert.notEqual(run.percent(), '100%');
  assert.equal(run.elements.get('#loading').classList.contains('is-hidden'), false);
  assert.equal(run.elements.get('#warning').style.display, 'block');
  assert.equal(run.elements.get('#retry').style.display, 'block');
}

test('explicit ready completes with decorative stage lights disabled', async () => {
  const run = await boot({ stageActive: false });
  const started = run.start();
  await flush();
  run.report('ready', 1);
  await run.advance(250);
  await started;
  assert.equal(run.ready(), true);
  assert.equal(run.percent(), '100%');
  assert.equal(run.context.window.choSirenStage.active, false);
});

test('lit decorative stage without ready times out visibly and cannot open the lobby', async () => {
  const run = await boot({ stageActive: true });
  const started = run.start();
  await run.advance(45_250);
  await started;
  assertFailed(run);
  run.elements.get('#retry').listeners.click();
  assert.deepEqual(run.redirects, ['reload'], 'startup retry retains valid immutable cached assets');
});

test('real asset progress maps to 70–98 percent and elapsed time cannot creep it forward', async () => {
  const run = await boot();
  const started = run.start();
  await flush();
  assert.equal(run.percent(), '70%');
  run.report('progress', 0.5, '正在准备舞台与界面素材');
  assert.equal(run.percent(), '84%');
  await run.advance(10_000);
  assert.equal(run.percent(), '84%');
  assert.equal(run.ready(), false);
  // Cached files must advance through the same explicit counter as downloads.
  run.report('progress', 0.75, '已完成缓存素材');
  assert.equal(run.percent(), '91%');
  run.report('progress', 0.25, '较迟到达的进度');
  assert.equal(run.percent(), '91%', 'late progress cannot move the visible bar backwards');
  run.report('ready', 1);
  await run.advance(250);
  await started;
  assert.equal(run.ready(), true);
});

test('a reported error stays visible and rejects a late ready report', async () => {
  const run = await boot();
  const started = run.start();
  await flush();
  run.report('error', 0.4, 'PNG decode failed: home-layer.png');
  run.report('ready', 1);
  await run.advance(1_000);
  await started;
  assertFailed(run);
  assert.match(run.elements.get('#warning-text').textContent, /PNG decode failed/);
});

test('early ready waits for the engine promise before allowing entry', async () => {
  const run = await boot({ enginePending: true });
  const started = run.start();
  run.report('ready', 1);
  await run.advance(250);
  assert.equal(run.ready(), false);
  assert.notEqual(run.percent(), '100%');
  run.completeEngine();
  await run.advance(250);
  await started;
  assert.equal(run.ready(), true);
  assert.equal(run.percent(), '100%');
});

test('the watchdog starts while createUnityInstance is still pending', async () => {
  const run = await boot({ enginePending: true });
  const started = run.start();
  await run.advance(45_250);
  await started;
  assertFailed(run);
  run.completeEngine();
  run.report('ready', 1);
  await run.advance(250);
  assertFailed(run);
});

test('ongoing progress resets the stall clock but the 180 second total deadline remains', async () => {
  const run = await boot();
  const started = run.start();
  for (let index = 1; index <= 5; index++) {
    await run.advance(30_000);
    assert.equal(run.elements.get('#warning').style.display, undefined);
    run.report('progress', index / 10, `素材 ${index}`);
  }
  await run.advance(30_250);
  await started;
  assertFailed(run);
});

test('repeated unchanged progress cannot prevent the stall deadline', async () => {
  const run = await boot();
  const started = run.start();
  run.report('progress', 0.5, '素材载入中');
  await run.advance(30_000);
  run.report('progress', 0.5, '素材载入中');
  await run.advance(15_250);
  await started;
  assertFailed(run);
});

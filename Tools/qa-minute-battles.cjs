// Isolated WebGL acceptance harness. Never attaches to a user's browser profile.
const { chromium } = require('/Users/nikizhao/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const readline = require('node:readline');
(async () => {
  const url = process.argv[2];
  if (!url) throw new Error('Pass the local or deployed preview URL');
  const out = path.resolve(__dirname, '../Artifacts/qa-minute-' + Date.now());
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' });
  const context = await browser.newContext({ viewport: { width: 480, height: 1024 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  const open = async () => {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.locator('#loading.is-hidden').waitFor({ timeout: 180000 });
    await page.waitForTimeout(3000);
  };
  const capture = async name => {
    const file = path.join(out, name.replace(/[^a-z0-9_-]/gi, '') + '.png');
    await page.screenshot({ path: file });
    console.log(JSON.stringify({ screenshot: file, errors }));
  };
  const save = async fixture => page.evaluate(async fixture => {
    const db = await new Promise((resolve, reject) => { const r = indexedDB.open('/idbfs'); r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
    const store = db.transaction('FILE_DATA', 'readonly').objectStore('FILE_DATA');
    const keys = await new Promise(resolve => { const r = store.getAllKeys(); r.onsuccess = () => resolve(r.result); });
    const key = keys.find(k => String(k).endsWith('/PlayerPrefs'));
    if (!key) throw new Error('Missing isolated PlayerPrefs');
    const record = await new Promise(resolve => { const r = store.get(key); r.onsuccess = () => resolve(r.result); });
    const bytes = new Uint8Array(record.contents), decode = new TextDecoder();
    if (decode.decode(bytes.slice(0, 8)) !== 'UnityPrf' || decode.decode(bytes.slice(17, 33)) !== 'ChoSiren.Save.v2' || bytes[33] !== 128)
      throw new Error('Unknown PlayerPrefs layout; refusing mutation');
    const length = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength).getUint32(34, true);
    const raw = decode.decode(bytes.slice(38, 38 + length));
    const value = JSON.parse(raw.replace(/\0+$/, ''));
    if (fixture) {
      const level = Math.max(1, Math.min(68, fixture.level || 1));
      const through = Math.max(0, Math.min(10, fixture.through || 0));
      value.MemberLevels = value.MemberLevels.map(() => level);
      value.Roster = null;
      value.StoryProgress = [79, 81, 83, 85, 87, 89, 91, 93, 95, 97, 100][through];
      value.ClearedStages = Array.from({ length: through }, (_, i) => ({ Id: 'stage-1-' + (i + 1), Stars: 3 }));
      value.Stamina = 120;
      value.EquippedAccessory = -1;
      const encoded = new TextEncoder().encode(JSON.stringify(value) + (raw.endsWith('\0') ? '\0' : ''));
      const next = new Uint8Array(bytes.length - length + encoded.length);
      next.set(bytes.slice(0, 38));
      new DataView(next.buffer).setUint32(34, encoded.length, true);
      next.set(encoded, 38); next.set(bytes.slice(38 + length), 38 + encoded.length);
      record.contents = next; record.timestamp = new Date();
      const tx = db.transaction('FILE_DATA', 'readwrite');
      await new Promise((resolve, reject) => { tx.oncomplete = resolve; tx.onerror = () => reject(tx.error); tx.objectStore('FILE_DATA').put(record, key); });
    }
    db.close();
    return { levels: value.MemberLevels.slice(0, 4), progress: value.StoryProgress, clears: value.ClearedStages, stamina: value.Stamina };
  }, fixture);
  await open(); await capture('home'); console.log('READY');
  for await (const line of readline.createInterface({ input: process.stdin })) {
    try {
      const action = JSON.parse(line);
      if (action.close) break;
      if (action.click) await page.mouse.click(...action.click);
      if (action.reload) await open();
      if (action.fixture) {
        await page.goto(new URL('qa-empty', url).href);
        console.log(JSON.stringify({ fixture: await save(action.fixture) }));
        await open();
      }
      await page.waitForTimeout(Math.min(action.wait || 1000, 50000));
      if (action.storage) console.log(JSON.stringify({ save: await save() }));
      await capture(action.name || 'capture');
    } catch (error) { console.log(JSON.stringify({ error: error.message })); }
  }
  await browser.close();
})().catch(error => { console.error(error); process.exitCode = 1; });

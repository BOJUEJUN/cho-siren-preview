// Actual browser screenshots in an isolated profile; no user save is modified.
// 0.3.8 fix pass: audition prev/next targets moved onto the measured hit rects
// (prev plaque screen x38-288/y1045-1162, next x471-704/y1045-1162 — the old
// (420,1060) sat on the dead counter Text between them so the whole prev chain
// was byte-identical). Idle/exit now parks on a probed noninteractive spot
// instead of (2,2), which could rest on the HUD avatar, and the counter region
// is captured as clipped state evidence instead of assumed from page shots.
const { chromium } = require(process.env.CHO_PLAYWRIGHT || '/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

(async () => {
  const url = process.argv[2];
  if (!url) throw new Error('Pass a local or public CHO WebGL URL');
  const output = path.resolve(process.argv[3] || '/tmp/cho-browser-reference-038');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true,
    executablePath: process.env.CHO_CHROME || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' });
  const context = await browser.newContext({ viewport: { width: 720, height: 1536 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const errors = [], failedRequests = [];
  const artDownloads = new Set();
  page.on('requestfinished', request => {
    if (/\/StreamingAssets\/Reference038\/[^?]+\.png/.test(request.url())) artDownloads.add(request.url());
  });
  page.on('pageerror', error => errors.push(error.message));
  page.on('requestfailed', request => failedRequests.push({ url: request.url(), error: request.failure()?.errorText }));
  const shots = [];
  const route = [], evidence = [], parkProbes = [];
  const canvas = page.locator('canvas').first();
  async function point(x, y) {
    const box = await canvas.boundingBox();
    if (!box) throw new Error('Unity canvas is not visible');
    return { x: box.x + x * box.width / 720, y: box.y + y * box.height / 1536 };
  }
  async function click(x, y, label) {
    const p = await point(x, y);
    route.push({ action: 'click', x, y, label: label || null });
    await page.mouse.click(p.x, p.y);
    await page.waitForTimeout(1100);
  }
  async function capture(name) {
    const file = path.join(output, name + '.png');
    await canvas.screenshot({ path: file });
    shots.push(file);
    route.push({ action: 'capture', name });
    console.log('CHO_BROWSER_CAPTURE ' + name);
  }
  // --- measured-state helpers: clip captures, pixel diffs, validated parking ---
  function decodePng(buf) {
    let pos = 8, w = 0, h = 0, bitDepth = 8, colorType = 6;
    const idat = [];
    while (pos + 8 <= buf.length) {
      const len = buf.readUInt32BE(pos), type = buf.toString('ascii', pos + 4, pos + 8);
      const data = buf.subarray(pos + 8, pos + 8 + len);
      if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); bitDepth = data[8]; colorType = data[9]; }
      else if (type === 'IDAT') idat.push(data);
      else if (type === 'IEND') break;
      pos += 12 + len;
    }
    const channels = { 0: 1, 2: 3, 4: 2, 6: 4 }[colorType];
    if (!channels || bitDepth !== 8) throw new Error('unsupported png ' + colorType + '/' + bitDepth);
    const raw = zlib.inflateSync(Buffer.concat(idat));
    const stride = w * channels, out = Buffer.alloc(stride * h);
    let rp = 0;
    for (let y = 0; y < h; y++) {
      const filter = raw[rp++];
      const row = raw.subarray(rp, rp + stride); rp += stride;
      const prev = y ? out.subarray((y - 1) * stride, y * stride) : null;
      const cur = out.subarray(y * stride, (y + 1) * stride);
      for (let x = 0; x < stride; x++) {
        const a = x >= channels ? cur[x - channels] : 0;
        const b = prev ? prev[x] : 0;
        const c = x >= channels && prev ? prev[x - channels] : 0;
        let v = row[x];
        if (filter === 1) v += a;
        else if (filter === 2) v += b;
        else if (filter === 3) v += (a + b) >> 1;
        else if (filter === 4) {
          const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
          v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
        cur[x] = v & 255;
      }
    }
    return { w, h, channels, data: out };
  }
  function diffRatio(a, b, threshold = 14) {
    if (!a || !b || a.w !== b.w || a.h !== b.h || a.channels !== b.channels) return 1;
    let changed = 0;
    for (let i = 0; i + 2 < a.data.length; i += a.channels) {
      if (Math.abs(a.data[i] - b.data[i]) > threshold ||
          Math.abs(a.data[i + 1] - b.data[i + 1]) > threshold ||
          (a.channels > 2 && Math.abs(a.data[i + 2] - b.data[i + 2]) > threshold)) changed++;
    }
    return changed / (a.w * a.h);
  }
  function tryDiff(before, after, threshold) {
    try { return diffRatio(decodePng(before), decodePng(after), threshold); }
    catch { return before.equals(after) ? 0 : 1; }
  }
  async function grabClip(dx, dy, w, h) {
    const box = await canvas.boundingBox();
    if (!box) throw new Error('Unity canvas is not visible');
    const sx = box.width / 720, sy = box.height / 1536;
    const x = Math.max(box.x, box.x + dx * sx), y = Math.max(box.y, box.y + dy * sy);
    return page.screenshot({ clip: {
      x, y,
      width: Math.max(1, Math.min(box.x + box.width, box.x + (dx + w) * sx) - x),
      height: Math.max(1, Math.min(box.y + box.height, box.y + (dy + h) * sy) - y),
    }});
  }
  async function saveClip(name, dx, dy, w, h) {
    const buf = await grabClip(dx, dy, w, h);
    const file = path.join(output, name + '.png');
    fs.writeFileSync(file, buf);
    shots.push(file);
    return buf;
  }
  function note(check, ok, detail) {
    evidence.push({ check, ok, detail });
    console.log('CHO_EVIDENCE ' + check + ' ' + (ok ? 'ok' : 'open') + (detail ? ' — ' + detail : ''));
  }
  // Idle/exit parking: candidates are canvas-edge strips outside every measured
  // control. Each candidate is probed on/off (pointer on it vs on a different
  // candidate) so a hidden control or ambient animation disqualifies the spot;
  // the lowest-diff candidate wins and every probe is logged for review.
  const PARK_CANDIDATES = [[717, 780], [717, 560], [3, 560], [3, 780], [360, 97]];
  let lastPark = null;
  async function parkPoint() {
    const ordered = lastPark
      ? [lastPark, ...PARK_CANDIDATES.filter(c => c[0] !== lastPark[0] || c[1] !== lastPark[1])]
      : PARK_CANDIDATES;
    let best = null;
    for (let i = 0; i < ordered.length; i++) {
      const [dx, dy] = ordered[i];
      const p = await point(dx, dy);
      await page.mouse.move(p.x, p.y);
      await page.waitForTimeout(300);
      const on = await grabClip(dx - 60, dy - 40, 120, 80);
      const [nx, ny] = ordered[(i + 1) % ordered.length];
      const np = await point(nx, ny);
      await page.mouse.move(np.x, np.y);
      await page.waitForTimeout(280);
      const off = await grabClip(dx - 60, dy - 40, 120, 80);
      const ratio = tryDiff(on, off);
      parkProbes.push({ dx, dy, ratio });
      const candidate = { dx, dy, x: p.x, y: p.y, ratio };
      if (ratio < 0.004) { best = { ...candidate, verified: true }; break; }
      if (!best || ratio < best.ratio) best = candidate;
    }
    if (best && best.verified !== true) best.verified = false;
    const chosen = await point(best.dx, best.dy);
    await page.mouse.move(chosen.x, chosen.y);
    await page.waitForTimeout(140);
    lastPark = [best.dx, best.dy];
    return { x: chosen.x, y: chosen.y, dx: best.dx, dy: best.dy, ratio: best.ratio, verified: best.verified === true };
  }
  async function idle(ms = 250) {
    const p = await parkPoint();
    await page.mouse.move(p.x, p.y);
    await page.waitForTimeout(ms);
  }
  // Outlast transient toasts before unrelated baseline captures.
  async function settle(ms = 2900) { await idle(ms); }
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('#loading.is-hidden').waitFor({ timeout: 240000 });
    const artDeadline = Date.now() + 180000;
    while (artDownloads.size < 9 && Date.now() < artDeadline) await page.waitForTimeout(250);
    if (artDownloads.size !== 9) throw new Error('Expected nine complete original PNG downloads');
    await page.waitForTimeout(4500);
    await idle();
    await capture('lobby');
    await click(689, 25, 'settings-gear');
    await idle();
    await capture('settings');
    await click(360, 924, 'settings-done');        // Done 完成 — proven close path
    // Sweep every measured navigation hit zone with a real pointer.
    for (const [name,x,y] of [['team',84,1399],['members',220,1407],['lobby',351,1411],['accessory',486,1407],['audition',627,1400]]) {
      const n = await point(x,y);
      await page.mouse.move(n.x,n.y);
      await page.waitForTimeout(200);
      await capture('nav-hover-' + name);
      await idle();
    }
    // These are the measured nonuniform lobby hit zones used across pages.
    const destinations = [['team',84,1399],['members',220,1407],['accessory',486,1407],['audition',627,1400]];
    for (const [name,x,y] of destinations) {
      await click(x,y,'nav-' + name);
      await idle();
      await capture(name);
      const navHover = await point(351,1411);
      await page.mouse.move(navHover.x,navHover.y);
      await page.waitForTimeout(250);
      await capture('nav-hover-on-' + name);
      await idle();
      if (name === 'members') {
        for (let member = 0; member < 4; member++) {
          await click(85 + member * 132,480,'member-card-' + member);
          await idle();
          await capture(member === 0 ? 'profile' : 'profile-member-' + member);
          await click(675,86,'profile-close');
        }
      }
      if (name === 'accessory') {
        for (let member = 1; member < 4; member++) {
          await click(190,350,'accessory-member-' + member); await idle(); await capture('accessory-member-' + member);
        }
      }
      if (name === 'audition') {
        // Corrected targets (board hit rects → 720x1536 screen):
        //   PreviousCandidate hit board (40,1255)-(340,1395) → screen x38-288/y1045-1162 → (170,1105)
        //   NextCandidate     hit board (560,1255)-(840,1395) → screen x471-704/y1045-1162 → (585,1105)
        //   CandidateCounter text sits between them and is raycastTarget=false (dead zone).
        const counterBox = [300, 1076, 145, 68];
        const counterIdle = await saveClip('ev-audition-counter-1', ...counterBox);
        const nx = await point(585,1105);
        await page.mouse.move(nx.x,nx.y); await page.waitForTimeout(300);
        await capture('audition-next-hover');
        await page.mouse.down(); await page.waitForTimeout(160);
        await capture('audition-next-pressed');
        await page.mouse.up(); await page.waitForTimeout(500);
        await capture('audition-next-released');
        const counterNext = await saveClip('ev-audition-counter-2', ...counterBox);
        await idle(); await capture('audition-next');
        note('audition-counter-advance', tryDiff(counterIdle, counterNext, 10) > 0.01,
          'counter region after next release must differ from the 1/10 baseline');
        const pv = await point(170,1105);
        await page.mouse.move(pv.x,pv.y); await page.waitForTimeout(300);
        await capture('audition-prev-hover');
        await page.mouse.down(); await page.waitForTimeout(160);
        await capture('audition-prev-pressed');
        await page.mouse.up(); await page.waitForTimeout(500);
        await capture('audition-prev-released');
        const counterPrev = await saveClip('ev-audition-counter-1-back', ...counterBox);
        await idle(); await capture('audition-previous');
        note('audition-counter-return', tryDiff(counterPrev, counterNext, 10) > 0.01 && tryDiff(counterPrev, counterIdle, 10) < 0.05,
          'counter must leave 2/10 and settle back at the 1/10 baseline');
        // Channel memory: online advances to 3/10, offline shows its own pool, online restores 3/10.
        await click(585,1105,'next'); await click(585,1105,'next');
        await click(535,200,'pool-offline'); await idle(); await capture('audition-offline');
        await click(215,200,'pool-online'); await idle(); await capture('audition-return-online');
      }
      await click(351,1411,'nav-lobby');
    }
    const p = await point(555,1080);
    await page.mouse.move(p.x,p.y);
    await page.waitForTimeout(250);
    await capture('lobby-hover');
    await page.mouse.down();
    await page.waitForTimeout(160);
    await capture('lobby-pressed');
    const park = await parkPoint();
    await page.mouse.move(park.x, park.y);
    await page.mouse.up();
    await page.waitForTimeout(350);
    await capture('lobby-exit');
    await click(555,1080,'live-on-stage');
    await capture('map');
    await click(587,1205,'start-challenge');
    await page.waitForTimeout(2200);
    await capture('battle');
    await click(596,874,'battle-guard-attempt');
    await capture('battle-guard-attempt');
    await click(490,1434,'battle-reroll-attempt');
    await capture('battle-reroll-attempt');
  } finally {
    const report = { url, capturedAt: new Date().toISOString(), shots, errors, failedRequests, artDownloads: [...artDownloads],
      route, evidence, parkProbes,
      note: 'Evidence flags and clipped state captures are measured signals for review; full-page screenshots still require human visual review against the accepted reference.' };
    fs.writeFileSync(path.join(output,'report.json'), JSON.stringify(report,null,2));
    await browser.close();
  }
  if (errors.length || failedRequests.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });

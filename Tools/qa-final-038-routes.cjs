// 0.3.8 supplementary evidence routes — the shots the reviews flagged as missing.
// One isolated browser session; real pointer/keyboard actions only; no save is kept.
//   * currency "+" tiles and the settings gear: hover/press/release-exit 4-state,
//     plus a real open/close of the diamond modal and a settings toggle flip.
//   * audition 上一位/下一位: 4-state chains plus clipped counter evidence for the
//     1 -> 2 -> 1 transition and selected-pool marker persistence (pool0/pool1
//     counters are different pools; a fresh 1/10 on the other channel is not a bug).
//   * members search: the entered query is captured before submit, then the
//     filtered grid and the restored grid after clearing.
//   * story 记录 history overlay, practice completion result, and the realtime
//     battle 结算 result card reached through StartChallenge with 2x speed.
// Coordinates are derived from source rects (board scale 0.833/0.8375, modal
// chrome ModalPolish038Chrome, panel PlaceTop = screen coords). Every evidence
// flag is a measured pixel-diff signal; the captures still need human review.
const { chromium } = require(process.env.CHO_PLAYWRIGHT || '/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

(async () => {
  const url = process.argv[2];
  if (!url) throw new Error('Pass a local or public CHO WebGL URL');
  const output = path.resolve(process.argv[3] || '/tmp/cho-browser-final-038');
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
  function diffFiles(nameA, nameB, threshold) {
    try {
      return tryDiff(fs.readFileSync(path.join(output, nameA + '.png')),
        fs.readFileSync(path.join(output, nameB + '.png')), threshold);
    } catch { return -1; }
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
  // hover -> press -> drag to a probed noninteractive spot -> release outside:
  // a real four-state chain that never activates the control and never parks
  // on a neighboring orbit/avatar.
  async function feedbackSequence(name, x, y) {
    const p = await point(x, y);
    await page.mouse.move(p.x, p.y); await page.waitForTimeout(350);
    await capture(name + '-hover');
    await page.mouse.down(); await page.waitForTimeout(170);
    await capture(name + '-press');
    const off = await parkPoint();
    await page.mouse.move(off.x, off.y); await page.waitForTimeout(120);
    await page.mouse.up(); await page.waitForTimeout(380);
    await capture(name + '-release-exit');
  }
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('#loading.is-hidden').waitFor({ timeout: 240000 });
    const artDeadline = Date.now() + 180000;
    while (artDownloads.size < 9 && Date.now() < artDeadline) await page.waitForTimeout(250);
    if (artDownloads.size !== 9) throw new Error('Expected nine complete original PNG downloads');
    await page.waitForTimeout(4500);
    await idle();
    await capture('final-lobby');

    // ---- Currency "+" tiles + settings gear: 4-state chains (release outside,
    // no activation), then one real open/close per family for route proof ----
    await feedbackSequence('final-plus-diamond', 325, 58);   // CurrencyPlus-diamond
    await feedbackSequence('final-plus-gold', 473, 59);      // CurrencyPlus-gold
    await feedbackSequence('final-plus-stamina', 612, 59);   // CurrencyPlus-stamina
    await feedbackSequence('final-settings-gear', 673, 47);  // Settings gear
    await click(330, 23, 'currency-plus-diamond'); await idle();
    await capture('final-currency-diamond-open');                       // real open proof
    await click(593, 477, 'close-currency-modal'); await idle();        // CloseProgression centre
    // ---- Settings: gear opens, music toggle 4-state without flipping, then a
    // real click flip and restore, Done back to lobby ----
    await click(689, 25, 'settings-gear'); await idle();
    await capture('final-settings');
    await feedbackSequence('final-setting-music', 485, 555);            // Toggle row, no flip
    const toggleIdle = await saveClip('ev-settings-toggle-idle', 400, 525, 175, 60);
    await click(485, 555, 'setting-music-toggle'); await idle();
    await capture('final-settings-music-off');
    const toggleOff = await saveClip('ev-settings-toggle-off', 400, 525, 175, 60);
    note('settings-toggle-flip', tryDiff(toggleIdle, toggleOff, 10) > 0.01,
      'music toggle region must change after a real click');
    await click(485, 555, 'setting-music-toggle');                      // restore on
    await click(360, 924, 'settings-done'); await idle();
    await capture('final-lobby-after-settings');

    // ---- Audition: prev/next 4-state + counter 1->2->1 + pool persistence ----
    // Hit rects (board px): prev (40,1255)-(340,1395) -> screen (170,1105);
    // next (560,1255)-(840,1395) -> (585,1105); counter text between is dead.
    // Counter text screen box: board (360,1298)-(520,1366) -> (300,1076,145,68).
    const COUNTER = [300, 1076, 145, 68];
    const MARKER0 = [95, 225, 255, 42];    // SelectedChannelGlow+underline under pool0 plaque
    const MARKER1 = [424, 225, 255, 42];   // same region under pool1 plaque
    await click(627, 1400, 'nav-audition'); await idle();
    await capture('final-audition');
    const counter1 = await saveClip('ev-counter-online-1', ...COUNTER);
    const marker0Online = await saveClip('ev-marker-online-active', ...MARKER0);
    await feedbackSequence('final-audition-next', 585, 1105);           // release outside: no advance
    const counterNoAdvance = await saveClip('ev-counter-no-advance', ...COUNTER);
    note('next-release-outside-no-advance', tryDiff(counter1, counterNoAdvance, 10) < 0.01,
      'release-outside on 下一位 must not advance the counter');
    await click(585, 1105, 'next-candidate'); await page.waitForTimeout(700);
    const counter2 = await saveClip('ev-counter-online-2', ...COUNTER);
    await capture('final-audition-candidate-2');
    note('counter-advance-1to2', tryDiff(counter1, counter2, 10) > 0.01,
      'counter region must change after a real 下一位 click');
    await feedbackSequence('final-audition-prev', 170, 1105);
    await click(170, 1105, 'prev-candidate'); await page.waitForTimeout(700);
    const counter1Back = await saveClip('ev-counter-online-1-back', ...COUNTER);
    await capture('final-audition-candidate-1');
    note('counter-return-2to1', tryDiff(counter2, counter1Back, 10) > 0.01 && tryDiff(counter1Back, counter1, 10) < 0.05,
      'counter must leave 2/10 and settle back at the 1/10 baseline');
    // Pool persistence: switch to pool1 then back; each pool keeps its own index.
    await click(535, 200, 'interview-pool-1'); await idle();
    await capture('final-audition-offline');
    const counterPool1 = await saveClip('ev-counter-offline', ...COUNTER);
    const marker0Offline = await saveClip('ev-marker-online-when-offline', ...MARKER0);
    await saveClip('ev-marker-offline-active', ...MARKER1);
    note('selected-pool-marker-moves', tryDiff(marker0Online, marker0Offline, 10) > 0.01,
      'pool0 marker region must differ between pool0-active and pool1-active states');
    await click(215, 200, 'interview-pool-0'); await idle();
    await capture('final-audition-online');
    const counterRestored = await saveClip('ev-counter-online-restored', ...COUNTER);
    note('pool-index-persistence', tryDiff(counterRestored, counter1, 10) < 0.05,
      'returning to pool0 restores its remembered index (channel memory) — 1/10 on a different pool is not a bug');

    // ---- Members: a real entered query -> filtered grid -> cleared ----
    await click(220, 1407, 'nav-members'); await idle();
    await capture('final-members');
    const searchIdle = await saveClip('ev-search-field-idle', 40, 349, 610, 52);
    const gridIdle = await saveClip('ev-members-grid-idle', 30, 410, 660, 700);
    const searchBar = await point(345, 375);
    await page.mouse.click(searchBar.x, searchBar.y);
    await page.waitForTimeout(500);
    await page.keyboard.insertText('星璃');                             // CJK via insertText
    await page.waitForTimeout(400);
    let searchTyped = await saveClip('ev-search-field-typed', 40, 349, 610, 52);
    if (tryDiff(searchIdle, searchTyped, 10) < 0.005) {
      // Fallback probe: re-focus and send a plain ASCII character by key events.
      await page.mouse.click(searchBar.x, searchBar.y);
      await page.waitForTimeout(300);
      await page.keyboard.type('8');
      await page.waitForTimeout(400);
      searchTyped = await saveClip('ev-search-field-typed', 40, 349, 610, 52);
      note('members-search-typed', tryDiff(searchIdle, searchTyped, 10) > 0.005,
        'CJK insertText did not land; ASCII key-event fallback used instead');
    } else {
      note('members-search-typed', true, 'search field shows the entered query before submit');
    }
    await capture('final-members-search-typed');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(900);
    await parkPoint();                                                  // defocus -> onEndEdit submits
    await page.waitForTimeout(900);
    const gridFiltered = await saveClip('ev-members-grid-filtered', 30, 410, 660, 700);
    await saveClip('ev-search-field-after-submit', 40, 349, 610, 52);
    await capture('final-members-search-result');
    note('members-search-applied', tryDiff(gridIdle, gridFiltered, 10) > 0.005,
      'grid region must change once the entered query filters the roster');
    await page.mouse.click(searchBar.x, searchBar.y);                   // clear for the restored state
    await page.waitForTimeout(300);
    for (let i = 0; i < 12; i++) await page.keyboard.press('Backspace');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(900);
    const gridCleared = await saveClip('ev-members-grid-cleared', 30, 410, 660, 700);
    await capture('final-members-search-cleared');
    note('members-search-cleared', tryDiff(gridIdle, gridCleared, 10) < 0.05,
      'clearing the query restores the unfiltered page-1 grid');

    // ---- Map -> story -> 记录 history -> back ----
    await click(351, 1411, 'nav-lobby'); await idle();
    await click(536, 1066, 'live-on-stage'); await idle();
    await capture('final-map');
    await click(176, 255, 'story-chapter-01'); await idle();
    await capture('final-story');
    await click(360, 1236, 'dialogue-advance'); await page.waitForTimeout(700);
    await capture('final-story-line2');
    await click(651, 38, 'story-history'); await idle();                // TopBar 记录 button
    await capture('final-story-history');
    note('story-history-opened', diffFiles('final-story-line2', 'final-story-history', 10) > 0.02,
      '记录 overlay must visibly differ from the dialogue state');
    await click(600, 467, 'history-close'); await idle();               // HistoryClose × (safeRoot modal)
    await capture('final-story-after-history');
    await click(61, 38, 'story-back'); await idle();
    await capture('final-map-after-story');

    // ---- Practice: real training commit -> 练习结果 -> done ----
    await click(51, 55, 'map-back'); await idle();                      // Back -> lobby
    await click(153, 631, 'practice-room'); await idle();
    await capture('final-practice');
    const practiceIdle = await grabClip(58, 582, 604, 430);             // PracticeResult region
    await click(502, 450, 'start-practice'); await page.waitForTimeout(3200);  // 1-2s move + result pop
    await capture('final-practice-result');
    const practiceDone = await grabClip(58, 582, 604, 430);
    note('practice-result-shown', tryDiff(practiceIdle, practiceDone, 10) > 0.02,
      'the result group region must change after 开始练习 commits a real level-up');
    await click(360, 1199, 'practice-done'); await settle();
    await capture('final-lobby-after-practice');

    // ---- Battle (战术演出): real StartChallenge -> 2x -> 结算 card -> map ----
    await click(536, 1066, 'live-on-stage'); await idle();
    await capture('final-map-2');
    const battleStart = Date.now();
    await click(590, 1228, 'start-challenge'); await page.waitForTimeout(2600);
    await capture('final-battle-live');
    const battleEntered = tryDiff(
      fs.readFileSync(path.join(output, 'final-map-2.png')),
      fs.readFileSync(path.join(output, 'final-battle-live.png')), 10) > 0.05;
    note('battle-started', battleEntered, 'battlefield must replace the chapter map after StartChallenge');
    if (battleEntered) {
      await click(578, 46, 'speed-2x');                                 // 2倍 keeps the wait bounded
      let settledBattle = false, polls = 0;
      let previous = await grabClip(100, 420, 520, 700);                // result-card region
      const deadline = Date.now() + 150000;
      await page.waitForTimeout(2500);
      while (Date.now() < deadline && !settledBattle) {
        const current = await grabClip(100, 420, 520, 700);
        const ratio = tryDiff(previous, current, 10);
        polls++;
        if (ratio < 0.02 && Date.now() - battleStart > 12000) {
          await page.waitForTimeout(1200);
          const again = await grabClip(100, 420, 520, 700);
          if (tryDiff(current, again, 10) < 0.02) settledBattle = true;
        }
        previous = current;
        if (!settledBattle) await page.waitForTimeout(2500);
      }
      note('battle-result-settled', settledBattle,
        `result card detected after ${polls} polls / ${Math.round((Date.now() - battleStart) / 1000)}s`);
      await capture('final-battle-result');
      const resultClip = await grabClip(100, 420, 520, 700);
      await page.waitForTimeout(1500);
      await capture('final-battle-result-steady');
      // ResultContinue sits at card-local y604 (victory -> screen ~1047) or y720
      // (defeat -> screen ~1118). Try defeat first: on a victory card (360,1118)
      // lands below the button inside dead card space; on a defeat card it is
      // the button itself.
      let returned = false;
      for (const [cx, cy] of [[360, 1118], [360, 1047]]) {
        await click(cx, cy, 'result-continue');
        await page.waitForTimeout(700);
        const current = await grabClip(100, 420, 520, 700);
        if (tryDiff(resultClip, current, 10) > 0.05) { returned = true; break; }
      }
      if (!returned) {                                                  // last resort: pause -> 退出战斗
        await click(661, 46, 'battle-pause');
        await click(360, 821, 'battle-exit');
      }
      await idle();
      await capture('final-map-returned');
      const backOnMap = diffFiles('final-map-2', 'final-map-returned', 10);
      note('battle-returned-to-map', backOnMap >= 0 && backOnMap < 0.2,
        'post-continue view should be the chapter map again (diff vs pre-battle map)');
      await click(51, 55, 'map-back'); await settle();
      await capture('final-lobby-returned');
    }

    // ---- Team orbit: clean release-exit re-proof (F5) — the pointer must end
    // on a probed blank spot, never on the neighbouring orbit card ----
    await click(84, 1399, 'nav-team'); await settle();
    await capture('final-team-idle');
    await feedbackSequence('final-team-orbit0', 357, 498);
    await idle();
    await capture('final-team-settled');
    const orbitResidual = diffFiles('final-team-idle', 'final-team-settled', 12);
    note('team-orbit-clean-exit', orbitResidual >= 0 && orbitResidual < 0.02,
      `full-page diff vs idle after release-outside (ratio ${orbitResidual.toFixed ? orbitResidual.toFixed(4) : orbitResidual})`);
  } finally {
    const report = { url, capturedAt: new Date().toISOString(), shots, errors, failedRequests, artDownloads: [...artDownloads],
      route, evidence, parkProbes,
      note: 'Supplementary evidence routes. Flags are measured signals; every capture still requires human visual review against the accepted reference.' };
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    await browser.close();
  }
  if (errors.length || failedRequests.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });

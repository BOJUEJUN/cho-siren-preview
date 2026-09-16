// 0.3.8 secondary-route sweep: every reachable modal/panel behind the five main
// pages, in an isolated browser profile (the real save is never touched).
// Coordinates are derived from the authored board rects and modal chrome in
// Assets/Scripts: map board 858x1834@0.8375, audition board 853x1844@0.833,
// accessory board 858x1832@0.7729 (content space), members 853x1844@0.833,
// team 863x1823@0.8343, profile 860x1828@0.8372, crystal modals centred 0.75 aspect.
// 0.3.8 fix pass: sign-flow targets corrected onto the measured modal buttons
// (ViewInterview (309,1294), SignCandidate (546,1266), SignCancel/SignResultStay
// (245,1010), SignConfirm/SignResultProfile (475,1010), SignModalClose (600,447));
// release-exit now parks on a probed noninteractive spot instead of +220/+130,
// which provably landed on the neighboring team orbit; the members search shot
// captures the entered query before submit; and the sign chain now proves
// confirm→success→after-sign plus a drained-diamond insufficient-balance case.
// Screenshots still need human visual review; this script only produces evidence.
const { chromium } = require(process.env.CHO_PLAYWRIGHT || '/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

(async () => {
  const url = process.argv[2];
  if (!url) throw new Error('Pass a local or public CHO WebGL URL');
  const output = path.resolve(process.argv[3] || '/tmp/cho-browser-secondary-038');
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
  // Whole-body feedback evidence on a baked-board control: hover -> press ->
  // drag to a probed noninteractive spot -> release outside (no activation,
  // page state unchanged). The release point can never be a neighboring control.
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
    await capture('sec-lobby');
    await feedbackSequence('sec-lobby-stage', 536, 1066);   // LiveOnStage baked entry

    // ---- Resource modals (user-named acceptance: 资源弹窗) ----
    // CurrencyPlus hit rects are measured: diamond(306.9,36.6,36.8,42.1),
    // gold(456,37.5,33.3,42.1), stamina(593.7,37.5,36.8,43).
    await click(330, 23, 'currency-plus-diamond'); await idle(); await capture('sec-currency-diamond');      // locked 无支付入口 state
    await click(601, 470, 'close-currency-modal');                                                          // CloseProgression
    await click(458, 23, 'currency-plus-gold'); await idle(); await capture('sec-currency-gold');
    // Hover/press/release-exit feedback on a modal action button (ModalPolish038PressFeedback).
    const exchange = await point(360, 794);
    await page.mouse.move(exchange.x, exchange.y); await page.waitForTimeout(300);
    await capture('sec-currency-exchange-hover');
    await page.mouse.down(); await page.waitForTimeout(160);
    await capture('sec-currency-exchange-pressed');
    const offTarget = await parkPoint();
    await page.mouse.move(offTarget.x, offTarget.y); await page.mouse.up(); await page.waitForTimeout(350);
    await capture('sec-currency-exchange-release-exit');
    await click(360, 794, 'currency-exchange'); await idle(); await capture('sec-currency-gold-confirm');// CurrencyExchange -> CurrencyConfirmation
    await click(360, 790, 'confirm-exchange'); await idle(); await capture('sec-currency-gold-exchanged'); // ConfirmExchange -> real exchange + toast
    await click(601, 470, 'close-currency-modal');                                                          // CloseProgression
    await click(582, 23, 'currency-plus-stamina'); await idle(); await capture('sec-currency-stamina');      // full stamina -> disabled quote
    await click(601, 470, 'close-currency-modal');

    // ---- Settings modal + reset confirmation (InfoModal) ----
    await click(689, 25, 'settings-gear'); await idle(); await capture('sec-settings');
    await click(485, 555, 'setting-music-toggle'); await idle(); await capture('sec-settings-music-off');   // Setting-音乐 Toggle
    await click(485, 555, 'setting-music-toggle');                                                          // toggle back on
    await click(360, 843, 'settings-reset'); await idle(); await capture('sec-settings-reset-confirm'); // Reset -> InfoModal
    await click(572, 513, 'infomodal-close');                                                          // InfoModal Close -> lobby
    await click(689, 25, 'settings-gear'); await click(360, 924, 'settings-done'); await idle();                      // reopen, Done -> lobby

    // ---- Daily/weekly task board ----
    await click(151, 1101, 'tasks'); await idle(); await capture('sec-taskboard');           // Tasks hotspot
    await click(532, 154, 'tab-weekly'); await idle(); await capture('sec-taskboard-weekly');     // TabWeekly
    await click(187, 154, 'tab-daily');                                                          // back to TabDaily
    await click(605, 209, 'checkin'); await idle(); await capture('sec-taskboard-checkin');    // CheckInTaskChip
    await click(614, 304, 'claim-first'); await idle(); await capture('sec-taskboard-claim');      // first Claim-* row (no-op if locked)
    await click(62, 55, 'taskboard-back');                                                            // Back -> lobby

    // ---- Practice + album lobby entries ----
    await click(153, 631, 'practice'); await idle(); await capture('sec-practice');             // PracticeRoom -> MemberPracticePanel
    await click(360, 1199, 'practice-done');                                                         // PracticeDone -> lobby
    await click(153, 880, 'album-locked'); await page.waitForTimeout(400);
    await capture('sec-album-locked-toast');                                        // AlbumProduction -> 即将开放 toast
    await settle();                                                                 // wait out the toast before the team baseline

    // ---- Team: orbit profile + captain picker ----
    await click(84, 1399, 'nav-team'); await idle(); await capture('sec-team');
    await feedbackSequence('sec-team-orbit0', 357, 498);    // TeamOrbit-0 card zone
    await click(357, 498, 'team-orbit0'); await idle(); await capture('sec-profile');              // TeamOrbit-0 -> authored dossier
    await click(620, 1421, 'deeptalk-locked'); await page.waitForTimeout(400);
    await capture('sec-profile-deeptalk-locked');                                   // affection<80 -> reason toast
    await click(675, 86, 'profile-close');                                                           // CloseTop -> team
    await click(225, 1263, 'change-leader'); await idle(); await capture('sec-captain-picker');      // ChangeLeader (118,1450,304,110) -> CaptainMemberPicker
    await click(225, 547, 'picker-ownership'); await idle(); await capture('sec-picker-all');           // PickerOwnership -> 全部 64
    await click(451, 1076, 'picker-next'); await idle(); await capture('sec-picker-page2');        // PickerNext
    await click(191, 1076, 'picker-previous');                                                         // PickerPrevious -> page 1 (owned rows)
    await click(360, 604, 'picker-row'); await idle(); await capture('sec-picker-profile');       // owned row -> dossier w/ return-to-picker
    await click(675, 86, 'profile-close');                                                           // CloseTop -> picker again
    await capture('sec-picker-returned');
    await click(622, 438, 'close-picker');                                                          // CloseProgression -> team

    // ---- Members: filters, pagination, locked dossier, search ----
    // Members board 853x1844 fits contentRoot(720x1290+246) at s=.8329:
    // screenX=360+(bx-426.5)*s, screenY=by*s-0.06. Filter row by 347..400 -> ~311.
    await click(220, 1407, 'nav-members'); await idle();
    await feedbackSequence('sec-members-ownedchip', 560, 311); // filter chip body feedback
    await click(560, 311, 'member-owned-filter'); await idle(); await capture('sec-members-owned-filter'); // MemberOwnedFilter on
    await click(560, 311, 'member-owned-filter');                                                          // owned filter off -> all 64
    await click(491, 1186, 'member-next-page'); await idle(); await capture('sec-members-page2');       // MemberNextPage (1386..1462)
    await click(91, 493, 'member-locked-card'); await idle(); await capture('sec-member-locked');         // locked silhouette dossier
    await click(631, 182, 'member-modal-close');                                                          // fallback CloseTop -> members
    await click(219, 1186, 'member-prev-page');                                                          // MemberPreviousPage -> page 1
    const searchPoint = await point(345, 375);                                      // MemberSearch bar
    const searchBefore = await saveClip('ev-members-search-idle', 40, 349, 610, 52);
    await page.mouse.click(searchPoint.x, searchPoint.y);
    await page.waitForTimeout(500);
    await page.keyboard.insertText('星璃');                                          // CJK via insertText (Unity WebGL)
    await page.waitForTimeout(400);
    const searchTyped = await saveClip('ev-members-search-typed', 40, 349, 610, 52); // field content before submit
    await capture('sec-members-search-typed');
    note('members-search-typed', tryDiff(searchBefore, searchTyped, 10) > 0.005,
      'search field region must change once the query is actually entered');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(900);
    const blurPark = await parkPoint();                                              // defocus -> onEndEdit submits
    await page.waitForTimeout(900);
    const searchResultGrid = await saveClip('ev-members-search-result-grid', 30, 410, 660, 700);
    await capture('sec-members-search');
    note('members-search-applied', tryDiff(searchBefore, searchResultGrid, 10) > 0.005,
      'grid region must change once the entered query filters the roster');
    await page.mouse.click(searchPoint.x, searchPoint.y);                            // clear search for filter evidence
    for (let i = 0; i < 12; i++) await page.keyboard.press('Backspace');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(900);
    await click(140, 311, 'member-role-filter'); await idle(); await capture('sec-members-role-filter');  // MemberRoleFilter cycles 职业

    // ---- Accessory: character selector + filters ----
    // EquipmentPolish038Selector: 640x880 centred card; member cells at
    // (28+(s%2)*294, 186+(s/2)*158, 284,150) -> first cell centre (210,589) on screen.
    await click(486, 1407, 'nav-accessory'); await idle();
    await click(632, 436, 'equipment-choose-member'); await idle(); await capture('sec-equipment-picker');     // EquipmentChooseMember -> selector
    await click(210, 589, 'equipment-member-0'); await idle(); await capture('sec-accessory-switched');   // cell 0 -> switch managed member
    await click(402, 573, 'equipment-category-filter'); await idle(); await capture('sec-accessory-category');   // EquipmentCategoryFilter
    await click(580, 573, 'equipment-owned-filter'); await idle(); await capture('sec-accessory-owned');      // EquipmentOwnedFilter

    // ---- Audition signing flow: confirm -> cancel -> confirm -> success ----
    // Audition board 853x1844 embedded page: screenX=4.75+bx*.833, screenY=by*.833.
    // Corrected targets: ViewInterview hit board (268,1500)-(463,1605) -> (309,1294);
    // SignCandidate hit board (470,1410)-(830,1628) -> (546,1266); modal buttons
    // body(30,500,200,64)/(260,500,200,64) -> (245,1010)/(475,1010); × -> (600,447).
    await click(627, 1400, 'nav-audition'); await idle();
    await click(536, 198, 'interview-pool-1'); await idle(); await capture('sec-audition-pool1');       // InterviewPool-1
    await click(215, 198, 'interview-pool-0');                                                          // InterviewPool-0
    const counterBeforeView = await saveClip('ev-audition-counter-view', 300, 1076, 145, 68);
    const toastBefore = await grabClip(60, 1238, 600, 75);                          // app Toast: bottom strip y1238-1313
    await click(309, 1294, 'view-interview'); await page.waitForTimeout(400);
    await capture('sec-audition-view-interview');                                   // ViewInterview -> trait toast
    const toastAfter = await grabClip(60, 1238, 600, 75);
    const counterAfterView = await saveClip('ev-audition-counter-view-after', 300, 1076, 145, 68);
    note('view-interview-responds', tryDiff(toastBefore, toastAfter, 10) > 0.005,
      'ViewInterview must visibly respond (trait toast), proving the click hit the button');
    note('view-interview-keeps-candidate', tryDiff(counterBeforeView, counterAfterView, 10) < 0.05,
      'ViewInterview shows a trait toast; it must not advance the candidate counter');
    await feedbackSequence('sec-sign-body', 546, 1266);                             // SignCandidate full art silhouette
    await click(546, 1266, 'sign-candidate'); await idle(); await capture('sec-sign-confirm');        // SignCandidate -> SignConfirmCard
    await click(245, 1010, 'sign-cancel');                                                          // SignCancel 再想想 -> audition
    await click(546, 1266, 'sign-candidate'); await idle();
    await click(475, 1010, 'sign-confirm'); await idle(); await capture('sec-sign-result');          // SignConfirm 确认签约 -> 签约成功 card
    await click(245, 1010, 'sign-result-stay');                                                          // SignResultStay 继续面试
    await settle(); await capture('sec-audition-after-sign');
    // Distinct-state evidence: the three captures must not be byte-identical.
    try {
      const confirmShot = fs.readFileSync(path.join(output, 'sec-sign-confirm.png'));
      const resultShot = fs.readFileSync(path.join(output, 'sec-sign-result.png'));
      const afterShot = fs.readFileSync(path.join(output, 'sec-audition-after-sign.png'));
      note('sign-flow-distinct', tryDiff(confirmShot, resultShot, 10) > 0.01 && tryDiff(resultShot, afterShot, 10) > 0.01,
        'confirm -> success -> after-sign must be three different visual states');
    } catch (error) { note('sign-flow-distinct', false, 'capture compare failed: ' + error.message); }

    // ---- Insufficient-balance signing case: drain 星钻 through the real gold
    // exchange (100 -> 2000 星光币 each), then keep signing until the confirm
    // modal locks with 星钻不足. Every step uses the real UI.
    await click(351, 1411, 'nav-lobby'); await idle();
    for (let round = 0; round < 2; round++) {
      await click(458, 23, 'currency-plus-gold'); await idle();
      await click(360, 794, 'currency-exchange'); await idle();
      await click(360, 790, 'confirm-exchange'); await idle();
      await click(601, 470, 'close-currency-modal'); await settle(1400);
    }
    await click(627, 1400, 'nav-audition'); await idle();
    let insufficient = false;
    for (let attempt = 0; attempt < 6 && !insufficient; attempt++) {
      await click(546, 1266, 'sign-candidate'); await idle();
      const modalBefore = await grabClip(115, 470, 490, 600);
      await capture('sec-sign-attempt-' + attempt);
      await click(475, 1010, 'sign-confirm'); await page.waitForTimeout(700);
      const modalAfter = await grabClip(115, 470, 490, 600);
      if (tryDiff(modalBefore, modalAfter, 10) < 0.01) {
        insufficient = true;
        await capture('sec-sign-insufficient');                       // 星钻不足 + disabled confirm stays open
        note('sign-insufficient-balance', true,
          'confirm click left the modal unchanged — confirm is disabled below the signing cost');
      } else {
        note('sign-attempt-' + attempt, true, 'signed successfully; draining toward the insufficient-balance case');
        await click(245, 1010, 'sign-result-stay'); await idle();      // SignResultStay -> audition
        await click(585, 1105, 'next-candidate'); await idle();        // Next -> different cost band
      }
    }
    if (!insufficient) note('sign-insufficient-balance', false, 'confirm kept succeeding within 6 attempts');
    await click(600, 447, 'sign-modal-close'); await idle();                        // SignModalClose ×

    // ---- Chapter map: story, drop preview, rewards, tasks, locked node ----
    await click(351, 1411, 'nav-lobby');                                                         // nav lobby
    await click(536, 1066, 'live-on-stage'); await idle(); await capture('sec-map');                 // LiveOnStage -> LevelMapPanel
    await click(176, 255, 'story-chapter-01'); await idle(); await capture('sec-story');                // StoryChapter-01 -> StoryPanel
    await click(360, 1236, 'dialogue-advance'); await idle(); await capture('sec-story-advance');       // DialogueBox advance
    await click(61, 38, 'story-back'); await idle();                                              // Back -> map
    await capture('sec-map-after-story');
    await click(536, 1066, 'live-on-stage');                                                          // guard: re-enter map if story exited early
    await idle();
    await click(245, 1265, 'drop-preview'); await idle(); await capture('sec-map-drop-preview');    // StageDropPreview -> StageLootModal
    await click(642, 248, 'close-chapter-modal');                                                          // CloseChapterModal
    await click(139, 1460, 'chapter-rewards'); await idle(); await capture('sec-map-rewards');         // ChapterRewardsModal
    await click(642, 276, 'close-chapter-modal');
    await click(407, 1460, 'chapter-tasks'); await idle(); await capture('sec-map-tasks');           // ChapterTasksModal
    await click(642, 290, 'close-chapter-modal');
    await feedbackSequence('sec-map-cta', 590, 1228);                               // StartChallenge baked CTA
    await click(330, 899, 'locked-stage'); await page.waitForTimeout(400);
    await capture('sec-map-locked-stage');                                          // Level-1-2 locked -> reason toast
    await click(51, 55, 'map-back');                                                            // Back -> lobby
    await idle(); await capture('sec-lobby-returned');
  } finally {
    const report = { url, capturedAt: new Date().toISOString(), shots, errors, failedRequests, artDownloads: [...artDownloads],
      route, evidence, parkProbes,
      note: 'Secondary-route sweep. Coordinates derive from source rects; evidence flags are measured signals, every capture still requires human visual review against the accepted reference.' };
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    await browser.close();
  }
  if (errors.length || failedRequests.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });

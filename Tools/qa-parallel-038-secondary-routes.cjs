// 0.3.8 secondary-route sweep: every reachable modal/panel behind the five main
// pages, in an isolated browser profile (the real save is never touched).
// Coordinates are derived from the authored board rects and modal chrome in
// Assets/Scripts: map board 858x1834@0.8375, audition board 853x1844@0.833,
// accessory board 858x1832@0.7729 (content space), members 853x1844@0.833,
// team 863x1823@0.8343, profile 860x1828@0.8372, crystal modals centred 0.75 aspect.
// Screenshots still need human visual review; this script only produces evidence.
const { chromium } = require(process.env.CHO_PLAYWRIGHT || '/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');

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
  const canvas = page.locator('canvas').first();
  async function point(x, y) {
    const box = await canvas.boundingBox();
    if (!box) throw new Error('Unity canvas is not visible');
    return { x: box.x + x * box.width / 720, y: box.y + y * box.height / 1536 };
  }
  async function click(x, y) {
    const p = await point(x, y);
    await page.mouse.click(p.x, p.y);
    await page.waitForTimeout(1100);
  }
  async function idle() {
    await page.mouse.move(2, 2);
    await page.waitForTimeout(250);
  }
  async function capture(name) {
    const file = path.join(output, name + '.png');
    await canvas.screenshot({ path: file });
    shots.push(file);
    console.log('CHO_BROWSER_CAPTURE ' + name);
  }
  // Whole-body feedback evidence on a baked-board control: hover -> press ->
  // drag off the control -> release outside (no activation, page state unchanged).
  async function feedbackSequence(name, x, y) {
    const p = await point(x, y);
    await page.mouse.move(p.x, p.y); await page.waitForTimeout(350);
    await capture(name + '-hover');
    await page.mouse.down(); await page.waitForTimeout(170);
    await capture(name + '-press');
    const off = await point(x > 380 ? x - 220 : x + 220, Math.min(1500, y + 130));
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
    await click(325, 58); await idle(); await capture('sec-currency-diamond');      // locked 无支付入口 state
    await click(601, 470);                                                          // CloseProgression
    await click(473, 59); await idle(); await capture('sec-currency-gold');
    // Hover/press/release-exit feedback on a modal action button (ModalPolish038PressFeedback).
    const exchange = await point(360, 794);
    await page.mouse.move(exchange.x, exchange.y); await page.waitForTimeout(300);
    await capture('sec-currency-exchange-hover');
    await page.mouse.down(); await page.waitForTimeout(160);
    await capture('sec-currency-exchange-pressed');
    const offTarget = await point(360, 620);
    await page.mouse.move(offTarget.x, offTarget.y); await page.mouse.up(); await page.waitForTimeout(350);
    await capture('sec-currency-exchange-release-exit');
    await click(360, 794); await idle(); await capture('sec-currency-gold-confirm');// CurrencyExchange -> CurrencyConfirmation
    await click(360, 790); await idle(); await capture('sec-currency-gold-exchanged'); // ConfirmExchange -> real exchange + toast
    await click(601, 470);                                                          // CloseProgression
    await click(612, 59); await idle(); await capture('sec-currency-stamina');      // full stamina -> disabled quote
    await click(601, 470);

    // ---- Settings modal + reset confirmation (InfoModal) ----
    await click(673, 47); await idle(); await capture('sec-settings');
    await click(485, 555); await idle(); await capture('sec-settings-music-off');   // Setting-音乐 Toggle
    await click(485, 555);                                                          // toggle back on
    await click(360, 843); await idle(); await capture('sec-settings-reset-confirm'); // Reset -> InfoModal
    await click(572, 513);                                                          // InfoModal Close -> lobby
    await click(673, 47); await click(360, 924); await idle();                      // reopen, Done -> lobby

    // ---- Daily/weekly task board ----
    await click(151, 1101); await idle(); await capture('sec-taskboard');           // Tasks hotspot
    await click(532, 154); await idle(); await capture('sec-taskboard-weekly');     // TabWeekly
    await click(187, 154);                                                          // back to TabDaily
    await click(605, 209); await idle(); await capture('sec-taskboard-checkin');    // CheckInTaskChip
    await click(614, 304); await idle(); await capture('sec-taskboard-claim');      // first Claim-* row (no-op if locked)
    await click(62, 55);                                                            // Back -> lobby

    // ---- Practice + album lobby entries ----
    await click(153, 631); await idle(); await capture('sec-practice');             // PracticeRoom -> MemberPracticePanel
    await click(360, 1199);                                                         // PracticeDone -> lobby
    await click(153, 880); await page.waitForTimeout(400);
    await capture('sec-album-locked-toast');                                        // AlbumProduction -> 即将开放 toast

    // ---- Team: orbit profile + captain picker ----
    await click(84, 1399); await idle(); await capture('sec-team');
    await feedbackSequence('sec-team-orbit0', 357, 498);    // TeamOrbit-0 card zone
    await click(357, 498); await idle(); await capture('sec-profile');              // TeamOrbit-0 -> authored dossier
    await click(620, 1421); await page.waitForTimeout(400);
    await capture('sec-profile-deeptalk-locked');                                   // affection<80 -> reason toast
    await click(675, 86);                                                           // CloseTop -> team
    await click(225, 1263); await idle(); await capture('sec-captain-picker');      // ChangeLeader (118,1450,304,110) -> CaptainMemberPicker
    await click(225, 547); await idle(); await capture('sec-picker-all');           // PickerOwnership -> 全部 64
    await click(451, 1076); await idle(); await capture('sec-picker-page2');        // PickerNext
    await click(191, 1076);                                                         // PickerPrevious -> page 1 (owned rows)
    await click(360, 604); await idle(); await capture('sec-picker-profile');       // owned row -> dossier w/ return-to-picker
    await click(675, 86);                                                           // CloseTop -> picker again
    await capture('sec-picker-returned');
    await click(622, 438);                                                          // CloseProgression -> team

    // ---- Members: filters, pagination, locked dossier, search ----
    // Members board 853x1844 fits contentRoot(720x1290+246) at s=.8329:
    // screenX=360+(bx-426.5)*s, screenY=by*s-0.06. Filter row by 347..400 -> ~311.
    await click(220, 1407); await idle();
    await feedbackSequence('sec-members-ownedchip', 560, 311); // filter chip body feedback
    await click(560, 311); await idle(); await capture('sec-members-owned-filter'); // MemberOwnedFilter on
    await click(560, 311);                                                          // owned filter off -> all 64
    await click(491, 1186); await idle(); await capture('sec-members-page2');       // MemberNextPage (1386..1462)
    await click(91, 493); await idle(); await capture('sec-member-locked');         // locked silhouette dossier
    await click(631, 182);                                                          // fallback CloseTop -> members
    await click(219, 1186);                                                          // MemberPreviousPage -> page 1
    const searchPoint = await point(345, 375);                                      // MemberSearch bar
    await page.mouse.click(searchPoint.x, searchPoint.y);
    await page.waitForTimeout(400);
    await page.keyboard.insertText('星璃');                                          // CJK via insertText (Unity WebGL)
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1200);
    await capture('sec-members-search');
    await page.mouse.click(searchPoint.x, searchPoint.y);                            // clear search for filter evidence
    await page.keyboard.press('Backspace');
    await page.keyboard.press('Backspace');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(900);
    await click(140, 311); await idle(); await capture('sec-members-role-filter');  // MemberRoleFilter cycles 职业

    // ---- Accessory: character selector + filters ----
    // EquipmentPolish038Selector: 640x880 centred card; member cells at
    // (28+(s%2)*294, 186+(s/2)*158, 284,150) -> first cell centre (210,589) on screen.
    await click(486, 1407); await idle();
    await click(632, 436); await idle(); await capture('sec-equipment-picker');     // EquipmentChooseMember -> selector
    await click(210, 589); await idle(); await capture('sec-accessory-switched');   // cell 0 -> switch managed member
    await click(402, 573); await idle(); await capture('sec-accessory-category');   // EquipmentCategoryFilter
    await click(580, 573); await idle(); await capture('sec-accessory-owned');      // EquipmentOwnedFilter

    // ---- Audition signing flow: confirm -> cancel -> confirm -> success ----
    // Audition board 853x1844 embedded page: screenX=4.75+bx*.833, screenY=by*.833.
    await click(627, 1400); await idle();
    await click(536, 198); await idle(); await capture('sec-audition-pool1');       // InterviewPool-1
    await click(215, 198);                                                          // InterviewPool-0
    await click(309, 1223); await page.waitForTimeout(400);
    await capture('sec-audition-view-interview');                                   // ViewInterview -> trait toast
    await feedbackSequence('sec-sign-body', 552, 1226);                             // SignCandidate full art silhouette
    await click(552, 1226); await idle(); await capture('sec-sign-confirm');        // SignCandidate -> SignConfirmCard
    await click(220, 932);                                                          // SignCancel -> audition
    await click(552, 1226); await idle();
    await click(500, 932); await idle(); await capture('sec-sign-result');          // SignConfirm -> 签约成功 card
    await click(220, 896);                                                          // SignResultStay (or safe no-op)
    await click(220, 932);                                                          // SignCancel if confirm was still open
    await idle(); await capture('sec-audition-after-sign');

    // ---- Chapter map: story, drop preview, rewards, tasks, locked node ----
    await click(351, 1411);                                                         // nav lobby
    await click(536, 1066); await idle(); await capture('sec-map');                 // LiveOnStage -> LevelMapPanel
    await click(176, 255); await idle(); await capture('sec-story');                // StoryChapter-01 -> StoryPanel
    await click(360, 1236); await idle(); await capture('sec-story-advance');       // DialogueBox advance
    await click(61, 38); await idle();                                              // Back -> map
    await capture('sec-map-after-story');
    await click(536, 1066);                                                          // guard: re-enter map if story exited early
    await idle();
    await click(245, 1265); await idle(); await capture('sec-map-drop-preview');    // StageDropPreview -> StageLootModal
    await click(642, 248);                                                          // CloseChapterModal
    await click(139, 1460); await idle(); await capture('sec-map-rewards');         // ChapterRewardsModal
    await click(642, 276);
    await click(407, 1460); await idle(); await capture('sec-map-tasks');           // ChapterTasksModal
    await click(642, 290);
    await feedbackSequence('sec-map-cta', 590, 1228);                               // StartChallenge baked CTA
    await click(330, 899); await page.waitForTimeout(400);
    await capture('sec-map-locked-stage');                                          // Level-1-2 locked -> reason toast
    await click(51, 55);                                                            // Back -> lobby
    await idle(); await capture('sec-lobby-returned');
  } finally {
    const report = { url, capturedAt: new Date().toISOString(), shots, errors, failedRequests, artDownloads: [...artDownloads],
      note: 'Secondary-route sweep. Coordinates derive from source rects; every capture still requires human visual review against the accepted reference.' };
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    await browser.close();
  }
  if (errors.length || failedRequests.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });

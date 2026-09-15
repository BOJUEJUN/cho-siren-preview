// Actual browser screenshots in an isolated profile; no user save is modified.
const { chromium } = require(process.env.CHO_PLAYWRIGHT || '/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');

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
  async function capture(name) {
    const file = path.join(output, name + '.png');
    await canvas.screenshot({ path: file });
    shots.push(file);
    console.log('CHO_BROWSER_CAPTURE ' + name);
  }
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('#loading.is-hidden').waitFor({ timeout: 240000 });
    const artDeadline = Date.now() + 180000;
    while (artDownloads.size < 9 && Date.now() < artDeadline) await page.waitForTimeout(250);
    if (artDownloads.size !== 9) throw new Error('Expected nine complete original PNG downloads');
    await page.waitForTimeout(4500);
    await capture('lobby');
    // These are the measured nonuniform lobby hit zones used across pages.
    const destinations = [['team',84,1399],['members',220,1407],['accessory',486,1407],['audition',627,1400]];
    for (const [name,x,y] of destinations) {
      await click(x,y);
      await capture(name);
      if (name === 'members') {
        await click(85,480);
        await capture('profile');
        await click(675,77);
      }
      await click(351,1411);
    }
    const p = await point(555,1080);
    await page.mouse.move(p.x,p.y);
    await page.waitForTimeout(250);
    await capture('lobby-hover');
    await page.mouse.down();
    await page.waitForTimeout(160);
    await capture('lobby-pressed');
    await page.mouse.move(5,5);
    await page.mouse.up();
    await page.waitForTimeout(350);
    await capture('lobby-exit');
    await click(555,1080);
    await capture('map');
    await click(587,1205);
    await page.waitForTimeout(2200);
    await capture('battle');
    await click(596,874);
    await capture('battle-guard-attempt');
    await click(490,1434);
    await capture('battle-reroll-attempt');
  } finally {
    const report = { url, capturedAt: new Date().toISOString(), shots, errors, failedRequests, artDownloads: [...artDownloads],
      note: 'Screenshots require visual review; captured click attempts alone do not prove gameplay action success.' };
    fs.writeFileSync(path.join(output,'report.json'), JSON.stringify(report,null,2));
    await browser.close();
  }
  if (errors.length || failedRequests.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });

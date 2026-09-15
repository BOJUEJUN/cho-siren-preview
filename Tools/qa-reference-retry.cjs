const { chromium } = require('/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
(async () => {
  const output = process.argv[3] || '/tmp/cho-reference-retry';
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless:true, executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' });
  const page = await browser.newPage({ viewport:{ width:720,height:1536 } });
  let failed = false;
  const downloaded = new Set(), errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('response', r => {
    if (r.status() === 200 && /\/Reference038\/.*\.png/.test(r.url())) downloaded.add(r.url());
  });
  await page.route('**/Reference038/*lobby-board-038.png', async route => {
    if (!failed) { failed=true; await route.fulfill({status:503,body:'test unavailable'}); }
    else await route.continue();
  });
  try {
    await page.goto(process.argv[2], {waitUntil:'domcontentloaded',timeout:60000});
    await page.locator('#loading.is-hidden').waitFor({timeout:240000});
    const deadline=Date.now()+60000;
    while (!failed && Date.now()<deadline) await page.waitForTimeout(250);
    if (!failed) throw Error('The intentional PNG failure was not exercised');
    await page.waitForTimeout(2500);
    const canvas=page.locator('canvas').first();
    await canvas.screenshot({path:output+'/failure.png'});
    const box=await canvas.boundingBox();
    await page.mouse.click(box.x+360*box.width/720,box.y+1092*box.height/1536);
    const retryDeadline=Date.now()+120000;
    while(downloaded.size<9 && Date.now()<retryDeadline) await page.waitForTimeout(250);
    if(downloaded.size!==9) throw Error('Retry did not obtain all nine PNGs');
    await page.waitForTimeout(3500);
    await canvas.screenshot({path:output+'/recovered.png'});
    fs.writeFileSync(output+'/report.json',JSON.stringify({failed,downloaded:[...downloaded],errors},null,2));
    if(errors.length) throw Error('Browser runtime errors');
    console.log('CHO_REFERENCE_RETRY_RECOVERED nine PNGs; inspect failure/recovered screenshots');
  } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});

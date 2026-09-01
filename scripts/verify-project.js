const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const index = read('index.html');
const core = read('game-core.js');
const serviceWorker = read('service-worker.js');
const manifest = JSON.parse(read('manifest.webmanifest'));
const cssFiles = fs.readdirSync(root).filter(file => file.endsWith('.css'));
const failures = [];
const notes = [];

const cleanRef = reference => reference.split(/[?#]/)[0].replace(/^\.\//, '');
const isLocalRef = reference => reference && !/^(?:[a-z]+:|\/\/|#|data:)/i.test(reference);
const ensureFile = (reference, source) => {
  if (!isLocalRef(reference)) return;
  const clean = cleanRef(reference);
  if (!clean || clean === '/') return;
  if (!fs.existsSync(path.join(root, clean))) failures.push(`${source} 引用了不存在的文件：${reference}`);
};

for (const match of index.matchAll(/(?:src|href|data-src)="([^"]+)"/g)) ensureFile(match[1], 'index.html');
for (const cssFile of cssFiles) {
  for (const match of read(cssFile).matchAll(/url\(["']?([^"')]+)["']?\)/g)) ensureFile(match[1], cssFile);
}
for (const match of serviceWorker.matchAll(/['"](\.\/[^'"]+)['"]/g)) ensureFile(match[1], 'service-worker.js');

const ids = [...index.matchAll(/\sid="([^"]+)"/g)].map(match => match[1]);
const duplicateIds = [...new Set(ids.filter((id, indexOfId) => ids.indexOf(id) !== indexOfId))];
if (duplicateIds.length) failures.push(`HTML 存在重复 id：${duplicateIds.join(', ')}`);

const requiredIds = [
  'gemBalance', 'coinBalance', 'staminaBalance', 'memberGrid', 'rosterGrid',
  'recruitDialog', 'memberDialog', 'stageButton', 'missionButton', 'dailyTaskButton',
  'signInButton', 'performanceDialog', 'beatButton', 'settingsButton', 'settingsDialog',
  'motionToggle', 'effectsToggle', 'exportSaveBtn', 'importSaveInput', 'installAppBtn',
  'profileButton', 'mailButton', 'noticeButton', 'liveButton', 'eventButton', 'shopButton',
  'inventorySortBtn', 'featureDialog', 'auditionCandidates', 'candidateInsight',
  'refreshCandidatesBtn', 'interviewCandidateBtn', 'signCandidateBtn', 'trainMemberBtn',
  'memberTalentGrid'
];
for (const id of requiredIds) if (!ids.includes(id)) failures.push(`缺少核心界面元素：#${id}`);

const coreIndex = index.indexOf('game-core.js');
const appIndex = index.indexOf('app.js');
if (coreIndex < 0 || appIndex < 0 || coreIndex > appIndex) failures.push('game-core.js 必须在 app.js 前加载');
if (!index.includes('name="viewport"') || !index.includes('viewport-fit=cover')) failures.push('缺少手机安全区 viewport 配置');
if (!index.includes('rel="manifest"')) failures.push('index.html 未连接 PWA manifest');
if (!index.includes('aria-live="polite"')) failures.push('缺少无障碍状态播报区域');
if (/<button\b[^>]*\bdata-toast=/i.test(index)) failures.push('HTML 中仍有只显示提示、没有真实操作的按钮');

if (manifest.display !== 'standalone') failures.push('manifest.display 必须为 standalone');
if (!manifest.start_url || !manifest.scope || !Array.isArray(manifest.icons) || !manifest.icons.length) failures.push('manifest 缺少 start_url、scope 或图标');
for (const icon of manifest.icons || []) ensureFile(icon.src, 'manifest.webmanifest');

const shellRefs = new Set([...serviceWorker.matchAll(/['"]\.\/([^'"]+)['"]/g)].map(match => cleanRef(match[1])));
for (const required of ['index.html', 'manifest.webmanifest', 'game-core.js', 'app.js', 'video-motion.js']) {
  if (!shellRefs.has(required)) failures.push(`离线应用壳缺少：${required}`);
}
const memberAssetRefs = [...core.matchAll(/\bimage:\s*['"]([^'"]+)['"]/g)].map(match => cleanRef(match[1]));
if (memberAssetRefs.length !== 9 || new Set(memberAssetRefs).size !== memberAssetRefs.length) {
  failures.push('9 名成员必须各自使用一张不同的立绘');
}
for (const memberAsset of memberAssetRefs) {
  ensureFile(memberAsset, 'game-core.js');
  if (!shellRefs.has(memberAsset)) failures.push(`成员立绘未加入离线缓存：${memberAsset}`);
}
if (!/cho-siren-v\d+/.test(serviceWorker)) failures.push('Service Worker 缓存版本格式无效');
if (!serviceWorker.includes("['script', 'style', 'manifest']")) failures.push('代码资源必须使用联网优先、断网回退策略');

const staticAssetRefs = new Set();
for (const match of index.matchAll(/\s(?:src|href)="([^"]+)"/g)) {
  if (isLocalRef(match[1])) staticAssetRefs.add(cleanRef(match[1]));
}
for (const cssFile of cssFiles) {
  for (const match of read(cssFile).matchAll(/url\(["']?([^"')]+)["']?\)/g)) {
    if (isLocalRef(match[1])) staticAssetRefs.add(cleanRef(match[1]));
  }
}
const staticBytes = [...staticAssetRefs]
  .filter(reference => fs.existsSync(path.join(root, reference)) && fs.statSync(path.join(root, reference)).isFile())
  .reduce((total, reference) => total + fs.statSync(path.join(root, reference)).size, 0);
const staticBudget = 1.5 * 1024 * 1024;
if (staticBytes > staticBudget) failures.push(`首屏静态资源 ${Math.round(staticBytes / 1024)}KB 超过 1536KB 预算`);
else notes.push(`首屏静态资源约 ${Math.round(staticBytes / 1024)}KB / 1536KB`);

const motionBudgets = {
  'assets/character-idle-mobile-lite.webp': 3.5 * 1024 * 1024,
  'assets/character-idle-seamless.webm': 5.5 * 1024 * 1024
};
for (const [file, budget] of Object.entries(motionBudgets)) {
  const size = fs.statSync(path.join(root, file)).size;
  if (size > budget) failures.push(`${file} 超过性能预算：${Math.round(size / 1024)}KB`);
}

if (failures.length) {
  console.error('发布检查失败：');
  failures.forEach(item => console.error(`- ${item}`));
  process.exitCode = 1;
} else {
  console.log('发布检查通过');
  notes.forEach(item => console.log(`- ${item}`));
  console.log(`- ${ids.length} 个 HTML id 均唯一`);
  console.log(`- ${staticAssetRefs.size} 个首屏本地引用均存在`);
}

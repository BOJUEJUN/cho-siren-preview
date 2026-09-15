#!/usr/bin/env node
// WebGL 发布物验收（跨平台版，替代 Test-WebGLDeliverable.ps1）。
// 校验 Builds/WebGL 产物结构、资产哈希命名、GitHub Pages 限制与孤儿文件，通过时输出 JSON 摘要。
// 用法: node Tools/Test-WebGLDeliverable.mjs [buildPath]   (默认 ../Builds/WebGL)

import { existsSync, statSync, readdirSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const toolDir = fileURLToPath(new URL('.', import.meta.url));
const buildPath = resolve(process.argv[2] ?? join(toolDir, '..', 'Builds', 'WebGL'));

const fail = (message) => {
  console.error(`[Test-WebGLDeliverable] ${message}`);
  process.exit(1);
};
const indexPath = join(buildPath, 'index.html');

if (!existsSync(indexPath)) fail(`缺少 WebGL 入口: ${indexPath}`);
if (!existsSync(join(buildPath, '.nojekyll'))) fail('缺少 GitHub Pages 标记: .nojekyll');

const htmlText = readFileSync(indexPath, 'utf8');
if (!/<canvas[^>]+width="720"[^>]+height="1536"/.test(htmlText))
  fail('WebGL canvas 不是规定的 720 x 1536 竖屏尺寸。');
if (!/--portrait-ratio:\s*720\s*\/\s*1536/.test(htmlText))
  fail('页面外壳缺少 720/1536 竖屏比例变量。');

const lobbyVideoPath = join(buildPath, 'StreamingAssets', 'Lobby', 'lobby-loop.mp4');
if (!existsSync(lobbyVideoPath)) fail(`缺少大厅循环视频: ${lobbyVideoPath}`);
if (statSync(lobbyVideoPath).size < 1024) fail('大厅循环视频异常为空。');

if (!htmlText.includes('new URL("Build/", pageUrl)'))
  fail('WebGL 资产未按 GitHub Pages 项目相对路径解析。');
if (!/Cache-Control[^>]+no-cache/.test(htmlText))
  fail('index.html 缺少绕过缓存的元数据。');

const assetPattern = /buildAssetUrl\("([^"]+\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js))"\)/g;
const assetNames = [...htmlText.matchAll(assetPattern)].map(match => match[1]);
if (assetNames.length !== 4)
  fail(`应有 4 个 WebGL 资产引用，实际 ${assetNames.length} 个。`);

const githubFileLimit = 100 * 1024 * 1024;
const recommendedDataLimit = 90 * 1024 * 1024;
const assets = assetNames.map((name) => {
  if (!/^[0-9a-f]{32}\./.test(name)) fail(`WebGL 资产不是内容哈希命名: ${name}`);
  const assetPath = join(buildPath, 'Build', name);
  if (!existsSync(assetPath)) fail(`引用的资产缺失: ${assetPath}`);
  const bytes = statSync(assetPath).size;
  if (bytes >= githubFileLimit)
    fail(`资产超过 GitHub 100 MiB 单文件上限: ${name} (${bytes} bytes)。`);
  if (name.endsWith('.data.unityweb') && bytes > recommendedDataLimit)
    console.warn(`[警告] data 包超出 90 MiB 建议预算，但仍可发布: ${name} (${bytes} bytes)`);
  const sha256 = createHash('sha256').update(readFileSync(assetPath)).digest('hex');
  return { file: name, bytes, sha256 };
});

const referenced = new Set(assetNames);
const artManifestPath = join(buildPath, 'StreamingAssets', 'Reference038', 'manifest.json');
let referenceArt = [];
if (existsSync(artManifestPath)) {
  const manifest = JSON.parse(readFileSync(artManifestPath, 'utf8'));
  if (manifest.entries?.length !== 9 || new Set(manifest.entries.map(e => e.key)).size !== 9)
    fail('Reference038 清单必须包含九张不同的界面素材');
  referenceArt = manifest.entries.map(entry => {
    if (!/^Reference038\/[0-9a-f]{16}-[a-z0-9-]+\.png$/.test(entry.file)) fail('不安全的界面素材路径');
    const bytes = readFileSync(join(buildPath, 'StreamingAssets', entry.file));
    const sha256 = createHash('sha256').update(bytes).digest('hex');
    if (!entry.file.includes(sha256.slice(0, 16))) fail('界面素材内容哈希不符: ' + entry.file);
    return { file: entry.file, bytes: bytes.length, sha256 };
  });
}
const unexpected = readdirSync(join(buildPath, 'Build'), { withFileTypes: true })
  .filter(entry => entry.isDirectory() || !referenced.has(entry.name))
  .map(entry => entry.name);
if (unexpected.length > 0)
  fail(`Build/ 内存在 index.html 未引用的孤儿条目: ${unexpected.join(', ')}`);

console.log(JSON.stringify({
  success: true,
  buildPath,
  canvas: '720x1536',
  githubPagesMarker: join(buildPath, '.nojekyll'),
  githubPerFileLimitBytes: githubFileLimit,
  preferredDataLimitBytes: recommendedDataLimit,
  lobbyVideo: { file: 'StreamingAssets/Lobby/lobby-loop.mp4', bytes: statSync(lobbyVideoPath).size },
  assets,
  referenceArt,
}, null, 2));

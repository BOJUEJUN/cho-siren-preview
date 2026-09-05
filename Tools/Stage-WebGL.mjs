// Shared, cross-platform release staging. This never commits or pushes.
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const suffixes = ['.data.unityweb', '.framework.js.unityweb', '.wasm.unityweb', '.loader.js'];
export const assetPattern = /^[a-f0-9]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$/;
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const requireThat = (condition, message) => { if (!condition) throw new Error(message); };
const sameAssets = (a, b) => a.length === b.length && [...a].sort().join() === [...b].sort().join();

export function validAssets(files) {
  return Array.isArray(files) && files.length === 4 && new Set(files).size === 4 &&
    files.every(file => typeof file === 'string' && assetPattern.test(file)) &&
    suffixes.every(suffix => files.filter(file => file.endsWith(suffix)).length === 1);
}

export function indexAssets(html) {
  const files = [...html.matchAll(/buildAssetUrl\("([^"\r\n]+)"\)/g)].map(match => match[1]);
  requireThat(validAssets(files), 'index.html must reference exactly four safe, hashed Unity assets');
  return suffixes.map(suffix => files.find(file => file.endsWith(suffix)));
}

function plainPath(path, directory = false) {
  const stat = lstatSync(path);
  requireThat(!stat.isSymbolicLink() && (directory ? stat.isDirectory() : stat.isFile()), `Unsafe path: ${path}`);
  return stat;
}

function rootPath(path) {
  const absolute = resolve(path);
  plainPath(absolute, true);
  requireThat(realpathSync(absolute) === absolute, `Root must not traverse a symlink: ${absolute}`);
  return absolute;
}

function nested(a, b) {
  const part = relative(b, a);
  return part === '' || (!isAbsolute(part) && part !== '..' && !part.startsWith('..' + sep));
}

function verifyFiles(root, files) {
  plainPath(join(root, 'Build'), true);
  return files.map(file => {
    requireThat(assetPattern.test(file), `Unsafe asset: ${file}`);
    const path = join(root, 'Build', file);
    const stat = plainPath(path);
    requireThat(stat.size > 0 && stat.size < 100 * 1024 * 1024, `Invalid asset size: ${file}`);
    requireThat(file.startsWith(hash(readFileSync(path)).slice(0, 32) + '.'), `Asset hash mismatch: ${file}`);
    return stat.size;
  });
}

function readBuild(root) {
  plainPath(join(root, 'index.html'));
  plainPath(join(root, '.nojekyll'));
  const html = readFileSync(join(root, 'index.html'), 'utf8');
  requireThat(/<canvas[^>]+width="720"[^>]+height="1536"/.test(html), 'Expected portrait Unity canvas');
  const assets = indexAssets(html);
  verifyFiles(root, assets);
  requireThat(sameAssets(readdirSync(join(root, 'Build')), assets), 'Source Build must contain only its four referenced assets');
  return { html, assets };
}

function walkFiles(root, prefix = '') {
  plainPath(root, true);
  return readdirSync(root).flatMap(name => {
    const path = join(root, name), stat = lstatSync(path), key = join(prefix, name);
    requireThat(!stat.isSymbolicLink(), `Symlink not allowed: ${path}`);
    if (stat.isDirectory()) return walkFiles(path, key);
    requireThat(stat.isFile(), `Unexpected entry: ${path}`);
    return [key];
  });
}

export function stageBuild({ buildRoot, pagesRoot, fallbackRoot, apply = false }) {
  buildRoot = rootPath(buildRoot);
  pagesRoot = rootPath(pagesRoot);
  requireThat(!nested(buildRoot, pagesRoot) && !nested(pagesRoot, buildRoot), 'Build and Pages roots must be separate');
  plainPath(join(pagesRoot, 'package.json'));
  requireThat(JSON.parse(readFileSync(join(pagesRoot, 'package.json'), 'utf8')).name === 'cho-siren-preview', 'Wrong Pages repository');
  plainPath(join(pagesRoot, 'index.html'));
  plainPath(join(pagesRoot, 'Build'), true);
  const source = readBuild(buildRoot);
  requireThat(source.html.includes('build-versions.json'), 'Current build must include the version-aware loader');
  const oldCurrent = indexAssets(readFileSync(join(pagesRoot, 'index.html'), 'utf8'));
  verifyFiles(pagesRoot, oldCurrent);
  const manifestPath = join(pagesRoot, 'build-versions.json');
  let oldPrevious = [];
  if (existsSync(manifestPath)) {
    plainPath(manifestPath);
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
    requireThat(manifest.schemaVersion === 1 && validAssets(manifest.current) && sameAssets(manifest.current, oldCurrent), 'Release manifest does not match the published index');
    requireThat(Array.isArray(manifest.previous) && (manifest.previous.length === 0 || validAssets(manifest.previous)), 'Invalid previous release');
    oldPrevious = manifest.previous;
    verifyFiles(pagesRoot, oldPrevious);
  }
  const knownFiles = [...new Set([...oldCurrent, ...oldPrevious])];
  requireThat(sameAssets(readdirSync(join(pagesRoot, 'Build')), knownFiles), 'Unknown Build entries: refusing to alter unowned files');
  let previous = sameAssets(source.assets, oldCurrent) ? oldPrevious : oldCurrent;
  let fallback;
  if (fallbackRoot) {
    requireThat(!existsSync(manifestPath) && sameAssets(source.assets, oldCurrent), 'Fallback is only for bootstrapping retention on the current release');
    fallbackRoot = rootPath(fallbackRoot);
    requireThat(!nested(fallbackRoot, pagesRoot) && !nested(pagesRoot, fallbackRoot), 'Fallback and Pages roots must be separate');
    fallback = readBuild(fallbackRoot);
    requireThat(!sameAssets(fallback.assets, source.assets), 'Fallback must be a different verified release');
    previous = fallback.assets;
  }
  const keep = [...new Set([...source.assets, ...previous])];
  const copies = source.assets.map(file => ({ source: join(buildRoot, 'Build', file), destination: join(pagesRoot, 'Build', file) }));
  if (fallback) copies.push(...fallback.assets.map(file => ({ source: join(fallbackRoot, 'Build', file), destination: join(pagesRoot, 'Build', file) })));
  const streamingRoot = join(buildRoot, 'StreamingAssets');
  const streaming = walkFiles(streamingRoot);
  requireThat(streaming.includes(join('Lobby', 'lobby-loop.mp4')), 'Missing lobby video');
  requireThat(plainPath(join(streamingRoot, 'Lobby', 'lobby-loop.mp4')).size > 1024, 'Invalid lobby video');
  const pagesStreaming = join(pagesRoot, 'StreamingAssets');
  if (existsSync(pagesStreaming)) walkFiles(pagesStreaming);
  for (const file of streaming) {
    const destination = join(pagesStreaming, file);
    // Reject file/directory collisions before any mutation.
    let parent = dirname(destination);
    while (nested(parent, pagesStreaming)) {
      if (existsSync(parent)) plainPath(parent, true);
      if (parent === pagesStreaming) break;
      parent = dirname(parent);
    }
    if (existsSync(destination)) plainPath(destination);
    copies.push({ source: join(streamingRoot, file), destination });
  }
  for (const file of ['.nojekyll', 'build-versions.json', 'index.html']) {
    const destination = join(pagesRoot, file);
    if (existsSync(destination)) plainPath(destination);
  }
  const removals = knownFiles.filter(file => !keep.includes(file));
  const manifest = { schemaVersion: 1, current: source.assets, previous };
  const changedCopies = copies.filter(item => !existsSync(item.destination) ||
    hash(readFileSync(item.source)) !== hash(readFileSync(item.destination)));
  if (apply) {
    for (const item of changedCopies) {
      mkdirSync(dirname(item.destination), { recursive: true });
      copyFileSync(item.source, item.destination);
    }
    // Dependencies first, then current-version metadata, then the page entry.
    // Keep the previous complete bundle: cached HTML must not point at deleted files.
    writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
    copyFileSync(join(buildRoot, '.nojekyll'), join(pagesRoot, '.nojekyll'));
    writeFileSync(join(pagesRoot, 'index.html'), source.html);
    for (const file of removals) unlinkSync(join(pagesRoot, 'Build', file));
    verifyFiles(pagesRoot, keep);
  }
  return { success: true, dryRun: !apply, current: source.assets, previous,
    copied: changedCopies.map(item => relative(pagesRoot, item.destination)), removed: removals };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    const value = flag => args.includes(flag) ? args[args.indexOf(flag) + 1] : undefined;
    requireThat(value('--build') && value('--pages'), 'Usage: node Stage-WebGL.mjs --build DIR --pages DIR [--fallback-build DIR] [--apply]');
    console.log(JSON.stringify(stageBuild({ buildRoot: value('--build'), pagesRoot: value('--pages'),
      fallbackRoot: value('--fallback-build'), apply: args.includes('--apply') }), null, 2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

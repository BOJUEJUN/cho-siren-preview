import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { stageBuild, suffixes } from './Stage-WebGL.mjs';

function fixture(t) {
  const root = mkdtempSync(join(realpathSync(tmpdir()), 'cho-stage-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  function build(name) {
    const path = join(root, name);
    mkdirSync(join(path, 'Build'), { recursive: true });
    mkdirSync(join(path, 'StreamingAssets', 'Lobby'), { recursive: true });
    writeFileSync(join(path, 'StreamingAssets', 'Lobby', 'lobby-loop.mp4'), Buffer.alloc(2048));
    const assets = suffixes.map(suffix => {
      const bytes = Buffer.from(name + suffix);
      const file = createHash('sha256').update(bytes).digest('hex').slice(0, 32) + suffix;
      writeFileSync(join(path, 'Build', file), bytes);
      return file;
    });
    writeFileSync(join(path, '.nojekyll'), '');
    writeFileSync(join(path, 'index.html'), '<canvas width="720" height="1536"></canvas>\n' +
      'build-versions.json\n' + assets.map(file => `buildAssetUrl("${file}")`).join('\n'));
    return { path, assets };
  }
  const a = build('a'), b = build('b'), c = build('c');
  const pages = join(root, 'pages');
  cpSync(a.path, pages, { recursive: true });
  writeFileSync(join(pages, 'package.json'), JSON.stringify({ name: 'cho-siren-preview' }));
  return { root, a, b, c, pages };
}

test('A to B retains A; B to C retains B; repeated C preserves B', t => {
  const f = fixture(t);
  stageBuild({ buildRoot: f.b.path, pagesRoot: f.pages, apply: true });
  for (const file of [...f.a.assets, ...f.b.assets]) assert.ok(existsSync(join(f.pages, 'Build', file)));
  stageBuild({ buildRoot: f.c.path, pagesRoot: f.pages, apply: true });
  for (const file of f.a.assets) assert.ok(!existsSync(join(f.pages, 'Build', file)));
  for (const file of [...f.b.assets, ...f.c.assets]) assert.ok(existsSync(join(f.pages, 'Build', file)));
  const again = stageBuild({ buildRoot: f.c.path, pagesRoot: f.pages, apply: true });
  assert.deepEqual(again.previous, f.b.assets);
  assert.deepEqual(again.copied, []);
  assert.deepEqual(again.removed, []);
});

test('dry run does not mutate index, manifest or assets', t => {
  const f = fixture(t), index = readFileSync(join(f.pages, 'index.html'), 'utf8');
  const result = stageBuild({ buildRoot: f.b.path, pagesRoot: f.pages });
  assert.equal(result.dryRun, true);
  assert.equal(readFileSync(join(f.pages, 'index.html'), 'utf8'), index);
  assert.ok(!existsSync(join(f.pages, 'build-versions.json')));
  assert.equal(readdirSync(join(f.pages, 'Build')).length, 4);
});

test('bootstrap restores verified fallback without changing the current bundle', t => {
  const f = fixture(t);
  const result = stageBuild({ buildRoot: f.a.path, pagesRoot: f.pages, fallbackRoot: f.b.path, apply: true });
  assert.deepEqual(result.current, f.a.assets);
  assert.deepEqual(result.previous, f.b.assets);
  assert.throws(() => stageBuild({ buildRoot: f.a.path, pagesRoot: f.pages, fallbackRoot: f.b.path }), /bootstrapping/);
});

test('unowned files, symlinks and corrupt bytes fail before staging', t => {
  for (const problem of ['extra', 'symlink', 'corrupt']) {
    const f = fixture(t);
    if (problem === 'extra') writeFileSync(join(f.pages, 'Build', 'notes.txt'), 'keep');
    if (problem === 'symlink') symlinkSync(f.b.path, join(f.pages, 'Build', 'link'));
    if (problem === 'corrupt') writeFileSync(join(f.b.path, 'Build', f.b.assets[0]), 'wrong bytes');
    assert.throws(() => stageBuild({ buildRoot: f.b.path, pagesRoot: f.pages, apply: true }));
    assert.ok(!existsSync(join(f.pages, 'build-versions.json')));
    assert.equal(readFileSync(join(f.pages, 'index.html'), 'utf8'), readFileSync(join(f.a.path, 'index.html'), 'utf8'));
  }
});

test('unsafe roots, incomplete manifests and missing retained files are rejected', t => {
  const f = fixture(t);
  assert.throws(() => stageBuild({ buildRoot: f.pages, pagesRoot: f.pages }), /separate/);
  stageBuild({ buildRoot: f.b.path, pagesRoot: f.pages, apply: true });
  writeFileSync(join(f.pages, 'build-versions.json'), JSON.stringify({ schemaVersion: 1, current: f.b.assets, previous: ['../secret'] }));
  assert.throws(() => stageBuild({ buildRoot: f.c.path, pagesRoot: f.pages }), /previous release/);
  writeFileSync(join(f.pages, 'build-versions.json'), JSON.stringify({ schemaVersion: 1, current: f.b.assets, previous: f.a.assets }));
  rmSync(join(f.pages, 'Build', f.a.assets[0]));
  assert.throws(() => stageBuild({ buildRoot: f.c.path, pagesRoot: f.pages }));
});

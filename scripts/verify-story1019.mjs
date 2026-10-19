// R73 original-byte verification. This file is created only in the review stage.
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const checkedRead = path => {
  const st = lstatSync(path);
  if (st.isSymbolicLink() || !st.isFile()) throw new Error(`Not a regular story file: ${path}`);
  return readFileSync(path);
};
const hash = data => createHash('sha256').update(data).digest('hex');
const expected = JSON.parse(checkedRead(join(root, 'scripts/story1019-originals-r73.json')));
const manifestBytes = checkedRead(join(root, 'StreamingAssets/Story1019/manifest.json'));
const manifest = JSON.parse(manifestBytes);
if (expected.total !== 28 || expected.entries.length !== 28 || manifest.entries.length !== 28 ||
    hash(manifestBytes) !== expected.manifestSha256) throw new Error('Story1019 manifest drift');
const known = new Map(expected.entries.map(e => [e.key, e]));
const chapterCgs = ['chapter-01-rehearsal-solo', 'chapter-01-rehearsal-arrival', 'chapter-01-alley-outro'];
if (chapterCgs.some(name => !known.has('Art/Story/Scenes1019/' + name))) throw new Error('Missing required R73c complete Chapter 01 painting');
const seen = new Set();
let bytes = 0;
for (const entry of manifest.entries) {
  const pinned = known.get(entry.key);
  if (!pinned || seen.has(entry.key) || !/^Story1019\/[a-f0-9]{16}-[a-z0-9-]+\.(?:png|jpg)$/.test(entry.file) ||
      !['key', 'file', 'sha256', 'bytes'].every(k => entry[k] === pinned[k]) ||
      !entry.file.split('/')[1].startsWith(entry.sha256.slice(0, 16) + '-')) throw new Error('Invalid story entry');
  seen.add(entry.key);
  const data = checkedRead(join(root, 'StreamingAssets', entry.file));
  if (data.length !== entry.bytes || hash(data) !== entry.sha256) throw new Error(`Story original changed: ${entry.key}`);
  if (pinned.png) {
    if (data.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a' ||
        data.readUInt32BE(16) !== pinned.png.width || data.readUInt32BE(20) !== pinned.png.height ||
        data[24] !== pinned.png.bitDepth || data[25] !== pinned.png.colorType) throw new Error('Story PNG format changed');
    if (entry.key.includes('/Portraits1019/') &&
        (data[25] !== 6 || pinned.png.alphaMin >= 255 || pinned.png.alphaMax <= 0)) throw new Error('Portrait alpha proof missing');
  }
  bytes += data.length;
}
const names = readdirSync(join(root, 'StreamingAssets/Story1019')).sort();
const wanted = ['manifest.json', ...expected.entries.map(e => e.file.split('/')[1])].sort();
if (JSON.stringify(names) !== JSON.stringify(wanted) || bytes !== expected.sourceBytes ||
    [...known.keys()].filter(k => k.includes('/Scenes1019/')).length !== 23 ||
    [...known.keys()].filter(k => k.includes('/Portraits1019/')).length !== 4 ||
    [...known.keys()].filter(k => k.includes('/Transitions1019/')).length !== 1) throw new Error('Story1019 file/category inventory differs');

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { retainedBuildFiles } from './build-retention.mjs';

const files = ['.data.unityweb', '.framework.js.unityweb', '.wasm.unityweb', '.loader.js']
  .map((suffix, i) => String(i + 1).repeat(32) + suffix);
const group = { version: '0.3.79', releaseId: 'R70', files };

test('legacy metadata has no extra server retention', () => {
  assert.deepEqual(retainedBuildFiles({ schemaVersion: 1 }), []);
});

test('complete retained cohort preserves all four historical URLs', () => {
  assert.deepEqual(retainedBuildFiles({ retained: [group] }), files);
});

test('partial or duplicate-suffix historical cohort fails closed', () => {
  for (const invalid of [files.slice(0, 3), [...files.slice(0, 3), 'a'.repeat(32) + '.data.unityweb']]) {
    assert.throws(() => retainedBuildFiles({ retained: [{ ...group, files: invalid }] }), /完整四资源/);
  }
});

test('unsafe paths and duplicate release identities are rejected', () => {
  assert.throws(() => retainedBuildFiles({ retained: [{ ...group, files: ['../Build/' + files[0], ...files.slice(1)] }] }));
  assert.throws(() => retainedBuildFiles({ retained: [group, group] }));
});

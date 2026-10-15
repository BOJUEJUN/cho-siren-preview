import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// CI executes the published plugin copy. A local run can select the Unity source.
const plugin = readFileSync(process.env.CHO_AUDIO_SAMPLERATE_PLUGIN_PATH ||
  new URL('./audio/ChoSirenAudioSampleRate.jslib', import.meta.url), 'utf8');

function load(instances = {}, enabled = 1) {
  const library = {};
  const audio = { audioWebEnabled: enabled, FAKEMOD_SAMPLERATE: 44100, audioInstances: instances };
  vm.runInNewContext(plugin, {
    LibraryManager: { library }, WEBAudio: audio,
    mergeInto: (target, entries) => Object.assign(target, entries)
  }, { filename: 'ChoSirenAudioSampleRate.jslib' });
  return { library, sampleRate: library.JS_Sound_GetAudioBufferSampleRate };
}

function paused(sampleRate) {
  return { soundClip: null, source: { isPausedMockNode: true, buffer: { sampleRate } } };
}

test('override retains the SDK ABI and only replaces its sample-rate lookup', () => {
  const { library } = load();
  assert.equal(library.JS_Sound_GetAudioBufferSampleRate__proxy, 'sync');
  assert.equal(library.JS_Sound_GetAudioBufferSampleRate__sig, 'ii');
  assert.deepEqual(Array.from(library.JS_Sound_GetAudioBufferSampleRate__deps), ['$WEBAudio']);
  assert.equal(Object.values(library).filter(value => typeof value === 'function').length, 1);
});

for (const rate of [48000, 44100]) {
  test(`paused ${rate} Hz buffer retains its actual sample rate`, () => {
    const channel = paused(rate);
    const { sampleRate } = load({ 7: channel });
    assert.equal(sampleRate(7), rate);
    assert.equal(channel.soundClip, null, 'The lookup does not mutate paused channel state.');
  });
}

test('FakeMOD pitch calculation keeps resumed 48 kHz playback at rate one', () => {
  const { sampleRate } = load({ 7: paused(48000) });
  assert.equal(48000 / sampleRate(7), 1);
  assert.ok(48000 / 44100 > 1.08, 'The old fallback reproduces the observed speed increase.');
});

test('normal SoundClip frequency takes precedence over a paused source buffer', () => {
  const { sampleRate } = load({ 1: { ...paused(48000), getFrequency: () => 32000 } });
  assert.equal(sampleRate(1), 32000);
});

test('normal playing Channel keeps its SoundClip frequency lookup', () => {
  const { sampleRate } = load({ 2: { ...paused(48000), soundClip: { getFrequency: () => 22050 } } });
  assert.equal(sampleRate(2), 22050);
});

test('unknown handles retain the SDK fallback', () => {
  const { sampleRate } = load();
  assert.equal(sampleRate(99), 44100);
});

test('disabled Web audio returns the fallback without reading a clip or buffer', () => {
  const instances = new Proxy({}, { get() { throw new Error('Disabled audio must not read instances.'); } });
  assert.equal(load(instances, 0).sampleRate(1), 44100);
});

test('ordinary source buffers are not mistaken for paused mock nodes', () => {
  const { sampleRate } = load({
    1: { source: { buffer: { sampleRate: 48000 } } },
    2: { source: { isPausedMockNode: false, buffer: { sampleRate: 48000 } } }
  });
  assert.equal(sampleRate(1), 44100);
  assert.equal(sampleRate(2), 44100);
});

test('empty or invalid paused buffers retain the fallback', () => {
  for (const source of [{ isPausedMockNode: true }, ...[undefined, 0, -1, NaN].map(rate => paused(rate).source)])
    assert.equal(load({ 1: { source } }).sampleRate(1), 44100);
});

test('a SoundClip with pending zero frequency preserves original SDK behavior', () => {
  assert.equal(load({ 1: { ...paused(48000), getFrequency: () => 0 } }).sampleRate(1), 0);
});

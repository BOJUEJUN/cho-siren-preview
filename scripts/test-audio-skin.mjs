import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';

const source = fs.readFileSync(new URL('../audio-skin.js', import.meta.url), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));

function buffer({ rate = 22050, length, channels = 1, fill = 0.1 }) {
  const data = Array.from({ length: channels }, () => new Float32Array(length).fill(fill));
  return { sampleRate: rate, length, numberOfChannels: channels, duration: length / rate, getChannelData: c => data[c] };
}

// Browser-shaped doubles: attributes are accessors on the prototype, as in WebIDL.
async function setup({ fetchVoice } = {}) {
  class AudioNode { connect(destination) { (this.links ||= []).push(destination); return destination; } }
  class AudioBufferSourceNode extends AudioNode {}
  for (const name of ['buffer', 'loopStart', 'loopEnd']) {
    Object.defineProperty(AudioBufferSourceNode.prototype, name, {
      configurable: true, enumerable: true, get() { return this['_' + name]; }, set(value) { this['_' + name] = value; }
    });
  }
  const theme = buffer({ rate: 48000, length: 48000 * 66, channels: 2, fill: 0.2 });
  theme.getChannelData(0).fill(0, 0, 960);                       // decoder priming: 20 ms of silence
  const files = {
    'lobby-theme.mp3': theme,
    'ui-click.wav': buffer({ rate: 48000, length: 3600 }),
    'ui-success.wav': buffer({ rate: 48000, length: 16320 }),
    'catalena-look.voice.mp3': buffer({ rate: 48000, length: 480000, channels: 2 })
  };
  const fetched = [];
  const fetch = async url => {
    fetched.push(url);
    if (fetchVoice && url.includes('.voice.mp3')) return fetchVoice(url);
    return { ok: true, arrayBuffer: async () => url };
  };
  class OfflineAudioContext {
    decodeAudioData(url, resolve, reject) {
      const decoded = files[Object.keys(files).find(name => url.includes(`/media/${name}?`))];
      if (decoded instanceof Error) reject(decoded); else resolve(decoded);
    }
  }
  const sources = [];
  const context = {
    state: 'running', currentTime: 5, destination: new AudioNode(),
    createGain() {
      const gain = new AudioNode();
      gain.context = this;
      gain.gain = { value: 1, cancelScheduledValues() {}, setTargetAtTime(value) { this.value = value; } };
      return gain;
    },
    createBufferSource() {
      const node = new AudioBufferSourceNode();
      node.context = this;
      node.start = (...args) => { node.started = args; };
      node.stop = () => { node.stopped = true; };
      sources.push(node);
      return node;
    }
  };
  // Page-level contexts start suspended; resume() only counts the attempts.
  class AudioContext {
    constructor() { this.state = 'suspended'; this.resumed = 0; }
    resume() { this.resumed++; return Promise.resolve(); }
  }
  const listeners = {};
  const window = { AudioBufferSourceNode, AudioNode, OfflineAudioContext, AudioContext,
    addEventListener(type, listener) { (listeners[type] ||= []).push(listener); } };
  const sandbox = { window, fetch, URL, document: { baseURI: 'https://example.test/game/' } };
  vm.runInNewContext(source, sandbox);
  await flush(); await flush();
  return { window, context, sources, files, fetched, listeners, sandbox };
}

// Run both production scripts: a failed voice must release the character's actual duck owner.
function character(run) {
  const videos = [];
  const root = { dataset: { clipFormat: 'green' }, append(video) { videos.push(video); } };
  Object.assign(run.sandbox.document, {
    hidden: false, querySelector: () => root, addEventListener() {},
    createElement: () => ({ events: {}, play: () => Promise.resolve(), pause() {}, load() {},
      remove() {}, removeAttribute() {}, addEventListener(type, callback) { this.events[type] = callback; } })
  });
  Object.assign(run.sandbox, { setTimeout: () => 0, clearTimeout() {} });
  run.window.choSirenStage = { active: true };
  run.context.createBufferSource().buffer = buffer({ length: 4000 });
  vm.runInNewContext(fs.readFileSync(new URL('../character-moments.js', import.meta.url), 'utf8'), run.sandbox);
  const api = run.window.choSirenCharacter;
  return { api, videos, play(index) { api.play(index, true); videos.at(-1).events.playing(); } };
}

test('global soundtracks are never fetched, replaced or given web-owned loop points', async () => {
  const { context, fetched } = await setup();
  assert.equal(fetched.length, 2);
  assert.ok(fetched.every(url => !url.includes('lobby-theme')));
  for (const original of [buffer({ length: 176400 }), buffer({ rate: 48000, length: 48000 * 120, channels: 2 })]) {
    const music = context.createBufferSource();
    music.buffer = original;
    music.loopStart = 0; music.loopEnd = original.duration;
    const unityGain = context.createGain();
    music.connect(unityGain);
    assert.equal(music.buffer, original);
    assert.equal(music.loopEnd, original.duration);
    assert.deepEqual(music.links, [unityGain]);
  }
});

test('silent twins, real soundtracks and other clips are left exactly as Unity made them', async () => {
  const { context } = await setup();
  for (const original of [buffer({ length: 176400, fill: 0 }), buffer({ rate: 44100, length: 176400 }),
    buffer({ length: 176400, channels: 2 }), buffer({ length: 5000 })]) {
    const node = context.createBufferSource();
    node.buffer = original;
    assert.equal(node.buffer, original);
    node.loopEnd = 3;
    assert.equal(node.loopEnd, 3);
  }
});

test('clicks and success arpeggios get the composed sounds; their loop points stay Unity\'s', async () => {
  const { context, files } = await setup();
  const click = context.createBufferSource();
  click.buffer = buffer({ length: 1654 });
  assert.equal(click.buffer, files['ui-click.wav']);
  click.loopEnd = 0.075;
  assert.equal(click.loopEnd, 0.075);
  const success = context.createBufferSource();
  success.buffer = buffer({ length: 7497 });
  assert.equal(success.buffer, files['ui-success.wav']);
});

test('pending title choice and duck survive binding before the Unity instance resolves', async () => {
  const { window } = await setup();
  const audio = window.choSirenAudio;
  audio.duck(true);
  audio.setMusicEnabled(false);
  audio.bindMusicBridge('ChoSirenMusicBridge', true, true);
  assert.equal(audio.musicState.ready, false);
  assert.equal(audio.musicState.enabled, false);
  const messages = [];
  window.choSirenUnityInstance = { SendMessage(...args) { messages.push(args); } };
  audio.attachUnity();
  assert.deepEqual(messages, [
    ['ChoSirenMusicBridge', 'SetMediaMusicOwners', ''],
    ['ChoSirenMusicBridge', 'SetMusicDucked', '1'],
    ['ChoSirenMusicBridge', 'SetMusicEnabled', '0']
  ]);
  assert.equal(audio.musicState.ready, true);
  assert.equal(audio.musicState.hasSoundtrack, true);
  audio.syncMusicEnabled(false);
  assert.equal(messages.length, 3, 'state acknowledgements must not recursively send');
  audio.duck(false);
  assert.deepEqual(messages[3], ['ChoSirenMusicBridge', 'SetMusicDucked', '0']);
});

test('a saved disabled music setting wins unless the player explicitly changes it', async () => {
  const { window, listeners } = await setup();
  const messages = [];
  window.choSirenUnityInstance = { SendMessage(...args) { messages.push(args); } };
  const audio = window.choSirenAudio;
  audio.bindMusicBridge('ChoSirenMusicBridge', false, true);
  listeners.touchend[0]();
  listeners.pointerup[0]();
  assert.equal(audio.musicState.enabled, false);
  assert.ok(messages.every(message => message[1] !== 'SetMusicEnabled'));
  audio.setMusicEnabled(true);
  assert.equal(audio.musicState.enabled, true);
  assert.deepEqual(messages.at(-1), ['ChoSirenMusicBridge', 'SetMusicEnabled', '1']);
  audio.unbindMusicBridge('ChoSirenMusicBridge');
  assert.equal(audio.musicState.ready, false);
  audio.duck(false);
  assert.equal(messages.at(-1)[1], 'SetMusicEnabled');
});

test('overlapping media leases survive early Unity binding and release independently', async () => {
  const { window } = await setup();
  const audio=window.choSirenAudio;
  assert.equal(audio.musicState.settingKnown,false);
  audio.setMediaMusicActive('loading-cg',true);
  audio.setMediaMusicActive('story-cg',true);
  audio.bindMusicBridge('ChoSirenMusicBridge',false,true);
  assert.equal(audio.musicState.settingKnown,true);
  assert.equal(audio.musicState.enabled,false);
  const messages=[];
  window.choSirenUnityInstance={ SendMessage(...args) { messages.push(args); } };
  audio.attachUnity();
  assert.deepEqual(messages[0],['ChoSirenMusicBridge','SetMediaMusicOwners','loading-cg,story-cg']);
  audio.setMediaMusicActive('loading-cg',false);
  assert.equal(audio.musicState.mediaOwnsMusic,true);
  assert.deepEqual(messages.at(-1),['ChoSirenMusicBridge','SetMediaMusicOwners','story-cg']);
  const count=messages.length;
  audio.setMediaMusicActive('story-cg',true); audio.setMediaMusicActive('bad/id',true);
  assert.equal(messages.length,count);
  audio.setMediaMusicActive('story-cg',false);
  assert.equal(audio.musicState.mediaOwnsMusic,false);
  assert.deepEqual(messages.at(-1),['ChoSirenMusicBridge','SetMediaMusicOwners','']);
  assert.equal(audio.musicState.enabled,false);
});

test('voice tracks start at the video\'s time and never start once stopped', async () => {
  const { window, context, sources, files } = await setup();
  context.createBufferSource().buffer = buffer({ length: 1654 });   // Unity's context becomes known
  const before = sources.length;
  window.choSirenAudio.playVoice('catalena-look', () => 3.2);
  await flush(); await flush();
  const voice = sources[before];
  assert.equal(voice.buffer, files['catalena-look.voice.mp3']);
  assert.deepEqual(voice.links, [context.destination]);
  assert.deepEqual(voice.started, [0, 3.2]);
  const stopped = window.choSirenAudio.playVoice('catalena-look', () => 1);
  stopped.stop();
  await flush(); await flush();
  assert.equal(sources.length, before + 1);
});

for (const failure of ['fetch', 'decode']) test(`a ${failure} failure restores character music and the next tap can retry`, async () => {
  let attempts = 0;
  const run = await setup({ fetchVoice: async url => {
    if (++attempts === 1 && failure === 'fetch') throw new Error('offline');
    return { ok: true, arrayBuffer: async () => url };
  } });
  const original = run.files['catalena-look.voice.mp3'];
  if (failure === 'decode') run.files['catalena-look.voice.mp3'] = new Error('bad audio');
  const player = character(run), audio = run.window.choSirenAudio;
  player.play(0);
  assert.equal(audio.musicState.ducked, true);
  await flush();
  assert.equal(audio.musicState.ducked, false);
  player.videos.at(-1).events.ended();
  assert.equal(audio.musicState.ducked, false);
  run.files['catalena-look.voice.mp3'] = original;
  player.play(0);
  await flush();
  assert.equal(attempts, 2);
  assert.equal(audio.musicState.ducked, true);
  run.sources.at(-1).onended();
  await flush();
  assert.equal(audio.musicState.ducked, false);
});

test('late failure, cancellation and old ended callbacks cannot release a replacement voice duck', async () => {
  const pending = new Map();
  const run = await setup({ fetchVoice: url => new Promise((resolve, reject) => {
    pending.set(new URL(url).pathname.split('/').at(-1), {
      resolve: () => resolve({ ok: true, arrayBuffer: async () => url }), reject
    });
  }) });
  run.files['catalena-whisper.voice.mp3'] = run.files['catalena-look.voice.mp3'];
  run.files['catalena-live.voice.mp3'] = run.files['catalena-look.voice.mp3'];
  const player = character(run), audio = run.window.choSirenAudio;
  player.play(0);
  player.play(1); // Cancels the still-loading voice; its completion arrives after this handle exists.
  pending.get('catalena-whisper.voice.mp3').resolve();
  await flush();
  pending.get('catalena-look.voice.mp3').reject(new Error('late network failure'));
  await flush();
  assert.equal(audio.musicState.ducked, true);
  const oldSource = run.sources.at(-1);
  player.play(2);
  pending.get('catalena-live.voice.mp3').resolve();
  await flush();
  oldSource.onended();
  await flush();
  assert.equal(audio.musicState.ducked, true);
  player.api.close();
  assert.equal(audio.musicState.ducked, false);
  run.sources.at(-1).onended();
  await flush();
  assert.equal(audio.musicState.ducked, false);
});

test('a tap resumes Unity\'s suspended or interrupted context and leaves a running one alone', async () => {
  const { window, listeners } = await setup();
  const unity = new window.AudioContext();
  unity.resume();                                   // Unity's own retry, refused outside a tap on iOS
  assert.equal(unity.resumed, 1);
  listeners.touchend[0]();
  listeners.pointerup[0]();
  assert.equal(unity.resumed, 3);
  unity.state = 'interrupted';                      // iOS after a phone call or app switch
  listeners.touchend[0]();
  assert.equal(unity.resumed, 4);
  unity.state = 'running';
  listeners.keydown[0]();
  listeners.pointerdown[0]();
  assert.equal(unity.resumed, 4);
});

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
async function setup() {
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
  const fetch = async url => { fetched.push(url); return { ok: true, arrayBuffer: async () => url }; };
  class OfflineAudioContext {
    decodeAudioData(url, resolve) { resolve(files[Object.keys(files).find(name => url.includes(`/media/${name}?`))]); }
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
  vm.runInNewContext(source, { window, fetch, URL, document: { baseURI: 'https://example.test/game/' } });
  await flush(); await flush();
  return { window, context, sources, files, fetched, listeners };
}

test('Unity\'s 8-second placeholder loop is replaced by the composed theme with its own loop points', async () => {
  const { context, files } = await setup();
  const music = context.createBufferSource();
  music.buffer = buffer({ length: 176400 });
  assert.equal(music.buffer, files['lobby-theme.mp3']);
  assert.equal(music.loopStart, 960 / 48000);
  assert.equal(music.loopEnd, 960 / 48000 + 64);
  music.loopStart = 0; music.loopEnd = 8;                         // Unity's values for its 8 s clip
  assert.equal(music.loopEnd, 960 / 48000 + 64);
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

test('music passes through a gain that dips while a clip with sound is on screen', async () => {
  const { window, context } = await setup();
  const music = context.createBufferSource();
  music.buffer = buffer({ length: 176400 });
  const unityGain = context.createGain();
  assert.equal(music.connect(unityGain), unityGain);
  const duck = music.links[0];
  assert.notEqual(duck, unityGain);
  assert.deepEqual(duck.links, [unityGain]);
  window.choSirenAudio.duck(true);
  assert.equal(duck.gain.value, 0.28);
  window.choSirenAudio.duck(false);
  assert.equal(duck.gain.value, 1);
  const click = context.createBufferSource();
  click.buffer = buffer({ length: 1654 });
  click.connect(unityGain);
  assert.deepEqual(click.links, [unityGain]);
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

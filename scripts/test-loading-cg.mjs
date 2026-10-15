import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source = fs.readFileSync(new URL('../loading-cg.js', import.meta.url), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));

function setup({ reduced = false, enabled = true, known = true, hasBgm = true, rejectFirst = null } = {}) {
  const events = {}, button = {}, attributes = {}, timers = [], fetched = [], choices = [], owners = [], videoEvents = {};
  let observer, hidden = false;
  const video = { muted: false, volume: 1, played: 0, paused: 0, loaded: 0, playing: false,
    src: 'media/loading-cg.mp4',
    getAttribute(name) { return name === 'data-has-bgm' ? String(hasBgm) : this.src; },
    removeAttribute(name) { if (name === 'src') this.src = null; },
    addEventListener(name, fn) { videoEvents[name] = fn; },
    play() { this.played++; this.playing = true;
      if (this.played === 1 && rejectFirst) return Promise.reject({ name: rejectFirst });
      return Promise.resolve(); },
    pause() { this.paused++; this.playing = false; }, load() { this.loaded++; }
  };
  const loading = { classList: { contains() { return hidden; } } };
  const sound = { setAttribute(k,v) { attributes[k]=v; }, addEventListener(k,fn) { button[k]=fn; } };
  const document = { hidden: false, addEventListener(k,fn) { events['doc:'+k]=fn; },
    querySelector(k) { return { '#loading':loading, '#loading-cg':video, '#sound-button':sound }[k]; } };
  const audio = { musicState: { enabled, settingKnown: known },
    setMusicEnabled(value) { choices.push(value); this.musicState.enabled=value; this.musicState.settingKnown=true; events['cho-siren-music-state'](); },
    setMediaMusicActive(owner, active) { owners.push({ owner, active, muted:video.muted, playing:video.playing }); } };
  const window = { choSirenAudio: audio,
    addEventListener(k,fn) { events[k]=fn; }, matchMedia() { return { matches: reduced }; },
    setTimeout(fn) { timers.push(fn); },
    localStorage: { getItem() { throw Error('no separate CG preference'); }, setItem() { throw Error('no separate CG preference'); } }
  };
  class MutationObserver { constructor(fn) { observer=fn; } observe() {} }
  vm.runInNewContext(source, { document, window, MutationObserver, fetch(url) { fetched.push(url); return Promise.resolve(); } });
  return { video, videoEvents, attributes, choices, owners, audio, events, button, document, timers, fetched,
    hide() { hidden=true; observer(); } };
}

test('CG claims music before playback and becomes audible only after a gesture', () => {
  const s=setup();
  assert.deepEqual(s.owners[0],{ owner:'loading-cg',active:true,muted:true,playing:false });
  assert.equal(s.video.volume,.45); assert.equal(s.video.muted,true);
  s.events.pointerdown();
  assert.equal(s.video.muted,false); assert.equal(s.video.playing,true);
  assert.deepEqual(s.choices,[], 'gestures never enable a saved disabled preference');
});

test('title speaker shares the game preference and never controls SFX', () => {
  const s=setup({ enabled:false });
  s.events.touchend(); assert.equal(s.video.muted,true);
  s.button.click({ stopPropagation() {} });
  assert.deepEqual(s.choices,[true]); assert.equal(s.video.muted,false);
  assert.equal(s.attributes['aria-pressed'],'true'); assert.equal(s.attributes['aria-label'],'关闭音乐');
  s.button.click({ stopPropagation() {} });
  assert.deepEqual(s.choices,[true,false]); assert.equal(s.video.muted,true);
  assert.equal(s.owners.at(-1).active,true, 'muting CG must not expose global BGM');
});

test('a saved mute setting is resolved before an early tap can unmute the CG', () => {
  const s=setup({ known:false });
  s.events.pointerup(); assert.equal(s.video.muted,true);
  s.audio.musicState={ enabled:false, settingKnown:true }; s.events['cho-siren-music-state']();
  assert.equal(s.video.muted,true);
  s.audio.musicState.enabled=true; s.events['cho-siren-music-state'](); assert.equal(s.video.muted,false);
});

test('background pauses and mutes CG while retaining its music lease', () => {
  const s=setup(); s.events.pointerdown();
  s.document.hidden=true; s.events['doc:visibilitychange']();
  assert.equal(s.video.playing,false); assert.equal(s.video.muted,true); assert.equal(s.owners.at(-1).active,true);
  s.document.hidden=false; s.events['doc:visibilitychange']();
  assert.equal(s.video.playing,true); assert.equal(s.video.muted,false); assert.deepEqual(s.choices,[]);
});

test('dismissal stops audible CG before releasing global music and unloads once', () => {
  const s=setup(); s.events.pointerdown(); s.hide();
  assert.deepEqual(s.owners.at(-1),{ owner:'loading-cg', active:false, muted:true, playing:false });
  assert.equal(s.video.src,'media/loading-cg.mp4');
  s.timers[0](); assert.equal(s.video.src,null); assert.equal(s.video.loaded,1);
  assert.deepEqual(s.fetched,['media/loading-cg.mp4']);
  const played=s.video.played;
  s.hide(); s.events.pointerdown(); s.events['doc:visibilitychange'](); s.events.pageshow();
  assert.equal(s.video.played,played); assert.equal(s.timers.length,1);
});

test('blocked autoplay waits for a later tap without releasing the media owner', async () => {
  const s=setup({ rejectFirst:'NotAllowedError' }); await flush();
  assert.equal(s.video.muted,true); assert.equal(s.video.played,2);
  assert.equal(s.owners.at(-1).active,true);
  s.events.touchend(); assert.equal(s.video.muted,false);
});

test('decode failure releases music only after stopping the video and cannot restart', async () => {
  const s=setup({ rejectFirst:'NotSupportedError' }); await flush();
  assert.deepEqual(s.owners.at(-1),{ owner:'loading-cg', active:false, muted:true, playing:false });
  const played=s.video.played; s.events.pointerup(); s.events.pageshow(); assert.equal(s.video.played,played);
  const t=setup(); t.events.pointerdown(); t.videoEvents.error();
  assert.deepEqual(t.owners.at(-1),{ owner:'loading-cg', active:false, muted:true, playing:false });
});

test('reduced motion or a silent movie leaves global BGM available', () => {
  const s=setup({ reduced:true });
  assert.equal(s.video.played,0); assert.equal(s.video.paused,1); assert.equal(s.video.muted,true);
  assert.equal(s.owners.at(-1).active,false);
  const t=setup({ hasBgm:false }); t.events.pointerdown();
  assert.equal(t.video.volume,0); assert.equal(t.video.muted,true); assert.equal(t.owners.at(-1).active,false);
});

test('back-forward restoration retains ownership and resumes CG instead of global music', () => {
  const s=setup(); s.events.pointerdown(); s.events.pagehide({ persisted:true });
  assert.equal(s.video.muted,true); assert.equal(s.video.playing,false); assert.equal(s.owners.at(-1).active,true);
  assert.equal(s.timers.length,0); s.events.pageshow();
  assert.equal(s.video.muted,false); assert.equal(s.video.playing,true);
  s.events.pagehide({ persisted:false }); assert.equal(s.owners.at(-1).active,false);
});

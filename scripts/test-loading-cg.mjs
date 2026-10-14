import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source = fs.readFileSync(new URL('../loading-cg.js', import.meta.url), 'utf8');
function setup({ reduced = false, enabled = false } = {}) {
  const events = {}, button = {}, attributes = {}, timers = [], fetched = [], choices = [];
  let observer, hidden = false;
  const video = { muted: false, volume: 1, played: 0, paused: 0, loaded: 0,
    src: 'media/loading-cg.mp4',
    getAttribute() { return this.src; },
    removeAttribute(name) { if (name === 'src') this.src = null; },
    play() { this.played++; return Promise.resolve(); },
    pause() { this.paused++; }, load() { this.loaded++; }
  };
  const loading = { classList: { contains() { return hidden; } } };
  const sound = { setAttribute(k,v) { attributes[k]=v; }, addEventListener(k,fn) { button[k]=fn; } };
  const document = { hidden: false, addEventListener(k,fn) { events['doc:'+k]=fn; },
    querySelector(k) { return { '#loading':loading, '#loading-cg':video, '#sound-button':sound }[k]; } };
  const window = { choSirenAudio: { musicState: { enabled }, setMusicEnabled(value) { choices.push(value); } },
    addEventListener(k,fn) { events[k]=fn; }, matchMedia() { return { matches: reduced }; },
    setTimeout(fn) { timers.push(fn); },
    localStorage: { getItem() { throw Error('legacy sound preference must not be read'); }, setItem() { throw Error('no separate CG preference'); } }
  };
  class MutationObserver { constructor(fn) { observer=fn; } observe() {} }
  vm.runInNewContext(source, { document, window, MutationObserver, fetch(url) { fetched.push(url); return Promise.resolve(); } });
  return { video, attributes, choices, events, button, document, timers, fetched, hide() { hidden=true; observer(); } };
}

test('CG stays silent and its speaker controls the saved game music only', () => {
  const s=setup();
  assert.equal(s.video.muted,true); assert.equal(s.video.volume,0);
  assert.equal(s.attributes['aria-pressed'],'false');
  s.button.click({ stopPropagation() {} });
  assert.deepEqual(s.choices,[true]);
  assert.equal(s.video.muted,true); assert.equal(s.video.volume,0);
  s.events['cho-siren-music-state']({ detail:{ enabled:true } });
  assert.equal(s.attributes['aria-pressed'],'true');
  assert.equal(s.attributes['aria-label'],'关闭音乐');
});

test('background and dismissal release the CG decoder without affecting music', () => {
  const s=setup({ enabled:true });
  assert.equal(s.video.played,1);
  s.document.hidden=true; s.events['doc:visibilitychange']();
  assert.equal(s.video.paused,1);
  s.document.hidden=false; s.events['doc:visibilitychange']();
  assert.equal(s.video.played,2);
  s.hide(); s.timers[0]();
  assert.equal(s.video.muted,true); assert.equal(s.video.loaded,1); assert.equal(s.video.src,null);
  assert.deepEqual(s.fetched,['media/loading-cg.mp4']);
  s.events['doc:visibilitychange']();
  assert.equal(s.video.played,2);
  assert.deepEqual(s.choices,[]);
});

test('reduced motion keeps the silent poster and never starts the video', () => {
  const s=setup({ reduced:true });
  assert.equal(s.video.played,0); assert.equal(s.video.paused,1);
  assert.equal(s.video.muted,true); assert.equal(s.video.volume,0);
});

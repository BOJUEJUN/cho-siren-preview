import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const code = readFileSync(new URL('../service-worker.js', import.meta.url), 'utf8');
function worker() {
  const listeners = {}, stored = new Map();
  let fetches = 0;
  const cache = {
    match: async key => stored.get(String(key))?.clone(),
    put: async (key, response) => { stored.set(String(key), response.clone()); },
    keys: async () => [...stored.keys()].map(url => ({url})),
    delete: async key => stored.delete(String(key))
  };
  const context = vm.createContext({URL, Headers, Response, Set, console,
    self: {location: {href:'https://example.test/game/service-worker.js'},
      addEventListener(type, fn) {listeners[type] = fn;}},
    caches: {has: async()=>false, open: async()=>cache},
    fetch: async () => {
      fetches++;
      const response = new Response(new Uint8Array([1,2,3,4]), {status:200});
      Object.defineProperty(response,'type',{value:'basic'});
      return response;
    }
  });
  vm.runInContext(code,context);
  return {key: url => vm.runInContext(`assetKey(new URL(${JSON.stringify(url)}))`,context),
    async get(url, range=false) {
      const waits=[];let pending;
      listeners.fetch({request:{method:'GET',url,headers:new Headers(range?{range:'bytes=0-1'}:{})},
        respondWith(value){pending=value;},waitUntil(value){waits.push(value);}});
      if(!pending) return null;
      const response=await pending; await Promise.all(waits); return response;
    }, fetches:()=>fetches};
}
const home='https://example.test/game/StreamingAssets/Music/home-1c7f72fed9af8b53.mp3';
const battle='https://example.test/game/StreamingAssets/Music/battle-11700e9a4a918ffe.mp3';

test('only immutable same-origin home and battle music are cached',()=>{
  const w=worker();
  assert.equal(w.key(home),home);assert.equal(w.key(battle),battle);
  for(const url of [home.replace('example.test','another.test'),home.replace('/game/','/other/'),
    home.replace('home-1c7f72fed9af8b53','home'),home.replace('home-','voice-'),home.replace('.mp3','.wav'),
    home+'?token=private']) assert.equal(w.key(url),null,url);
});

test('retry music refreshes its immutable key without caching other queries',()=>{
  const w=worker();assert.equal(w.key(home+'?retry=one'),home);
  assert.equal(w.key(home+'?v=one'),null);
});

test('returning to a music context reuses downloaded audio bytes',async()=>{
  const w=worker();
  assert.deepEqual([...new Uint8Array(await (await w.get(home)).arrayBuffer())],[1,2,3,4]);
  assert.deepEqual([...new Uint8Array(await (await w.get(home)).arrayBuffer())],[1,2,3,4]);
  assert.equal(w.fetches(),1);
  await w.get(home+'?retry=one');assert.equal(w.fetches(),2);
});

test('range music requests pass through without storing an incomplete audio body',async()=>{
  const w=worker();assert.equal(await w.get(home,true),null);assert.equal(w.fetches(),0);
});

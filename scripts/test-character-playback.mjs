import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source=fs.readFileSync(new URL('../character-moments.js',import.meta.url),'utf8');
function setup(plays=[]){
  const videos=[],timers=new Map();let id=0;
  const root={dataset:{},append(v){videos.push(v)}};
  function video(){const events={};return {readyState:3,currentTime:0,events,play(){return plays.shift() || Promise.resolve()},pause(){this.paused=true},load(){},removeAttribute(){},remove(){this.removed=true},addEventListener(k,f){events[k]=f}}}
  const window={choSirenStage:{active:true}};
  vm.runInNewContext(source,{window,document:{querySelector:()=>root,createElement:()=>video(),addEventListener(){},baseURI:'https://example.test/'},URL,setTimeout:f=>{timers.set(++id,f);return id},clearTimeout:i=>timers.delete(i)});
  return {api:window.choSirenCharacter,root,videos,timers};
}
test('slow next clip keeps currently displayed character until ready',()=>{const {api,videos}=setup();api.play(0,true);videos[0].events.playing();api.play(1,true);assert.equal(api.currentClip,0);assert.ok(!videos[0].removed);videos[1].events.playing();assert.equal(api.currentClip,1);assert.equal(videos[0].removed,true)});
test('failed or timed-out next clip retains current character',()=>{const {api,videos,timers}=setup();api.play(0,true);videos[0].events.playing();api.play(1,true);videos[1].events.error();assert.equal(api.currentClip,0);api.play(2,true);[...timers.values()][0]();assert.equal(api.currentClip,0)});
test('superseded playing/error callbacks cannot replace or close latest clip',()=>{const {api,videos}=setup();api.play(0,true);api.play(1,true);videos[1].events.playing();videos[0].events.playing();videos[0].events.error();assert.equal(api.currentClip,1)});
test('exit invalidates delayed callbacks and releases both video elements',()=>{const {api,videos}=setup();api.play(0,true);videos[0].events.playing();api.play(1,true);api.close();videos[1].events.playing();assert.equal(api.active,false);assert.equal(api.currentClip,-1);assert.ok(videos.every(v=>v.removed))});
test('natural end returns to portrait',async()=>{const {api,videos}=setup();api.play(0,true);videos[0].events.playing();videos[0].events.ended();assert.equal(api.currentClip,-1);assert.equal(api.active,false)});
test('frame upload preserves Unity GL state and skips duplicate video frame',()=>{
 const {api,videos}=setup();api.play(0,true);videos[0].events.playing();const state={binding:'ui',flip:false,premult:true};let uploads=0;
 const gl={TEXTURE_BINDING_2D:'binding',UNPACK_FLIP_Y_WEBGL:'flip',UNPACK_PREMULTIPLY_ALPHA_WEBGL:'premult',TEXTURE_2D:1,RGBA:2,UNSIGNED_BYTE:3,getParameter:k=>state[k],bindTexture:(k,v)=>state.binding=v,pixelStorei:(k,v)=>state[k]=v,texSubImage2D(){uploads++}};
 const texture={};assert.ok(api.uploadFrame(gl,texture));assert.ok(api.uploadFrame(gl,texture));assert.equal(uploads,1);assert.deepEqual(state,{binding:'ui',flip:false,premult:true});videos[0].currentTime=.1;api.uploadFrame(gl,texture);assert.equal(uploads,2);
});

 test('old rejected audio retry cannot close the newer playing clip',async()=>{
  let rejectFirst,rejectRetry;
  const first=new Promise((_,reject)=>rejectFirst=reject),retry=new Promise((_,reject)=>rejectRetry=reject);
  const {api,videos}=setup([first,retry]);
  api.play(0,true);rejectFirst(new Error('autoplay blocked'));await Promise.resolve();
  api.play(1,true);videos[1].events.playing();rejectRetry(new Error('old aborted retry'));await Promise.resolve();await Promise.resolve();
  assert.equal(api.currentClip,1);assert.equal(api.active,true);
 });

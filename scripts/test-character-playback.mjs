import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source=fs.readFileSync(new URL('../character-moments.js',import.meta.url),'utf8');
function setup(plays=[],globals={},{webm=false,idle=''}={}){
  const videos=[],made=[],timers=new Map(),taps={};let id=0;
  const root={dataset:idle?{idleClip:idle}:{},append(v){videos.push(v)}};
  function video(){const events={};const v={readyState:3,currentTime:0,events,plays:0,play(){this.plays++;this.paused=false;return plays.shift() || Promise.resolve()},pause(){this.paused=true},load(){},removeAttribute(){},remove(){this.removed=true},addEventListener(k,f){events[k]=f},canPlayType:t=>webm&&t.startsWith('video/webm')?'probably':''};made.push(v);return v}
  const window={choSirenStage:{active:true}};
  const document={hidden:false,querySelector:()=>root,createElement:()=>video(),addEventListener(type,f){(taps[type]||=[]).push(f)},baseURI:'https://example.test/'};
  vm.runInNewContext(source,{window,document,URL,setTimeout:f=>{timers.set(++id,f);return id},clearTimeout:i=>timers.delete(i),...globals});
  return {api:window.choSirenCharacter,root,videos,made,timers,window,taps,document};
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

const iPhone='Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/129.0 Mobile/15E148 Safari/604.1';
test('Safari and iOS load a packed copy (WebM where it plays, else HEVC), other browsers keep VP9 alpha',()=>{
 const ios=setup([],{navigator:{userAgent:iPhone}},{webm:true});ios.api.play(0,true);assert.match(ios.videos[0].src,/media\/catalena-look\.packed\.webm\?v=/);
 const oldIos=setup([],{navigator:{userAgent:iPhone}});oldIos.api.play(0,true);assert.match(oldIos.videos[0].src,/media\/catalena-look\.packed\.mp4\?v=/);
 const safari=setup([],{navigator:{userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15'}},{webm:true});
 safari.api.play(2,true);assert.match(safari.videos[0].src,/catalena-live\.packed\.webm/);
 const chrome=setup([],{navigator:{userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36'}},{webm:true});
 chrome.api.play(1,true);assert.match(chrome.videos[0].src,/catalena-whisper\.webm\?v=/);
 const forced=setup([],{location:{href:'https://example.test/?alphaVideo=packed'}});forced.api.play(0,true);assert.match(forced.videos[0].src,/\.packed\.webm/);
 const forcedMp4=setup([],{location:{href:'https://example.test/?alphaVideo=packed-mp4'}},{webm:true});forcedMp4.api.play(0,true);assert.match(forcedMp4.videos[0].src,/\.packed\.mp4/);
});

test('a packed copy that fails to load hands over to the next one; the last failure leaves the portrait',()=>{
 const {api,videos,root}=setup([],{navigator:{userAgent:iPhone}},{webm:true});
 api.play(0,true);videos[0].events.error();
 assert.equal(videos[0].removed,true);assert.match(videos[1].src,/catalena-look\.packed\.mp4/);assert.equal(root.dataset.state,'loading');
 Object.assign(videos[1],{videoWidth:1472,videoHeight:1280});videos[1].events.playing();
 assert.equal(api.currentClip,0);assert.equal(root.dataset.state,'playing');
 api.play(1,true);videos[2].events.error();videos[3].events.error();
 assert.equal(videos.length,4);assert.equal(api.currentClip,0);assert.equal(root.dataset.state,'playing');
});

test('play() refused as unsupported also moves on to the next copy',async()=>{
 const unsupported=Object.assign(new Error('format'),{name:'NotSupportedError'});
 const {api,videos}=setup([Promise.reject(unsupported)],{navigator:{userAgent:iPhone}},{webm:true});
 api.play(2,true);await Promise.resolve();await Promise.resolve();
 assert.equal(videos.length,2);assert.match(videos[1].src,/catalena-live\.packed\.mp4/);
 videos[1].events.playing();assert.equal(api.currentClip,2);
});

// Minimal WebGL2 double: records what the compositor does and how Unity's state looks.
const CAPS=['BLEND','CULL_FACE','DEPTH_TEST','POLYGON_OFFSET_FILL','RASTERIZER_DISCARD','SAMPLE_ALPHA_TO_COVERAGE','SAMPLE_COVERAGE','SCISSOR_TEST','STENCIL_TEST'];
function webgl2({encoding='LINEAR',complete=true}={}){
 const names=['ACTIVE_TEXTURE','DRAW_FRAMEBUFFER_BINDING','VIEWPORT','CURRENT_PROGRAM','VERTEX_ARRAY_BINDING','PIXEL_UNPACK_BUFFER_BINDING','UNPACK_FLIP_Y_WEBGL','UNPACK_PREMULTIPLY_ALPHA_WEBGL','COLOR_WRITEMASK','TEXTURE_BINDING_2D','SAMPLER_BINDING','TEXTURE0','TEXTURE3','TEXTURE_2D','DRAW_FRAMEBUFFER','PIXEL_UNPACK_BUFFER','COLOR_ATTACHMENT0','FRAMEBUFFER_COMPLETE','FRAMEBUFFER_ATTACHMENT_COLOR_ENCODING','SRGB','LINEAR','RGBA','RGBA8','UNSIGNED_BYTE','TRIANGLES','VERTEX_SHADER','FRAGMENT_SHADER','COMPILE_STATUS','LINK_STATUS','TEXTURE_MIN_FILTER','TEXTURE_MAG_FILTER','TEXTURE_WRAP_S','TEXTURE_WRAP_T','NEAREST','CLAMP_TO_EDGE',...CAPS];
 const gl=Object.fromEntries(names.map(n=>[n,n]));
 const s={ACTIVE_TEXTURE:'TEXTURE3',DRAW_FRAMEBUFFER_BINDING:'unity-fbo',VIEWPORT:[0,0,720,1536],CURRENT_PROGRAM:'unity-program',VERTEX_ARRAY_BINDING:'unity-vao',PIXEL_UNPACK_BUFFER_BINDING:'unity-pbo',UNPACK_FLIP_Y_WEBGL:false,UNPACK_PREMULTIPLY_ALPHA_WEBGL:true,COLOR_WRITEMASK:[true,true,true,false],textures:{TEXTURE0:'unity-tex0',TEXTURE3:'unity-tex3'},samplers:{TEXTURE0:'unity-sampler0'},enabled:{BLEND:true,SCISSOR_TEST:true}};
 const log={draws:[],attach:[],uniforms:{},uploads:0,during:null};
 Object.assign(gl,{
  getParameter:k=>k==='TEXTURE_BINDING_2D'?s.textures[s.ACTIVE_TEXTURE]??null:k==='SAMPLER_BINDING'?s.samplers[s.ACTIVE_TEXTURE]??null:s[k],
  isEnabled:k=>!!s.enabled[k],enable:k=>{s.enabled[k]=true},disable:k=>{s.enabled[k]=false},
  activeTexture:u=>{s.ACTIVE_TEXTURE=u},bindTexture:(t,x)=>{s.textures[s.ACTIVE_TEXTURE]=x},bindSampler:(u,x)=>{s.samplers['TEXTURE'+u]=x},
  bindBuffer:(t,b)=>{s.PIXEL_UNPACK_BUFFER_BINDING=b},pixelStorei:(k,v)=>{s[k]=v},viewport:(...v)=>{s.VIEWPORT=v},colorMask:(...v)=>{s.COLOR_WRITEMASK=v},
  useProgram:p=>{s.CURRENT_PROGRAM=p},bindVertexArray:v=>{s.VERTEX_ARRAY_BINDING=v},bindFramebuffer:(t,f)=>{s.DRAW_FRAMEBUFFER_BINDING=f},
  framebufferTexture2D:(t,a,tt,x)=>{log.attach.push(x)},checkFramebufferStatus:()=>complete?'FRAMEBUFFER_COMPLETE':'INCOMPLETE',getFramebufferAttachmentParameter:()=>encoding,
  createShader:t=>({t}),shaderSource(){},compileShader(){},getShaderParameter:()=>true,createProgram:()=>'packed-program',attachShader(){},linkProgram(){},getProgramParameter:()=>true,
  getUniformLocation:(p,n)=>n,uniform1i:(n,v)=>{log.uniforms[n]=v},uniform3i:(n,...v)=>{log.uniforms[n]=v},
  createTexture:()=>'packed-frame',createFramebuffer:()=>'packed-fbo',createVertexArray:()=>'packed-vao',texParameteri(){},texImage2D(){log.uploads++},texSubImage2D(){log.uploads++},
  drawArrays:(m,f,c)=>{log.draws.push(c);log.during={viewport:s.VIEWPORT,program:s.CURRENT_PROGRAM,fbo:s.DRAW_FRAMEBUFFER_BINDING,caps:CAPS.filter(k=>s.enabled[k]),texture0:s.textures.TEXTURE0,sampler0:s.samplers.TEXTURE0,pbo:s.PIXEL_UNPACK_BUFFER_BINDING,flip:s.UNPACK_FLIP_Y_WEBGL}}
 });
 const snapshot=()=>JSON.stringify({...s,enabled:CAPS.map(k=>!!s.enabled[k])});
 return {gl,log,snapshot};
}
function packedPlayer(){const run=setup([],{navigator:{userAgent:iPhone}});run.api.play(0,true);Object.assign(run.videos[0],{videoWidth:1472,videoHeight:1280});run.videos[0].events.playing();return run}

test('packed frame is recombined into the Unity texture and every GL state is restored',()=>{
 const {api,videos}=packedPlayer(),{gl,log,snapshot}=webgl2(),before=snapshot();
 assert.ok(api.uploadFrame(gl,'unity-character'));
 assert.equal(snapshot(),before);
 assert.deepEqual(log.draws,[3]);
 assert.deepEqual(log.attach,['unity-character',null]);
 assert.deepEqual(log.uniforms,{uFrame:0,uPacking:[720,1280,752],uKey:0,uLinear:0});
 assert.deepEqual(log.during,{viewport:[0,0,720,1280],program:'packed-program',fbo:'packed-fbo',caps:[],texture0:'packed-frame',sampler0:null,pbo:null,flip:false});
 assert.ok(api.uploadFrame(gl,'unity-character'));assert.equal(log.uploads,1);
 videos[0].currentTime=.1;api.uploadFrame(gl,'unity-character');assert.equal(log.uploads,2);assert.equal(snapshot(),before);
});

test('sRGB Unity textures receive linear values so the stored bytes match a plain upload',()=>{
 const {api}=packedPlayer(),{gl,log}=webgl2({encoding:'SRGB'});
 assert.ok(api.uploadFrame(gl,'unity-character'));assert.equal(log.uniforms.uLinear,1);
});

test('a target Unity cannot render into closes the clip and leaves GL state untouched',()=>{
 const {api}=packedPlayer(),{gl,log,snapshot}=webgl2({complete:false}),before=snapshot();
 assert.equal(api.uploadFrame(gl,'unity-character'),false);
 assert.equal(snapshot(),before);assert.deepEqual(log.draws,[]);assert.equal(api.active,false);
});

test('a clip refused sound plays its voice through Web Audio and ducks the music until it ends',async()=>{
 const calls=[],ducks=[];let stops=0;
 const audio={playVoice:(name,time)=>{calls.push([name,time()]);return {stop(){stops++}}},duck:on=>ducks.push(on)};
 const {api,videos,window}=setup([Promise.reject(new Error('NotAllowedError'))]);window.choSirenAudio=audio;
 api.play(0,true);await Promise.resolve();await Promise.resolve();
 assert.equal(videos[0].muted,true);
 videos[0].currentTime=0.2;videos[0].events.playing();
 assert.deepEqual(calls,[['catalena-look',0.2]]);
 assert.equal(ducks.at(-1),true);
 videos[0].events.waiting();assert.equal(stops,1);
 videos[0].events.playing();assert.equal(calls.length,2);
 videos[0].events.ended();
 assert.equal(stops,2);assert.equal(ducks.at(-1),false);
});

test('a clip that plays with its own sound needs no voice track but still ducks the music',async()=>{
 const calls=[],ducks=[];
 const audio={playVoice:name=>{calls.push(name);return {stop(){}}},duck:on=>ducks.push(on)};
 const {api,videos,window}=setup();window.choSirenAudio=audio;
 api.play(1,true);videos[0].events.playing();
 assert.deepEqual(calls,[]);assert.equal(ducks.at(-1),true);
 api.close();assert.equal(ducks.at(-1),false);
});

test('a tap unlocks spare players, and the next clip plays on one of them',()=>{
 const {api,videos,made,taps}=setup();
 assert.ok(['touchend','pointerup','click','keydown'].every(type=>taps[type]?.length>=1));
 taps.touchend[0]();
 assert.equal(made.length,2);
 assert.ok(made.every(v=>v.plays===1&&v.paused&&v.muted&&v.playsInline));
 api.play(0,true);
 assert.equal(videos[0],made[0]);assert.equal(videos[0].plays,2);assert.equal(videos[0].muted,false);
 taps.pointerup[0]();
 assert.equal(made.length,3);assert.equal(made[1].plays,1);assert.equal(made[2].plays,1);
 api.play(1,true);api.play(2,true);
 assert.deepEqual(videos,[made[0],made[1],made[2]]);
});

test('leaving the page ends the clip instead of leaving it frozen with the music ducked',()=>{
 const ducks=[];const {api,videos,taps,document,window}=setup();window.choSirenAudio={duck:on=>ducks.push(on),playVoice:()=>({stop(){}})};
 api.play(0,true);videos[0].events.playing();assert.equal(ducks.at(-1),true);
 taps.visibilitychange[0]();assert.equal(api.currentClip,0);
 document.hidden=true;taps.visibilitychange[0]();
 assert.equal(api.active,false);assert.equal(api.currentClip,-1);assert.equal(videos[0].removed,true);assert.equal(ducks.at(-1),false);
});

test('clips download in the background once the lobby is up, one after another, unless data saving is on',async()=>{
 const fetched=[];
 const fetch=async url=>{fetched.push(url);let reads=0;return {status:200,body:{getReader:()=>({read:async()=>({done:reads++>0})})}}};
 const {timers}=setup([],{fetch});
 assert.equal(timers.size,1);
 await [...timers.values()][0]();
 assert.deepEqual(fetched.map(url=>new URL(url).pathname),['/media/catalena-look.webm','/media/catalena-whisper.webm','/media/catalena-live.webm']);
 const saving=setup([],{fetch,navigator:{userAgent:'',connection:{saveData:true}}});
 assert.equal(saving.timers.size,0);
});

// --- idle loop ---------------------------------------------------------------------
function idleRun(){const run=setup([],{},{idle:'catalena-idle'});run.api.setIdle(true);run.videos[0].events.playing();return run}
test('the idle loop starts once Unity allows it and the lobby is up, and stops when Unity says so',()=>{
 const {api,videos}=setup([],{},{idle:'catalena-idle'});
 assert.equal(videos.length,0);
 api.setIdle(true);assert.match(videos[0].src,/media\/catalena-idle\.webm/);assert.equal(videos[0].muted,true);
 videos[0].events.playing();assert.equal(api.currentClip,3);
 api.setIdle(false);assert.equal(api.currentClip,-1);assert.equal(videos[0].removed,true);
});
test('a tap during the loop waits for its first frame; the loop\'s last frame stays until the performance plays',()=>{
 const {api,videos}=idleRun();
 assert.equal(api.play(1,true),true);assert.equal(videos.length,1);assert.equal(api.currentClip,3);
 videos[0].events.ended();
 assert.match(videos[1].src,/catalena-whisper/);assert.equal(api.currentClip,3);assert.ok(!videos[0].removed);
 videos[1].events.playing();assert.equal(api.currentClip,1);assert.equal(videos[0].removed,true);
});
test('a performance hands back to the loop, holding its last frame until the loop plays',()=>{
 const {api,videos}=idleRun();
 api.play(0,false);videos[0].events.ended();videos[1].events.playing();
 videos[1].events.ended();
 assert.match(videos[2].src,/catalena-idle/);assert.equal(api.currentClip,0);assert.ok(!videos[1].removed);
 videos[2].events.playing();assert.equal(api.currentClip,3);assert.equal(videos[1].removed,true);
});
test('with no tap the loop restarts on the same player',()=>{
 const {api,videos}=idleRun();const plays=videos[0].plays;
 videos[0].currentTime=2.5;videos[0].events.ended();
 assert.equal(videos.length,1);assert.equal(videos[0].currentTime,0);assert.equal(videos[0].plays,plays+1);assert.equal(api.currentClip,3);
});
test('a loop that cannot load is given up and performances return to the portrait',()=>{
 const {api,videos}=setup([],{},{idle:'catalena-idle'});
 api.setIdle(true);videos[0].events.error();assert.equal(api.currentClip,-1);
 api.play(2,false);videos[1].events.playing();videos[1].events.ended();
 assert.equal(api.currentClip,-1);assert.equal(videos.length,2);
});
test('clip size reports the colour half of a packed copy',()=>{
 const {api,videos}=packedPlayer();assert.deepEqual({...api.clipSize},{width:720,height:1280,keyed:false});
 const plain=setup();plain.api.play(0,false);Object.assign(plain.videos[0],{videoWidth:1080,videoHeight:1920});plain.videos[0].events.playing();
 assert.deepEqual({...plain.api.clipSize},{width:1080,height:1920,keyed:false});
});

test('green-screen copies are chosen for every browser and keyed on the GPU at full width',()=>{
 // The format is read when the player starts, so this player is built with it set.
 const videos=[];const root={dataset:{clipFormat:'green',clipVersion:'r18a'},append(v){videos.push(v)}};
 const window={choSirenStage:{active:true}};
 const video=()=>{const events={};return {readyState:3,currentTime:0,events,play(){return Promise.resolve()},pause(){},load(){},removeAttribute(){},remove(){},addEventListener(k,f){events[k]=f},canPlayType:()=>'probably'}};
 const document={hidden:false,querySelector:()=>root,createElement:video,addEventListener(){},baseURI:'https://example.test/'};
 vm.runInNewContext(source,{window,document,URL,setTimeout:()=>0,clearTimeout(){},navigator:{userAgent:iPhone}});
 const api=window.choSirenCharacter;api.play(1,false);
 assert.match(videos[0].src,/media\/catalena-whisper\.green\.mp4\?v=r18a/);
 Object.assign(videos[0],{videoWidth:1080,videoHeight:1920});videos[0].events.playing();
 assert.deepEqual({...api.clipSize},{width:1080,height:1920,keyed:true});
 const {gl,log,snapshot}=webgl2(),before=snapshot();
 assert.ok(api.uploadFrame(gl,'unity-character'));
 assert.equal(snapshot(),before);
 assert.deepEqual(log.uniforms,{uFrame:0,uPacking:[1080,1920,0],uKey:1,uLinear:0});
 assert.deepEqual(log.during.viewport,[0,0,1080,1920]);
});

test('a tap inside one of the loop\'s cut stretches starts at once; outside, at the next one',()=>{
 // idleCuts is read at start-up, so this player is built with it set.
 const run=(()=>{const videos=[];const root={dataset:{idleClip:'catalena-idle',idleCuts:'0-1.2,2.8-9'},append(v){videos.push(v)}};
  const window={choSirenStage:{active:true}};
  const video=()=>{const events={};return {readyState:3,currentTime:0,events,plays:0,play(){this.plays++;return Promise.resolve()},pause(){},load(){},removeAttribute(){},remove(){this.removed=true},addEventListener(k,f){events[k]=f},canPlayType:()=>''}};
  const document={hidden:false,querySelector:()=>root,createElement:video,addEventListener(){},baseURI:'https://example.test/'};
  vm.runInNewContext(source,{window,document,URL,setTimeout:()=>0,clearTimeout(){}});
  return {api:window.choSirenCharacter,videos}})();
 run.api.setIdle(true);run.videos[0].events.playing();
 run.videos[0].currentTime=0.6;run.api.play(0,false);
 assert.equal(run.videos.length,2);assert.match(run.videos[1].src,/catalena-look/);
 run.videos[1].events.playing();run.videos[1].events.ended();run.videos[2].events.playing();
 run.videos[2].currentTime=2.0;run.api.play(2,false);assert.equal(run.videos.length,3);
 const gl={TEXTURE_BINDING_2D:0,UNPACK_FLIP_Y_WEBGL:0,UNPACK_PREMULTIPLY_ALPHA_WEBGL:0,TEXTURE_2D:0,RGBA:0,UNSIGNED_BYTE:0,getParameter:()=>null,bindTexture(){},pixelStorei(){},texSubImage2D(){}};
 run.api.uploadFrame(gl,{});assert.equal(run.videos.length,3);
 run.videos[2].currentTime=3.0;run.api.uploadFrame(gl,{});
 assert.equal(run.videos.length,4);assert.match(run.videos[3].src,/catalena-live/);
});

test('a green-screen performance speaks its line beside it; the line outlives the clip and the next one replaces it',()=>{
 const videos=[];const root={dataset:{clipFormat:'green',idleClip:'catalena-idle'},append(v){videos.push(v)}};
 const window={choSirenStage:{active:true}};
 const video=()=>{const events={};return {readyState:3,currentTime:0,events,play(){return Promise.resolve()},pause(){},load(){},removeAttribute(){},remove(){this.removed=true},addEventListener(k,f){events[k]=f},canPlayType:()=>''}};
 const document={hidden:false,querySelector:()=>root,createElement:video,addEventListener(){},baseURI:'https://example.test/'};
 vm.runInNewContext(source,{window,document,URL,setTimeout:()=>0,clearTimeout(){}});
 const calls=[],ducks=[];let stops=0,finish=null;
 window.choSirenAudio={playVoice:(name,time,onEnded)=>{calls.push([name,time()]);finish=onEnded;return {stop(){stops++}}},duck:on=>ducks.push(on)};
 const api=window.choSirenCharacter;api.setIdle(true);videos[0].events.playing();
 assert.deepEqual(calls,[]);assert.equal(ducks.at(-1),false);
 api.play(1,true);videos[0].events.ended();
 assert.equal(videos[1].muted,true);
 videos[1].events.playing();
 assert.deepEqual(calls,[['catalena-whisper',0]]);assert.equal(ducks.at(-1),true);
 videos[1].events.ended();videos[2].events.playing();
 assert.equal(api.currentClip,3);assert.equal(stops,0);assert.equal(ducks.at(-1),true);
 finish();assert.equal(ducks.at(-1),false);
 api.play(2,true);videos[2].events.ended();videos[3].events.playing();
 api.play(0,true);videos[3].events.ended();videos[4].events.playing();
 assert.equal(stops,1);assert.deepEqual(calls.map(c=>c[0]),['catalena-whisper','catalena-live','catalena-look']);
 api.play(1,false);videos[4].events.ended();videos[5].events.playing();
 assert.equal(calls.length,3);
});

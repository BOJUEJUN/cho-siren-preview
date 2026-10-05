import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs';
const source=fs.readFileSync(new URL('../character-moments.js',import.meta.url),'utf8');
function setup(plays=[],globals={}){
  const videos=[],timers=new Map();let id=0;
  const root={dataset:{},append(v){videos.push(v)}};
  function video(){const events={};return {readyState:3,currentTime:0,events,play(){return plays.shift() || Promise.resolve()},pause(){this.paused=true},load(){},removeAttribute(){},remove(){this.removed=true},addEventListener(k,f){events[k]=f}}}
  const window={choSirenStage:{active:true}};
  vm.runInNewContext(source,{window,document:{querySelector:()=>root,createElement:()=>video(),addEventListener(){},baseURI:'https://example.test/'},URL,setTimeout:f=>{timers.set(++id,f);return id},clearTimeout:i=>timers.delete(i),...globals});
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

const iPhone='Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/129.0 Mobile/15E148 Safari/604.1';
test('Safari and iOS load the packed HEVC copy, other browsers keep VP9 alpha',()=>{
 const ios=setup([],{navigator:{userAgent:iPhone}});ios.api.play(0,true);assert.match(ios.videos[0].src,/media\/catalena-look\.packed\.mp4\?v=/);
 const safari=setup([],{navigator:{userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15'}});
 safari.api.play(2,true);assert.match(safari.videos[0].src,/catalena-live\.packed\.mp4/);
 const chrome=setup([],{navigator:{userAgent:'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36'}});
 chrome.api.play(1,true);assert.match(chrome.videos[0].src,/catalena-whisper\.webm\?v=/);
 const forced=setup([],{location:{href:'https://example.test/?alphaVideo=packed'}});forced.api.play(0,true);assert.match(forced.videos[0].src,/\.packed\.mp4/);
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
 assert.deepEqual(log.uniforms,{uFrame:0,uPacking:[720,1280,752],uLinear:0});
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

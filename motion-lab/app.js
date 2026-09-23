(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const MODES = [
    {id:1,title:'节奏舞台',en:'RESONANCE',symbol:'✦',color:'#c5ff79',description:'把首页变成开场前的 Live House。追光与心跳同步，按钮踩着鼓点呼吸，点击就是一次全场共振。',moments:['移动鼠标，让舞台随视线轻轻转动','触碰入口，感受踩在拍点上的弹性','点击开演，追光聚拢、声浪炸开'],short:'追光 · 节拍 · 全场共振',show:'全场共振',kicker:'THE STAGE IS YOURS',line:'灯光就位。这一刻，你就是舞台中心。',impact:'ON AIR!'},
    {id:2,title:'棱镜全息',en:'PRISM PROTOCOL',symbol:'◇',color:'#87ffee',description:'像唤醒一台来自未来的偶像终端。扫描光穿过画面，界面悬在不同深度，指尖点亮细密的全息碎片。',moments:['移动视线，观察人物和界面的层间视差','悬停按钮，捕捉色散边缘与定位光环','点击开演，看棱镜展开成全息舞台'],short:'扫描 · 色散 · 全息展开',show:'链接未来',kicker:'SIREN LINK / ESTABLISHED',line:'棱镜已展开，下一场演出在未来发生。',impact:'LINKED'},
    {id:3,title:'漫画暴走',en:'POP RIOT',symbol:'✹',color:'#ffe566',description:'让她从漫画分镜里跳出来。大胆的顿帧、弹性纸片和冲击线，每一次点击，都像翻到最热血的一页。',moments:['看纸片按钮错拍登场、俏皮抖肩','按住再松开，体验夸张的压缩反弹','点击开演，让漫画爆字占满画面'],short:'顿帧 · 反弹 · 漫画爆字',show:'现在，炸场！',kicker:'TURN IT UP / BREAK THE FRAME',line:'准备好了吗？把今天翻成最闪耀的一页。',impact:'BOOM!'},
    {id:4,title:'深空失重',en:'ZERO GRAVITY',symbol:'◎',color:'#acbaff',description:'把舞台送入一片安静的宇宙。人物和入口各自漂浮，星尘追随指尖，开演时穿过一圈光速隧道。',moments:['停留片刻，观察每层不同的漂浮节奏','移动鼠标，用引力牵动附近的星尘','点击开演，穿过星门抵达下一站'],short:'漂浮 · 引力 · 星门跃迁',show:'跃迁，下一站',kicker:'DESTINATION / YOUR UNIVERSE',line:'所有星光，都正向你靠近。',impact:'WARP ✧'},
    {id:5,title:'绮梦花火',en:'DREAM BLOOM',symbol:'❋',color:'#ffc4e8',description:'一场会呼吸的梦。极光慢慢晕开，花瓣掠过发梢，指尖留下一条柔软的光带，让开演变成花火盛放。',moments:['移动指尖，留下渐渐消散的柔光轨迹','悬停入口，感受花瓣般舒展的反馈','点击开演，等待光与花瓣一起盛放'],short:'极光 · 光带 · 花火盛放',show:'为你，盛放',kicker:'LET YOUR DREAMS BLOOM',line:'把这一刻的心动，唱给整个世界听。',impact:'BLOOM'}
  ];
  const LABELS = {Tasks:'任务',AlbumProduction:'专辑制作',PracticeRoom:'练习室',LiveOnStage:'开始演出',Profile:'个人资料','Currency-diamond':'钻石','Currency-gold':'星光币','Currency-stamina':'体力',Mail:'邮件',Settings:'设置','Nav-team':'团队','Nav-members':'成员','Nav-lobby':'大厅','Nav-accessory':'饰品','Nav-audition':'选秀'};
  const layout = window.HOME_LAYOUT;
  const scene=$('scene'), world=$('world'), body=document.body;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  let mode=MODES[0], paused=reduced.matches, intensity=reduced.matches ? 0 : 1;
  let ready=false, performing=false, opening=false, hovering='', pointer={x:.5,y:.45,inside:false}, smoothed={x:.5,y:.45};
  let time=0,lastFrame=0,raf=0,entryTimer,showTimer,impactTimer,toastTimer,previousFocus=null;
  let soundOn=false,audioCtx=null, width=360,height=760,dpr=1,trail=[],bursts=[],hoverPulse=0;
  let favorites=[];
  try{favorites=JSON.parse(localStorage.getItem('cho-motion-lab-favorites')||'[]').filter(x=>Number.isInteger(x)&&x>=1&&x<=5);}catch{}
  const layerNodes=[], actionNodes=new Map();
  const canvases=[$('atmosphere'),$('sparks')], contexts=canvases.map(x=>x.getContext('2d'));
  let seed=731;
  function random(){seed=(seed*16807)%2147483647;return(seed-1)/2147483646;}
  const particles=Array.from({length:84},()=>({x:random(),y:random(),s:random(),a:random()*Math.PI*2,v:.3+random(),n:random()}));
  const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
  const fract=x=>x-Math.floor(x);
  const mix=(a,b,x)=>a+(b-a)*x;
  function position(el,r){el.style.left=r.x/layout.width*100+'%';el.style.top=r.y/layout.height*100+'%';el.style.width=r.w/layout.width*100+'%';el.style.height=r.h/layout.height*100+'%';}

  function buildHome(){
    layout.elements.forEach((e,i)=>{
      const node=document.createElement('div');
      const char=e.id==='layer-04', outline=e.id==='layer-03';
      node.className='layer'+(e.action?' action-layer':'')+(char?' character':'')+(outline?' outline':'')+(e.action==='LiveOnStage'?' cta':'');
      position(node,e);node.style.zIndex=e.z;node.style.setProperty('--order',e.action.startsWith('Nav-')?5+i*.15:i*.3);
      const inner=document.createElement('div');inner.className='motion';
      const img=new Image();img.src='assets/home-'+e.id+'.png';img.alt='';img.draggable=false;img.decoding='async';inner.append(img);node.append(inner);$('layers').append(node);
      layerNodes.push({node,inner,img,e,char,outline});
      if(e.action){if(!actionNodes.has(e.action))actionNodes.set(e.action,[]);actionNodes.get(e.action).push(node);}
    });
    const texts=[['音律少女',515,85,700,106,105,false],['LV. 1',515,198,530,69,78,false],['战力 1003',515,272,680,89,88,false],['390',1480,215,420,124,112,true],['1,200',2395,215,430,124,112,true],['120/120',3320,208,505,124,108,true]];
    texts.forEach(([text,x,y,w,h,size,center])=>{const el=document.createElement('div');el.className='hud-text'+(center?' center':'');el.textContent=text;position(el,{x,y,w,h});el.style.fontSize=size/layout.width*100+'cqw';$('layers').append(el);});
    layout.hits.forEach(hit=>{
      const el=document.createElement('button');el.className='hotspot';el.dataset.action=hit.id;el.setAttribute('aria-label',LABELS[hit.id]);el.title=LABELS[hit.id];position(el,hit);
      el.addEventListener('pointerenter',e=>{if(e.pointerType!=='touch')enterAction(hit.id);});
      el.addEventListener('pointerleave',()=>leaveAction(hit.id));
      el.addEventListener('focus',()=>enterAction(hit.id));
      el.addEventListener('blur',()=>leaveAction(hit.id));
      el.addEventListener('pointerdown',()=>{if(!ready)return;enterAction(hit.id);setActionClass(hit.id,'pressed',true);});
      el.addEventListener('pointerup',()=>setActionClass(hit.id,'pressed',false));
      el.addEventListener('pointercancel',()=>leaveAction(hit.id));
      el.addEventListener('click',event=>{if(!ready)return;activate(hit,event);if(event.pointerType==='touch')leaveAction(hit.id);});
      $('hotspots').append(el);
    });
    document.querySelectorAll('.beat-meter,.show-equalizer').forEach(el=>{for(let i=0;i<11;i++){const bar=document.createElement('i');bar.style.setProperty('--i',i);el.append(bar);}});
    document.querySelectorAll('.motion-signature span').forEach((el,i)=>el.style.setProperty('--n',i+1));
  }
  function setActionClass(action,cls,on){(actionNodes.get(action)||[]).forEach(n=>n.classList.toggle(cls,on));}
  function enterAction(action){if(!ready||performing||!$('game-sheet').hidden)return;if(hovering&&hovering!==action)leaveAction(hovering);hovering=action;hoverPulse=time;setActionClass(action,'active',true);$('interaction-status').textContent=LABELS[action]+' · 点击体验';}
  function leaveAction(action){setActionClass(action,'active',false);setActionClass(action,'pressed',false);if(hovering===action)hovering='';if(!performing&&$('game-sheet').hidden)$('interaction-status').textContent='点击画面中的按钮试试看';}
  function clearActions(){for(const id of actionNodes.keys()){setActionClass(id,'active',false);setActionClass(id,'pressed',false);}hovering='';}

  function buildSelector(){
    MODES.forEach(m=>{
      const btn=document.createElement('button');btn.className='mode-card';btn.dataset.mode=m.id;btn.style.setProperty('--card-color',m.color);btn.setAttribute('aria-label',m.id+' '+m.title);btn.innerHTML=`<span class="mode-number">0${m.id}</span><span><span class="mode-name">${m.title}</span><span class="mode-en">${m.en}</span></span><span class="mode-glyph" aria-hidden="true">${m.symbol}</span><span class="saved-mark" aria-hidden="true" hidden>♥</span>`;btn.addEventListener('click',()=>setMode(m.id));$('modes').append(btn);
      const card=document.createElement('button');card.className='overview-card';card.style.setProperty('--card-color',m.color);card.setAttribute('aria-label','体验'+m.title);card.innerHTML=`<div class="overview-art"><span class="overview-glyph">${m.symbol}</span></div><div class="overview-caption"><span>0${m.id} / ${m.en}</span><h3>${m.title}</h3><p>${m.short}</p></div>`;card.addEventListener('click',()=>{closeOverview();setMode(m.id);});$('overview-grid').append(card);
    });
  }
  function setMode(id,updateHash=true){
    mode=MODES.find(x=>x.id===Number(id))||MODES[0];
    stopShow(false);closeSheet(false);clearActions();bursts=[];trail=[];time=0;smoothed={x:.5,y:.45};
    body.dataset.mode=mode.id;scene.dataset.mode=mode.id;
    $('preview-code').textContent='0'+mode.id+' / '+(mode.id===2?'PRISM':mode.id===3?'POP RIOT':mode.id===4?'ZERO G':mode.id===5?'DREAM':'RESONANCE');
    $('direction-number').textContent='0'+mode.id;$('direction-title').textContent=mode.title;$('direction-english').textContent=mode.en;$('direction-symbol').textContent=mode.symbol;
    $('direction-description').textContent=mode.description;$('moments').replaceChildren(...mode.moments.map(text=>{const li=document.createElement('li');li.textContent=text;return li;}));
    $('show-title').textContent=mode.show;$('show-kicker').textContent=mode.kicker;$('show-description').textContent=mode.line;
    document.querySelectorAll('.mode-card').forEach(b=>b.setAttribute('aria-current',String(Number(b.dataset.mode)===mode.id)));
    if(updateHash&&location.hash!=='#v'+mode.id){try{history.replaceState(null,'','#v'+mode.id);}catch{location.hash='v'+mode.id;}}
    updateFavorite();if(ready){intro();drawAtmosphere(contexts[0],0);drawSparks(contexts[1],0,0);}
  }
  function intro(){
    stopShow(false);closeSheet(false);clearActions();clearTimeout(entryTimer);scene.classList.remove('entering');void scene.offsetWidth;
    scene.classList.add('entering');entryTimer=setTimeout(()=>{scene.classList.remove('entering');if(!hovering&&!performing&&$('game-sheet').hidden)$('interaction-status').textContent='点击画面中的按钮试试看';},2100);
    if(!paused&&intensity>0)emit(.5,.45,mode.id===3?26:18,false);
    $('interaction-status').textContent=mode.title+' · 入场预览';
  }
  function updateFavorite(){const saved=favorites.includes(mode.id);$('favorite').setAttribute('aria-pressed',String(saved));$('favorite-icon').textContent=saved?'♥':'♡';$('favorite-text').textContent=saved?'已收藏这个方向':'收藏这个方向';document.querySelectorAll('.mode-card').forEach(el=>el.querySelector('.saved-mark').hidden=!favorites.includes(Number(el.dataset.mode)));$('favorite-summary').textContent=favorites.length?'已收藏 '+favorites.map(n=>'0'+n).join(' / '):'你的灵感，由你定调。';}
  function toast(text){$('toast').textContent=text;$('toast').classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('visible'),2300);}

  function updateMotion(){
    body.classList.toggle('paused',paused);body.classList.toggle('zero-motion',intensity===0);body.style.setProperty('--intensity',intensity);body.style.setProperty('--energy',Math.min(intensity,1.2));
    $('pause').setAttribute('aria-pressed',String(paused));$('pause-icon').textContent=paused?'▷':'Ⅱ';$('pause-text').textContent=paused?'继续动效':'暂停动效';
    $('intensity').value=String(Math.round(intensity*100));$('intensity-value').value=Math.round(intensity*100)+'%';$('intensity').style.setProperty('--range',intensity/1.5*100+'%');
    $('motion-state').textContent=paused?'Ⅱ PAUSED':intensity===0?'○ STILL':'● MOTION ON';
    if(paused||intensity===0){$('device').style.setProperty('--tilt-x','0deg');$('device').style.setProperty('--tilt-y','0deg');layerNodes.forEach(l=>l.node.style.removeProperty('translate'));}
  }
  function togglePause(){paused=!paused;updateMotion();toast(paused?'动效已暂停，按钮仍可操作':'动效继续播放');}
  async function toggleSound(){
    if(!soundOn){try{const AudioClass=window.AudioContext||window.webkitAudioContext;if(!AudioClass)throw Error('Unavailable');audioCtx=audioCtx||new AudioClass();await audioCtx.resume();soundOn=audioCtx.state==='running';if(!soundOn)throw Error('Suspended');}catch{toast('此浏览器暂时无法播放音效');return;}}
    else soundOn=false;
    updateSound();if(soundOn)tone('tap');
  }
  function updateSound(){$('sound').setAttribute('aria-pressed',String(soundOn));$('sound-text').textContent=soundOn?'音效开':'音效关';$('sound-icon').style.opacity=soundOn?'1':'.45';}
  function tone(type){if(!soundOn||!audioCtx||audioCtx.state!=='running')return;const notes=mode.id===1?[220,330,440,660]:mode.id===2?[392,587.33,783.99,1174.66]:mode.id===3?[261.63,329.63,392,523.25]:mode.id===4?[196,293.66,392,587.33]:[329.63,440,493.88,659.25];const count=type==='show'?8:1;for(let i=0;i<count;i++){const o=audioCtx.createOscillator(),g=audioCtx.createGain();o.type=mode.id===3?'triangle':'sine';const start=audioCtx.currentTime+i*.115;o.frequency.setValueAtTime(notes[i%notes.length],start);if(mode.id===4)o.frequency.exponentialRampToValueAtTime(notes[i%notes.length]*1.5,start+.26);g.gain.setValueAtTime(0,start);g.gain.linearRampToValueAtTime(type==='show'?.032:.025,start+.014);g.gain.exponentialRampToValueAtTime(.001,start+.25);o.connect(g).connect(audioCtx.destination);o.start(start);o.stop(start+.3);}}

  function activate(hit,event){
    if(opening||performing)return;
    const rect=scene.getBoundingClientRect();const isPoint=event&&event.detail!==0;
    const x=isPoint?clamp((event.clientX-rect.left)/rect.width,0,1):(hit.x+hit.w/2)/layout.width;
    const y=isPoint?clamp((event.clientY-rect.top)/rect.height,0,1):(hit.y+hit.h/2)/layout.height;
    emit(x,y,mode.id===3?34:24,false);tone('tap');
    if(hit.id==='LiveOnStage'){startShow();return;}
    if(hit.id==='Nav-lobby'){intro();toast('已经在大厅 · 重播入场');return;}
    openSheet(hit.id,event?.currentTarget);
  }
  function showImpact(text){if(paused||intensity===0)return;$('impact').textContent=text;$('impact').classList.remove('fire');void $('impact').offsetWidth;$('impact').classList.add('fire');clearTimeout(impactTimer);impactTimer=setTimeout(()=>$('impact').classList.remove('fire'),750);}
  function startShow(){
    if(!ready||opening||performing)return;closeSheet(false);clearActions();opening=true;previousFocus=document.activeElement;
    $('trigger-show').disabled=true;$('hotspots').inert=true;$('interaction-status').textContent=mode.title+' · 开演转场';showImpact(mode.impact);emit(.72,.66,100,true);tone('show');
    const delay=paused||intensity===0?0:mode.id===3?350:mode.id===5?650:450;
    clearTimeout(showTimer);showTimer=setTimeout(()=>{opening=false;performing=true;scene.classList.add('performing');$('performance').hidden=false;$('return-home').focus({preventScroll:true});emit(.5,.47,110,true);$('interaction-status').textContent='演出预演中 · 可随时返回首页';},delay);
  }
  function stopShow(restore=true){clearTimeout(showTimer);opening=false;performing=false;scene.classList.remove('performing');$('performance').hidden=true;$('trigger-show').disabled=!ready;$('hotspots').inert=!ready;clearTimeout(impactTimer);$('impact').classList.remove('fire');if(restore)restoreFocus();$('interaction-status').textContent='点击画面中的按钮试试看';}

  const row=(symbol,title,sub,end,attrs='')=>`<button class="sheet-row" ${attrs}><span class="row-symbol">${symbol}</span><span><strong>${title}</strong><small>${sub}</small></span><span class="row-end">${end}</span></button>`;
  function openSheet(action,opener){
    previousFocus=opener||document.activeElement;clearActions();$('sheet-title').textContent=LABELS[action];$('sheet-category').textContent=mode.en+' / '+(action.startsWith('Nav-')?'SIREN CLUB':'BACKSTAGE');
    let html='';
    if(action==='Tasks')html='<p class="sheet-intro">今天也朝梦想前进一步。</p>'+row('✦','完成一场演出','今日任务 · 1 / 1','领取','data-demo="reward"')+row('♫','进入练习室','今日任务 · 0 / 1','去练习','data-demo="practice"')+row('♡','与成员打个招呼','今日任务 · 0 / 1','去看看','data-demo="members"');
    else if(action==='AlbumProduction')html='<p class="sheet-intro">首张专辑《FIRST LIGHT》<br>给她的声音，选一个心动的方向。</p>'+row('◈','霓虹序曲','电子流行 · 闪耀舞台','选择','data-select="track"')+row('☾','月光来信','轻柔抒情 · 梦中私语','选择','data-select="track"')+row('✹','不设限','流行摇滚 · 热烈自由','选择','data-select="track"')+'<button class="sheet-action" data-demo="record">开始录制预演</button>';
    else if(action==='PracticeRoom')html='<p class="sheet-intro">跟随节奏轻点，让灵感逐渐升温。<br>点击下方按钮，完成 4 次练习。</p><div class="progress-track"><span id="practice-progress"></span></div><button class="sheet-action" id="practice-tap">♫ 跟拍 · 0 / 4</button>';
    else if(action==='Nav-members'||action==='Nav-team')html='<p class="sheet-intro">同一个她，三种舞台气质。<br>点选一张概念卡，决定今天的主唱。</p><div class="member-grid">'+['Cecelia','PRISM','DREAM'].map((s,i)=>`<button class="member-card${i===0?' selected':''}" data-select="member" aria-pressed="${i===0}"><span class="member-portrait"></span><strong>${s}</strong><small>${['LEAD VOCAL','SYNTH POP','DREAM POP'][i]}</small></button>`).join('')+'</div><button class="sheet-action" data-demo="team">确认舞台阵容</button>';
    else if(action==='Nav-accessory')html='<p class="sheet-intro">为舞台点缀一束属于你的光。</p>'+row('◇','棱镜耳饰','冷光系列 · 舞台限定','试戴','data-select="accessory"')+row('✧','星轨项链','星尘系列 · 自由漂浮','试戴','data-select="accessory"')+row('❋','绮梦发饰','花火系列 · 温柔绽放','试戴','data-select="accessory"');
    else if(action==='Nav-audition')html='<p class="sheet-intro">STAR CALL · 下一颗星，就是你。</p>'+row('★','新星招募','声音、风格与无限可能','OPEN','data-demo="audition"')+'<button class="sheet-action" data-demo="audition">点亮新星预演</button>';
    else if(action==='Profile')html='<div class="profile-avatar" aria-hidden="true"></div><p class="sheet-intro">音律少女<br>LV. 1 · 战力 1003<br>梦想的第一章，刚刚开始。</p>'+row('✦','今天的舞台宣言','「我的声音，会被听见。」','♡','data-demo="declaration"');
    else if(action==='Mail')html='<p class="sheet-intro">来自舞台另一边的心意。</p>'+row('✉','欢迎来到 CHO-SIREN','制作组 · 第一封来信','阅读','data-demo="mail"')+row('♫','今晚，我们舞台见','Cecelia · 一份演出邀请','阅读','data-demo="invitation"');
    else if(action==='Settings')html='<p class="sheet-intro">找到适合你的观看节奏。</p>'+row('◉','画面动效',paused?'当前已暂停':'当前正在播放',paused?'继续':'暂停','data-demo="motion"')+row('♫','互动音效',soundOn?'当前已开启':'当前已关闭',soundOn?'关闭':'开启','data-demo="sound"')+row('↻','重新入场','再看一次完整的登场动画','重播','data-demo="replay"');
    else html='<p class="sheet-intro">'+(action==='Currency-diamond'?'390 钻石 · 把闪耀留给下一场演出。':action==='Currency-gold'?'1,200 星光币 · 每一步成长都在积累。':'120 / 120 体力 · 今天的舞台，已经准备好。')+'</p><button class="sheet-action" data-demo="resource">感受一次补给动效</button>';
    $('sheet-content').innerHTML=html;$('sheet-backdrop').hidden=false;$('game-sheet').hidden=false;$('hotspots').inert=true;
    $('close-sheet').focus({preventScroll:true});$('interaction-status').textContent=LABELS[action]+' · '+mode.title+'面板动效';
    let practice=0;
    $('sheet-content').querySelectorAll('[data-select]').forEach(btn=>btn.addEventListener('click',()=>{const group=btn.dataset.select;$('sheet-content').querySelectorAll(`[data-select="${group}"]`).forEach(el=>{el.classList.toggle('selected',el===btn);el.setAttribute('aria-pressed',String(el===btn));const end=el.querySelector('.row-end');if(end)end.textContent=el===btn?'已选':'选择';});tone('tap');emit(.5,.72,15,false);}));
    $('practice-tap')?.addEventListener('click',()=>{practice=Math.min(practice+1,4);$('practice-progress').style.width=practice/4*100+'%';$('practice-tap').textContent=practice===4?'✦ 练习完成 · 再来一轮':'♫ 跟拍 · '+practice+' / 4';tone('tap');emit(.5,.76,24,false);if(practice===4){showImpact('PERFECT');practice=0;}});
    $('sheet-content').querySelectorAll('[data-demo]').forEach(btn=>btn.addEventListener('click',async()=>{
      tone('tap');const what=btn.dataset.demo;
      if(what==='reward'){btn.querySelector('.row-end').textContent='已领取';btn.disabled=true;emit(.5,.6,45,false);toast('奖励星光已收下 · 演示效果');}
      if(what==='practice')openSheet('PracticeRoom',previousFocus);
      if(what==='members')openSheet('Nav-members',previousFocus);
      if(what==='record'){if(!$('sheet-content').querySelector('.selected')){toast('先选一首你喜欢的歌');return;}closeSheet(false);startShow();}
      if(what==='team'){closeSheet();toast('已选定今天的主唱 · 演示效果');emit(.5,.8,35,false);}
      if(what==='audition'){closeSheet(false);showImpact('NEW STAR');emit(.5,.4,100,true);toast('新星登场！');}
      if(what==='declaration'){btn.classList.toggle('selected');btn.querySelector('.row-end').textContent=btn.classList.contains('selected')?'♥':'♡';emit(.5,.65,20,false);}
      if(what==='mail'){btn.querySelector('small').textContent='愿你在这里，听见属于自己的声音。';btn.querySelector('.row-end').textContent='已读';}
      if(what==='invitation'){btn.querySelector('small').textContent='留一束追光给我，好吗？今晚见。';btn.querySelector('.row-end').textContent='已读';}
      if(what==='motion'){togglePause();btn.querySelector('small').textContent=paused?'当前已暂停':'当前正在播放';btn.querySelector('.row-end').textContent=paused?'继续':'暂停';}
      if(what==='sound'){await toggleSound();btn.querySelector('small').textContent=soundOn?'当前已开启':'当前已关闭';btn.querySelector('.row-end').textContent=soundOn?'关闭':'开启';}
      if(what==='replay'){closeSheet(false);intro();}
      if(what==='resource'){closeSheet();emit(.5,.12,65,false);toast('补给光效预演 · 示例数值保持不变');}
    }));
  }
  function restoreFocus(){const target=previousFocus?.isConnected&&previousFocus.getClientRects().length&&previousFocus!==body?previousFocus:document.querySelector('.hotspot[data-action="LiveOnStage"]');target?.focus({preventScroll:true});}
  function closeSheet(restore=true){const wasOpen=!$('game-sheet').hidden;$('game-sheet').hidden=true;$('sheet-backdrop').hidden=true;$('hotspots').inert=!ready||performing||opening;if(wasOpen&&restore)restoreFocus();if(wasOpen)$('interaction-status').textContent='点击画面中的按钮试试看';}
  function closeOverview(){$('overview').close();$('compare').setAttribute('aria-expanded','false');}

  function resize(){const r=scene.getBoundingClientRect();width=r.width;height=r.height;dpr=Math.min(devicePixelRatio||1,2);canvases.forEach(c=>{c.width=Math.round(width*dpr);c.height=Math.round(height*dpr);});contexts.forEach(c=>c.setTransform(dpr,0,0,dpr,0,0));}
  function emit(x,y,count,big){if(paused||intensity===0)return;const amount=Math.round(count*Math.min(intensity,1.2));for(let i=0;i<amount;i++){const a=i/Math.max(amount,1)*Math.PI*2+Math.random()*.4,speed=(big?.25:.12)*(Math.random()*.7+.3);bursts.push({x,y,vx:Math.cos(a)*speed,vy:Math.sin(a)*speed*.5,age:0,life:big?1.9:1.05,size:(big?2:1)*(1+Math.random()*2),angle:a,hue:Math.random(),big});}if(bursts.length>260)bursts.splice(0,bursts.length-260);}
  function star(c,x,y,r,alpha,color=mode.color){c.globalAlpha=alpha;c.fillStyle=color;c.beginPath();c.moveTo(x,y-r);c.quadraticCurveTo(x+r*.15,y-r*.15,x+r,y);c.quadraticCurveTo(x+r*.15,y+r*.15,x,y+r);c.quadraticCurveTo(x-r*.15,y+r*.15,x-r,y);c.quadraticCurveTo(x-r*.15,y-r*.15,x,y-r);c.fill();}
  function petal(c,x,y,s,angle,alpha){c.save();c.translate(x,y);c.rotate(angle);c.globalAlpha=alpha;c.fillStyle='#ffd6ef';c.beginPath();c.moveTo(0,-s);c.bezierCurveTo(s*1.4,-s*.5,s*.7,s*.8,0,s);c.bezierCurveTo(-s*.7,s*.4,-s,-s*.8,0,-s);c.fill();c.restore();}
  function drawAtmosphere(c,t){
    c.clearRect(0,0,width,height);if(intensity===0)return;const e=Math.min(intensity,1.2),w=width,h=height;
    c.save();
    if(mode.id===1){
      const beat=Math.pow(Math.max(0,Math.sin(t*Math.PI*2/1.12)),9);
      const g=c.createRadialGradient(w*.78,h*.71,0,w*.78,h*.71,w*.6);g.addColorStop(0,`rgba(197,255,121,${.06*beat*e})`);g.addColorStop(1,'rgba(197,255,121,0)');c.fillStyle=g;c.fillRect(0,0,w,h);
      for(let i=0;i<24;i++){const p=particles[i],x=(p.x<.5?p.x*.35:.85+(p.x-.5)*.3)*w,y=(1-fract(p.y+t*.017*p.v))*h;star(c,x,y,1+p.s*2.5,(.14+p.s*.25)*e,p.n>.5?'#e6ffd0':'#c8a4ff');}
      for(let i=0;i<3;i++){const phase=fract(t*.44+i/3);c.globalAlpha=(1-phase)*.16*e;c.strokeStyle='#c5ff79';c.lineWidth=.7;c.beginPath();c.ellipse(w*.73,h*.65,w*(.1+phase*.38),h*(.05+phase*.12),-.4,0,Math.PI*2);c.stroke();}
    }else if(mode.id===2){
      c.strokeStyle='#8affeb';c.lineWidth=.5;for(let i=0;i<18;i++){const p=particles[i],x=fract(p.x+t*.008*(i%2?1:-1))*w,y=p.y*h,s=3+p.s*5;c.globalAlpha=(.18+Math.sin(t+p.a)*.1)*e;c.strokeRect(x-s/2,y-s/2,s,s);c.beginPath();c.moveTo(x-2,y);c.lineTo(x+2,y);c.stroke();}
      c.globalAlpha=.12*e;c.setLineDash([2,5]);c.beginPath();c.moveTo(w*.04,h*.2);c.lineTo(w*.32,h*.2);c.lineTo(w*.4,h*.25);c.moveTo(w*.96,h*.42);c.lineTo(w*.83,h*.42);c.lineTo(w*.7,h*.48);c.stroke();c.setLineDash([]);
      c.font=`${w*.018}px monospace`;c.globalAlpha=.55*e;c.fillStyle='#bafff4';c.fillText('SYNC '+String(Math.floor(t*3)%1000).padStart(3,'0'),w*.05,h*.205);c.fillText('PRISM / ONLINE',w*.7,h*.416);
      if(pointer.inside){c.save();c.translate(smoothed.x*w,smoothed.y*h);c.rotate(t*.5);c.globalAlpha=.32*e;c.beginPath();for(let i=0;i<4;i++){const a=i*Math.PI/2;c.moveTo(Math.cos(a)*20,Math.sin(a)*20);c.arc(0,0,20,a,a+.65);}c.stroke();c.restore();}
    }else if(mode.id===3){
      const tick=Math.floor(t*3);for(let i=0;i<12;i++){const p=particles[i],x=(p.x>.5?.9+p.s*.08:.02+p.s*.07)*w,y=fract(p.y+tick*.003)*h;c.save();c.translate(x,y);c.rotate(p.a+tick*.07);c.globalAlpha=.45*e;c.fillStyle=i%2?'#ffe566':'#fd86c9';if(i%3===0){c.fillRect(-2,-5,4,10);}else{c.beginPath();c.moveTo(-4,3);c.lineTo(0,-5);c.lineTo(4,3);c.closePath();c.fill();}c.restore();}
      const pulse=Math.sin(t*2)> .83;if(pulse){c.strokeStyle='#ffe888';c.globalAlpha=.23*e;c.lineWidth=1.2;for(let i=0;i<8;i++){const a=i*Math.PI/4+.2;c.beginPath();c.moveTo(w*.5+Math.cos(a)*w*.53,h*.5+Math.sin(a)*h*.5);c.lineTo(w*.5+Math.cos(a)*w*.38,h*.5+Math.sin(a)*h*.4);c.stroke();}}
    }else if(mode.id===4){
      for(let i=0;i<74;i++){const p=particles[i];let x=fract(p.x+t*.003*p.v)*w,y=fract(p.y-t*.005*p.v)*h;x+=(smoothed.x-.5)*p.s*13;y+=(smoothed.y-.5)*p.s*13;const dx=pointer.x*w-x,dy=pointer.y*h-y,dist=Math.hypot(dx,dy);if(pointer.inside&&dist<90){x+=dx*(1-dist/90)*.17;y+=dy*(1-dist/90)*.17;}const alpha=(.17+p.s*.3+Math.sin(t*p.v+p.a)*.12)*e;c.globalAlpha=alpha;c.fillStyle=i%3?'#d6e0ff':'#a7dfff';c.beginPath();c.arc(x,y,.45+p.s*.8,0,Math.PI*2);c.fill();if(p.s>.91)star(c,x,y,3,alpha*.75,'#d7eaff');}
      const shoot=fract(t/8);if(shoot<.13){const n=shoot/.13;c.globalAlpha=Math.sin(n*Math.PI)*.55*e;const x=(.9-n*.7)*w,y=(.14+n*.18)*h;const g=c.createLinearGradient(x,y,x+40,y-20);g.addColorStop(0,'#dfebff');g.addColorStop(1,'#aabbff00');c.strokeStyle=g;c.lineWidth=1;c.beginPath();c.moveTo(x,y);c.lineTo(x+40,y-20);c.stroke();}
    }else{
      for(let i=0;i<30;i++){const p=particles[i];const y=fract(p.y+t*.009*p.v)*h,x=(p.x+Math.sin(t*.45+p.a)*.04)*w;if(i<15){petal(c,x,y,1.5+p.s*2.4,t*.5+p.a,(.24+p.s*.3)*e);}else{const g=c.createRadialGradient(x,y,0,x,y,3+p.s*5);g.addColorStop(0,`rgba(255,225,245,${(.14+p.s*.22)*e})`);g.addColorStop(1,'rgba(255,195,225,0)');c.globalAlpha=1;c.fillStyle=g;c.fillRect(x-8,y-8,16,16);}}
      if(trail.length>1){c.lineCap='round';for(let i=1;i<trail.length;i++){const p=trail[i],prev=trail[i-1];c.globalAlpha=Math.max(0,1-p.age/.8)*.5*e;c.strokeStyle=i%2?'#ffd1ed':'#c7beff';c.lineWidth=1.5;c.beginPath();c.moveTo(prev.x*w,prev.y*h);c.quadraticCurveTo(prev.x*w,p.y*h,p.x*w,p.y*h);c.stroke();}}
    }
    c.restore();
  }
  function drawSparks(c,dt,t){
    c.clearRect(0,0,width,height);if(intensity===0)return;const w=width,h=height;
    bursts.forEach(p=>{p.age+=dt;p.x+=p.vx*dt;p.y+=p.vy*dt;if(mode.id===3||mode.id===5)p.vy+=dt*.04;const alpha=Math.max(0,1-p.age/p.life);c.save();const x=p.x*w,y=p.y*h;c.translate(x,y);c.rotate(p.angle+p.age*(mode.id===3?5:1));c.globalAlpha=alpha;c.strokeStyle=mode.color;c.fillStyle=p.hue>.75?'#fff':mode.color;
      if(mode.id===1){c.fillRect(-p.size/2,-p.size*2,p.size,p.size*4);}
      else if(mode.id===2){c.lineWidth=.8;c.strokeRect(-p.size,-p.size,p.size*2,p.size*2);}
      else if(mode.id===3){c.beginPath();c.moveTo(0,-p.size*2);c.lineTo(p.size*1.5,p.size);c.lineTo(-p.size*1.5,p.size);c.closePath();c.fill();}
      else if(mode.id===4){c.lineWidth=.7;c.beginPath();c.moveTo(0,0);c.lineTo(-p.size*3,0);c.stroke();star(c,0,0,p.size*.8,alpha);}
      else petal(c,0,0,p.size*1.6,0,alpha);
      c.restore();});bursts=bursts.filter(p=>p.age<p.life);
    if(performing&&!paused){c.save();c.globalAlpha=.22*Math.min(intensity,1);c.strokeStyle=mode.color;c.lineWidth=.6;
      if(mode.id===4){for(let i=0;i<40;i++){const p=particles[i],r=fract(p.s+t*.4),a=p.a;c.beginPath();c.moveTo(w*.5+Math.cos(a)*r*w,h*.43+Math.sin(a)*r*h);c.lineTo(w*.5+Math.cos(a)*(r+.035)*w,h*.43+Math.sin(a)*(r+.035)*h);c.stroke();}}
      else if(mode.id===5){for(let i=0;i<22;i++){const p=particles[i];petal(c,fract(p.x+Math.sin(t*.5+p.a)*.05)*w,fract(p.y+t*.05)*h,2+p.s*3,t+p.a,.3);}}
      c.restore();}
  }
  function animate(stamp){
    const dt=Math.min((stamp-lastFrame)/1000||0,.04);lastFrame=stamp;
    const run=ready&&!paused&&intensity>0&&!document.hidden;
    if(run){time+=dt;smoothed.x=mix(smoothed.x,pointer.inside?pointer.x:.5,.065);smoothed.y=mix(smoothed.y,pointer.inside?pointer.y:.45,.065);
      const px=(smoothed.x-.5)*intensity,py=(smoothed.y-.45)*intensity;
      const depth=mode.id===2?1.6:mode.id===4?1.25:mode.id===3?.5:1;
      $('device').style.setProperty('--tilt-x',px*(mode.id===2?5:2)+'deg');$('device').style.setProperty('--tilt-y',-py*(mode.id===2?3:1.5)+'deg');
      layerNodes.forEach(l=>{if(l.char||l.outline)l.node.style.translate=`${px*3*depth}px ${py*2*depth}px`;else if(l.e.id==='layer-00')l.node.style.translate=`${-px*2*depth}px ${-py*2*depth}px`;});
      trail.forEach(p=>p.age+=dt);trail=trail.filter(p=>p.age<.8);drawAtmosphere(contexts[0],time);drawSparks(contexts[1],dt,time);
    }else if(intensity===0){contexts.forEach(c=>c.clearRect(0,0,width,height));bursts=[];trail=[];}
    raf=requestAnimationFrame(animate);
  }

  buildHome();buildSelector();setMode(Number(location.hash.match(/^#v([1-5])$/)?.[1]||1),false);updateMotion();updateSound();resize();
  $('hotspots').inert=true;$('trigger-show').disabled=true;
  const imageLoads=layerNodes.map(({img})=>img.decode?img.decode():new Promise((res,rej)=>{img.onload=res;img.onerror=rej;}));
  let loaded=0;imageLoads.forEach(p=>p.then(()=>{$('load-progress').textContent=`正在装载首页图层 · ${++loaded} / ${layerNodes.length}`;},()=>{}));
  Promise.all(imageLoads).then(()=>{ready=true;scene.classList.remove('loading');$('hotspots').inert=false;$('trigger-show').disabled=false;intro();}).catch(()=>{$('load-progress').textContent='图层加载失败，请保留 assets 文件夹后重新打开。';});
  new ResizeObserver(resize).observe(scene);
  scene.addEventListener('pointermove',e=>{const r=scene.getBoundingClientRect();pointer={x:clamp((e.clientX-r.left)/r.width,0,1),y:clamp((e.clientY-r.top)/r.height,0,1),inside:true};if(mode.id===5&&!paused&&intensity>0){trail.push({x:pointer.x,y:pointer.y,age:0});if(trail.length>32)trail.shift();}});
  scene.addEventListener('pointerleave',()=>{pointer.inside=false;clearActions();});
  window.addEventListener('pointerup',()=>{for(const id of actionNodes.keys())setActionClass(id,'pressed',false);});
  window.addEventListener('blur',()=>{pointer.inside=false;clearActions();});
  document.addEventListener('visibilitychange',()=>{if(document.hidden){clearActions();cancelAnimationFrame(raf);body.classList.add('paused');}else{updateMotion();lastFrame=performance.now();raf=requestAnimationFrame(animate);}});
  window.addEventListener('hashchange',()=>{const n=Number(location.hash.match(/^#v([1-5])$/)?.[1]);if(n&&n!==mode.id)setMode(n,false);});
  $('intensity').addEventListener('input',e=>{intensity=Number(e.target.value)/100;updateMotion();});
  $('pause').addEventListener('click',togglePause);$('sound').addEventListener('click',toggleSound);$('replay').addEventListener('click',()=>{intro();tone('tap');});$('trigger-show').addEventListener('click',startShow);$('return-home').addEventListener('click',()=>{stopShow();tone('tap');});
  $('favorite').addEventListener('click',()=>{const has=favorites.includes(mode.id);favorites=has?favorites.filter(x=>x!==mode.id):[...favorites,mode.id].sort();try{localStorage.setItem('cho-motion-lab-favorites',JSON.stringify(favorites));}catch{}updateFavorite();toast(has?'已取消收藏':mode.title+'已收藏，可以继续比较其他方向');});
  $('close-sheet').addEventListener('click',()=>closeSheet());$('sheet-backdrop').addEventListener('click',()=>closeSheet());
  $('compare').addEventListener('click',()=>{$('overview').showModal();$('compare').setAttribute('aria-expanded','true');});$('close-overview').addEventListener('click',closeOverview);$('overview').addEventListener('close',()=>$('compare').setAttribute('aria-expanded','false'));$('overview').addEventListener('click',e=>{if(e.target===$('overview')){const r=$('overview').getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)closeOverview();}});
  document.addEventListener('keydown',e=>{
    if($('overview').open)return;
    if(e.key==='Escape'){if(!$('game-sheet').hidden){e.preventDefault();closeSheet();}else if(performing||opening){e.preventDefault();stopShow();}return;}
    if(e.key==='Tab'&&(!$('game-sheet').hidden||performing)){const area=performing?$('performance'):$('game-sheet'),focusable=[...area.querySelectorAll('button:not(:disabled),input,[tabindex="0"]')],first=focusable[0],last=focusable.at(-1);if(e.shiftKey&&(document.activeElement===first||!area.contains(document.activeElement))){last?.focus();e.preventDefault();}else if(!e.shiftKey&&(document.activeElement===last||!area.contains(document.activeElement))){first?.focus();e.preventDefault();}return;}
    if(/INPUT|TEXTAREA|SELECT/.test(e.target.tagName)||e.metaKey||e.ctrlKey||e.altKey)return;
    if(/^[1-5]$/.test(e.key)){e.preventDefault();setMode(Number(e.key));}
    if(e.key===' '&&e.target===body){e.preventDefault();togglePause();}
  });
  reduced.addEventListener('change',e=>{if(e.matches){paused=true;intensity=0;updateMotion();}});
  raf=requestAnimationFrame(animate);
})();

const members=[['星璃','主唱','68'],['绯音','舞者','64'],['雾白','支援','59'],['夜莺','主唱','57'],['澄夏','舞者','52'],['弦月','支援','49'],['琥珀','主唱','46'],['遥光','舞者','43'],['初雪','支援','40']];
const memberCard=(m,i,compact=false)=>`<article class="member-card" style="--n:${i}"><div class="art"></div><div class="meta"><small>${m[1]} · SSR</small><b>${m[0]}</b><em>Lv.${m[2]}</em></div></article>`;
document.querySelector('#memberGrid').innerHTML=members.slice(0,4).map((m,i)=>memberCard(m,i)).join('');
document.querySelector('#rosterGrid').innerHTML=members.map((m,i)=>memberCard(m,i,true)).join('');
const items=['◇','♬','✦','♡','◈','♛','✧','♫','◉','⌁','♢','✿','✪','♧','⬡','❖','♩','✺','◎','♜'];
document.querySelector('#inventoryGrid').innerHTML=items.map((x,i)=>`<button class="item" data-toast="${['共鸣音核','舞台饰品','星辉素材','纪念唱片'][i%4]} ×${i+1}"><em>${i%3?'SR':'SSR'}</em>${x}<small>×${i+1}</small></button>`).join('');
const screens=[...document.querySelectorAll('.screen')];
document.querySelectorAll('[data-screen]').forEach(btn=>btn.addEventListener('click',()=>{
  screens.forEach(s=>s.classList.toggle('active',s.id===btn.dataset.screen));
  document.querySelectorAll('[data-screen]').forEach(b=>b.classList.toggle('active',b===btn));
}));
let toastTimer;document.addEventListener('click',e=>{const el=e.target.closest('[data-toast]');if(!el)return;const toast=document.querySelector('.toast');toast.textContent=el.dataset.toast;toast.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>toast.classList.remove('show'),1600)});
document.querySelectorAll('.filter-row').forEach(row=>row.addEventListener('click',e=>{if(!e.target.matches('button'))return;row.querySelectorAll('button').forEach(b=>b.classList.toggle('active',b===e.target));}));
document.querySelector('#sortBtn').addEventListener('click',e=>{
  const grid=document.querySelector('#rosterGrid');
  e.currentTarget.textContent=e.currentTarget.textContent.includes('↓')?'战力 ↑':'战力 ↓';
  [...grid.children].reverse().forEach(card=>grid.append(card));
});

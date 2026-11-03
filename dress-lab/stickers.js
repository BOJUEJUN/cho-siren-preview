// 配饰贴纸：每个都是 100×100 的 SVG，c 是主色。
window.DressStickers = (() => {
  const K = '#16141c';
  const wrap = (body, vb = '0 0 100 100') =>
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb}">${body}</svg>`;
  const mirror = body => `${body}<g transform="translate(100 0) scale(-1 1)">${body}</g>`;
  const heart = (cx, cy, r) => {
    const s = r / 20;
    const p = (x, y) => `${(cx + x * s).toFixed(1)} ${(cy + y * s).toFixed(1)}`;
    return `M${p(0, 18)} C${p(-22, 4)} ${p(-24, -14)} ${p(-12, -18)} C${p(-6, -20)} ${p(0, -15)} ${p(0, -10)} C${p(0, -15)} ${p(6, -20)} ${p(12, -18)} C${p(24, -14)} ${p(22, 4)} ${p(0, 18)}Z`;
  };
  const star = (cx, cy, R, r, n = 5) => {
    const pts = [];
    for (let i = 0; i < n * 2; i++) {
      const a = -Math.PI / 2 + (i * Math.PI) / n;
      const d = i % 2 ? r : R;
      pts.push(`${(cx + Math.cos(a) * d).toFixed(1)},${(cy + Math.sin(a) * d).toFixed(1)}`);
    }
    return pts.join(' ');
  };

  const list = [
    { id: 'cat', name: '猫耳', color: '#ff3fa4', size: 150, anchor: 'top',
      svg: c => wrap(mirror(`<path d="M8 74 L20 12 L46 54 Z" fill="${K}" stroke="${c}" stroke-width="3" stroke-linejoin="round"/><path d="M16 62 L21 30 L36 52 Z" fill="${c}" opacity=".85"/>`)) },
    { id: 'horn', name: '小恶魔角', color: '#9b5cff', size: 140, anchor: 'top',
      svg: c => wrap(mirror(`<path d="M24 80 C10 52 14 26 32 8 C29 32 35 50 44 68 Z" fill="${c}" stroke="${K}" stroke-width="3" stroke-linejoin="round"/>`)) },
    { id: 'halo', name: '光环', color: '#19f0ff', size: 150, anchor: 'top',
      svg: c => wrap(`<ellipse cx="50" cy="50" rx="44" ry="15" fill="none" stroke="${c}" stroke-width="12" opacity=".25"/><ellipse cx="50" cy="50" rx="42" ry="13" fill="none" stroke="${c}" stroke-width="6"/><ellipse cx="50" cy="48" rx="40" ry="11" fill="none" stroke="#fff" stroke-width="1.5" opacity=".8"/>`) },
    { id: 'crown', name: '王冠', color: '#ffd23f', size: 120, anchor: 'top',
      svg: c => wrap(`<path d="M12 72 L16 28 L35 50 L50 16 L65 50 L84 28 L88 72 Z" fill="${c}" stroke="${K}" stroke-width="3" stroke-linejoin="round"/><rect x="10" y="70" width="80" height="14" rx="4" fill="${c}" stroke="${K}" stroke-width="3"/><circle cx="50" cy="77" r="4.5" fill="#ff3fa4" stroke="${K}" stroke-width="2"/><circle cx="30" cy="77" r="3.5" fill="#19f0ff" stroke="${K}" stroke-width="2"/><circle cx="70" cy="77" r="3.5" fill="#19f0ff" stroke="${K}" stroke-width="2"/>`) },
    { id: 'bow', name: '蝴蝶结', color: '#ff3fa4', size: 100, anchor: 'top',
      svg: c => wrap(`${mirror(`<path d="M50 50 L12 24 Q2 50 12 76 Z" fill="${c}" stroke="${K}" stroke-width="3" stroke-linejoin="round"/><path d="M40 48 L18 34" stroke="#fff" stroke-width="2" opacity=".55"/>`)}<rect x="41" y="39" width="18" height="22" rx="6" fill="${c}" stroke="${K}" stroke-width="3"/>`) },
    { id: 'clip', name: '星星发卡', color: '#ffd23f', size: 70, anchor: 'top',
      svg: c => wrap(`<polygon points="${star(50, 52, 44, 19)}" fill="${c}" stroke="${K}" stroke-width="4" stroke-linejoin="round"/><polygon points="${star(44, 44, 10, 4, 4)}" fill="#fff" opacity=".85"/>`) },
    { id: 'phones', name: '耳机', color: '#19f0ff', size: 170, anchor: 'top',
      svg: c => wrap(`<path d="M15 64 A35 35 0 0 1 85 64" fill="none" stroke="${K}" stroke-width="10"/><path d="M15 64 A35 35 0 0 1 85 64" fill="none" stroke="${c}" stroke-width="3.5"/><rect x="4" y="52" width="20" height="32" rx="8" fill="${c}" stroke="${K}" stroke-width="3"/><rect x="76" y="52" width="20" height="32" rx="8" fill="${c}" stroke="${K}" stroke-width="3"/><rect x="10" y="60" width="8" height="16" rx="3" fill="${K}"/><rect x="82" y="60" width="8" height="16" rx="3" fill="${K}"/>`) },
    { id: 'hglass', name: '爱心墨镜', color: '#ff3fa4', size: 120, anchor: 'face',
      svg: c => wrap(`<path d="${heart(27, 50, 21)}" fill="${c}" fill-opacity=".85" stroke="${K}" stroke-width="3.5"/><path d="${heart(73, 50, 21)}" fill="${c}" fill-opacity=".85" stroke="${K}" stroke-width="3.5"/><path d="M44 44 Q50 39 56 44" fill="none" stroke="${K}" stroke-width="3.5"/><path d="M17 42 L22 38" stroke="#fff" stroke-width="3" stroke-linecap="round"/><path d="M63 42 L68 38" stroke="#fff" stroke-width="3" stroke-linecap="round"/>`) },
    { id: 'visor', name: '赛博墨镜', color: '#19f0ff', size: 130, anchor: 'face',
      svg: c => wrap(`<path d="M4 40 Q50 24 96 40 L91 60 Q50 74 9 60 Z" fill="${c}" fill-opacity=".8" stroke="${K}" stroke-width="3.5" stroke-linejoin="round"/><path d="M14 44 Q50 33 86 44" fill="none" stroke="#fff" stroke-width="2.5" opacity=".75"/><path d="M12 54 L88 54" stroke="${K}" stroke-width="1.5" opacity=".35"/>`) },
    { id: 'mask', name: '朋克口罩', color: '#ff3fa4', size: 90, anchor: 'face',
      svg: c => wrap(`<path d="M8 32 Q50 22 92 32 L86 62 Q50 86 14 62 Z" fill="${K}" stroke="${c}" stroke-width="3" stroke-linejoin="round"/><path d="M28 46 L40 58 M40 46 L28 58 M60 46 L72 58 M72 46 L60 58" stroke="${c}" stroke-width="3.5" stroke-linecap="round"/><path d="M8 32 L2 26 M92 32 L98 26" stroke="${K}" stroke-width="3"/>`) },
    { id: 'choker', name: '铆钉项圈', color: '#c9c9d6', size: 90, anchor: 'neck',
      svg: c => wrap(`<path d="M8 36 Q50 62 92 36" fill="none" stroke="${K}" stroke-width="11" stroke-linecap="round"/>${[18, 34, 50, 66, 82].map((x, i) => { const y = 36 + 26 * (1 - Math.pow((x - 50) / 42, 2)) * 0.5 + 7; return `<path d="M${x - 4} ${y - 4} L${x} ${y + 5} L${x + 4} ${y - 4} Z" fill="${c}" stroke="${K}" stroke-width="1.5"/>`; }).join('')}<circle cx="50" cy="66" r="9" fill="none" stroke="${c}" stroke-width="4"/>`) },
    { id: 'bat', name: '蝙蝠翼', color: '#9b5cff', size: 380, anchor: 'back', back: true,
      svg: c => wrap(mirror(`<path d="M50 52 C42 32 24 18 2 22 C10 30 10 38 6 46 C14 44 19 48 16 57 C24 53 30 57 29 66 C36 60 44 59 50 64 Z" fill="${c}" fill-opacity=".9" stroke="${K}" stroke-width="2" stroke-linejoin="round"/><path d="M48 52 C38 36 22 26 6 26" fill="none" stroke="${K}" stroke-width="1.5" opacity=".5"/>`)) },
    { id: 'feather', name: '羽翼', color: '#7ee8ff', size: 380, anchor: 'back', back: true,
      svg: c => wrap(mirror([0, 1, 2, 3, 4].map(i => `<ellipse cx="${30 - i * 3}" cy="${46 + i * 3}" rx="${24 - i * 2}" ry="7" transform="rotate(${-34 + i * 17} 46 50)" fill="${i % 2 ? '#fff' : c}" fill-opacity=".9" stroke="${K}" stroke-width="1.5"/>`).join(''))) },
    { id: 'bolt', name: '闪电', color: '#ffd23f', size: 90, anchor: 'free',
      svg: c => wrap(`<polygon points="58,4 18,56 46,56 36,96 84,38 55,38 68,4" fill="${c}" stroke="${K}" stroke-width="4" stroke-linejoin="round"/>`) },
    { id: 'heart', name: '爱心', color: '#ff3fa4', size: 80, anchor: 'free',
      svg: c => wrap(`<path d="${heart(50, 50, 42)}" fill="${c}" stroke="${K}" stroke-width="4"/><path d="M28 36 Q32 28 40 28" fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" opacity=".8"/>`) },
    { id: 'spark', name: '星光', color: '#19f0ff', size: 90, anchor: 'free',
      svg: c => wrap(`<polygon points="${star(50, 50, 46, 9, 4)}" fill="${c}" stroke="${K}" stroke-width="2.5" stroke-linejoin="round"/><polygon points="${star(80, 20, 14, 3, 4)}" fill="${c}"/><polygon points="${star(22, 78, 10, 2.5, 4)}" fill="${c}"/>`) },
    { id: 'fly', name: '蝴蝶', color: '#2d6bff', size: 90, anchor: 'free',
      svg: c => wrap(`${mirror(`<path d="M50 48 C38 20 10 14 8 30 C6 44 28 50 50 50 Z" fill="${c}" stroke="${K}" stroke-width="2.5"/><path d="M50 52 C34 54 18 66 26 80 C34 90 46 72 50 54 Z" fill="${c}" fill-opacity=".8" stroke="${K}" stroke-width="2.5"/><circle cx="24" cy="32" r="5" fill="#fff" opacity=".7"/>`)}<rect x="47.5" y="34" width="5" height="40" rx="2.5" fill="${K}"/>`) },
    { id: 'mic', name: '麦克风', color: '#c9c9d6', size: 110, anchor: 'free',
      svg: c => wrap(`<rect x="44" y="44" width="12" height="50" rx="5" fill="${K}"/><rect x="42" y="44" width="16" height="8" fill="#ff3fa4"/><circle cx="50" cy="28" r="22" fill="${c}" stroke="${K}" stroke-width="4"/><path d="M34 22 H66 M32 30 H68 M36 38 H64 M44 8 V48 M56 8 V48" stroke="${K}" stroke-width="1.8" opacity=".55"/>`) }
  ];

  const cache = new Map();
  const url = (id, color) => {
    const key = `${id}|${color}`;
    if (!cache.has(key)) {
      const def = list.find(s => s.id === id);
      cache.set(key, `data:image/svg+xml;charset=utf-8,${encodeURIComponent(def.svg(color))}`);
    }
    return cache.get(key);
  };
  return { list, url, get: id => list.find(s => s.id === id) };
})();

// CHO-SIREN 造型工坊：点哪改哪的 2D 换色、图案、配饰与舞台，脸部像素默认受保护。
(() => {
  'use strict';
  const W = 512, H = 704, N = W * H;
  const STORE = 'dresslab:v1';
  const $ = id => document.getElementById(id);

  const MEMBERS = [
    { id: 'xingli', name: '星璃', head: [350, 140] },
    { id: 'feiyin', name: '绯音', head: [278, 158] },
    { id: 'wubai', name: '雾白', head: [296, 92] },
    { id: 'yeying', name: '夜莺', head: [232, 236] },
    { id: 'chengxia', name: '澄夏', head: [290, 196] },
    { id: 'xianyue', name: '弦月', head: [288, 112] },
    { id: 'hupo', name: '琥珀', head: [282, 128] },
    { id: 'yaoguang', name: '遥光', head: [286, 126] },
    { id: 'chuxue', name: '初雪', head: [288, 168] }
  ].map(m => ({ ...m, src: `../assets/member-${m.id}.webp` }));

  const PALETTE = ['#19f0ff', '#ff3fa4', '#9b5cff', '#2d6bff', '#3dff8a', '#ffd23f', '#ff8a3d', '#ff4a4a',
    '#ffb3d9', '#7ee8ff', '#ffffff', '#c9c9d6', '#5a5a6e', '#15151c'];
  const PATTERNS = [
    { id: '', name: '纯色' }, { id: 'stripe', name: '斜纹' }, { id: 'dots', name: '波点' },
    { id: 'plaid', name: '格纹' }, { id: 'grid', name: '霓虹网格' }, { id: 'zebra', name: '斑马纹' },
    { id: 'diamond', name: '菱格' }, { id: 'glitter', name: '闪粉' }, { id: 'gradient', name: '渐变' }
  ];
  const BGS = [
    { id: 'neon', name: '霓虹夜', css: 'radial-gradient(120% 70% at 50% 18%,#3b1d5e 0%,#160f2a 46%,#07070d 100%)', stops: [['#3b1d5e', 0], ['#160f2a', .46], ['#07070d', 1]] },
    { id: 'stage', name: '演唱会', img: '../assets/background-portrait-display.webp' },
    { id: 'pink', name: '甜心粉', css: 'linear-gradient(180deg,#ffd1ea 0%,#ff7cc0 55%,#7a2e8f 100%)', stops: [['#ffd1ea', 0], ['#ff7cc0', .55], ['#7a2e8f', 1]] },
    { id: 'ice', name: '冰蓝', css: 'linear-gradient(180deg,#dff9ff 0%,#69d8ff 50%,#123a73 100%)', stops: [['#dff9ff', 0], ['#69d8ff', .5], ['#123a73', 1]] },
    { id: 'sunset', name: '晚霞', css: 'linear-gradient(180deg,#ffcf6b 0%,#ff6a7a 50%,#3a1650 100%)', stops: [['#ffcf6b', 0], ['#ff6a7a', .5], ['#3a1650', 1]] },
    { id: 'mono', name: '纯黑', css: '#0b0b10', stops: [['#0b0b10', 0], ['#0b0b10', 1]] },
    { id: 'white', name: '纯白', css: '#f3f2f7', stops: [['#f3f2f7', 0], ['#f3f2f7', 1]] }
  ];
  const LIGHTS = [
    { id: 'none', name: '自然' },
    { id: 'neon', name: '霓虹', blend: 'soft-light', grad: ['#19f0ff', '#ff3fa4'], alpha: .75 },
    { id: 'cold', name: '冷光', blend: 'soft-light', color: '#3a8bff', alpha: .7 },
    { id: 'warm', name: '暖光', blend: 'soft-light', color: '#ff9a3d', alpha: .7 },
    { id: 'gray', name: '黑白', blend: 'saturation', color: '#808080', alpha: 1 }
  ];
  const GLOWS = [
    { id: 'none', name: '无' }, { id: '#19f0ff', name: '青' }, { id: '#ff3fa4', name: '粉' },
    { id: '#9b5cff', name: '紫' }, { id: '#ffd23f', name: '金' }, { id: '#ffffff', name: '白' }
  ];

  // ---------- 存储 ----------
  const store = {
    get(key, fallback) { try { const v = localStorage.getItem(`${STORE}:${key}`); return v ? JSON.parse(v) : fallback; } catch (_) { return fallback; } },
    set(key, value) { try { localStorage.setItem(`${STORE}:${key}`, JSON.stringify(value)); return true; } catch (_) { return false; } }
  };

  // ---------- 状态 ----------
  const blankLook = () => ({ edits: [], stickers: [], stage: { bg: 'neon', light: 'none', glow: 'none' }, name: '' });
  let member = MEMBERS[0];
  let look = blankLook();
  let undoStack = [], redoStack = [];
  let tab = 'color';
  let sel = null;          // { seed:[x,y], tol, global, protect }
  let activeEdit = -1;     // 正在调整的改动
  let activeSticker = null;
  let tol = 40, globalMode = false, protect = true;
  let lastColor = PALETTE[0], lastColor2 = '#ffffff';
  let view = { zoom: 1, tx: 0, ty: 0 };
  let peeking = false;

  // ---------- 像素数据 ----------
  const charCanvas = $('char'), cctx = charCanvas.getContext('2d', { willReadFrequently: true });
  const selCanvas = $('sel'), sctx = selCanvas.getContext('2d');
  let orig = null, Hh, S, L, C, A, skin;
  const maskCache = new Map();
  const imgCache = new Map();

  const loadImage = src => {
    if (!imgCache.has(src)) imgCache.set(src, new Promise((res, rej) => {
      const im = new Image(); im.decoding = 'async';
      im.onload = () => res(im); im.onerror = rej; im.src = src;
    }));
    return imgCache.get(src);
  };

  function analyse(im) {
    const c = document.createElement('canvas'); c.width = W; c.height = H;
    const x = c.getContext('2d', { willReadFrequently: true });
    x.drawImage(im, 0, 0, W, H);
    orig = x.getImageData(0, 0, W, H);
    const d = orig.data;
    Hh = new Float32Array(N); S = new Float32Array(N); L = new Float32Array(N); C = new Float32Array(N);
    A = new Uint8Array(N); skin = new Uint8Array(N);
    for (let i = 0; i < N; i++) {
      const r = d[i * 4] / 255, g = d[i * 4 + 1] / 255, b = d[i * 4 + 2] / 255;
      const mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2;
      let h = 0, s = 0;
      if (mx !== mn) {
        const dd = mx - mn;
        s = l > .5 ? dd / (2 - mx - mn) : dd / (mx + mn);
        h = mx === r ? (g - b) / dd + (g < b ? 6 : 0) : mx === g ? (b - r) / dd + 2 : (r - g) / dd + 4;
        h *= 60;
      }
      Hh[i] = h; S[i] = s; L[i] = l; C[i] = mx - mn; A[i] = d[i * 4 + 3];
      // 肤色：偏橙的暖色、中等饱和、偏亮
      skin[i] = h >= 5 && h <= 32 && s >= .25 && s <= .88 && l >= .58 && l <= .94 ? 1 : 0;
    }
    // 脸部核心（眼睛、嘴）整块保护
    const [fx, fy] = member.head;
    for (let y = Math.max(0, fy - 26); y <= Math.min(H - 1, fy + 26); y++)
      for (let x = Math.max(0, fx - 26); x <= Math.min(W - 1, fx + 26); x++)
        if ((x - fx) ** 2 + (y - fy) ** 2 <= 26 * 26) skin[y * W + x] = 1;
    maskCache.clear();
  }

  // 两个像素的颜色差：颜色越淡，色相越不重要；明暗差权重低，保住阴影
  function diff(i, j) {
    let dh = Math.abs(Hh[i] - Hh[j]); if (dh > 180) dh = 360 - dh;
    const cm = Math.min(1, Math.min(C[i], C[j]) / .1);
    return dh / 180 * 2 * cm + Math.abs(C[i] - C[j]) * .55 + Math.abs(L[i] - L[j]) * .42;
  }

  function computeMask(seed, t, global, prot) {
    const key = `${seed[0]},${seed[1]},${t},${global ? 1 : 0},${prot ? 1 : 0}`;
    if (maskCache.has(key)) return maskCache.get(key);
    const si = seed[1] * W + seed[0];
    if (A[si] < 40) return null;
    const tolv = t / 100;
    const usable = i => A[i] >= 40 && !(prot && skin[i]);
    // 相连模式：和种子大体相近，且和相邻像素变化平缓（遇到线稿和别的布料就停）
    const far = tolv * 1.0, step = .03 + tolv * .12;
    const m = new Uint8Array(N);
    if (global) {
      for (let i = 0; i < N; i++) if (usable(i) && diff(i, si) < tolv) m[i] = 255;
    } else if (usable(si)) {
      const stack = [si]; m[si] = 255;
      const ok = (j, from) => !m[j] && usable(j) && diff(j, from) < step && diff(j, si) < far;
      while (stack.length) {
        const i = stack.pop(), x = i % W;
        if (x > 0 && ok(i - 1, i)) { m[i - 1] = 255; stack.push(i - 1); }
        if (x < W - 1 && ok(i + 1, i)) { m[i + 1] = 255; stack.push(i + 1); }
        if (i >= W && ok(i - W, i)) { m[i - W] = 255; stack.push(i - W); }
        if (i < N - W && ok(i + W, i)) { m[i + W] = 255; stack.push(i + W); }
      }
    }
    // 边缘柔化：紧邻区域的一圈像素半强度上色，避免锯齿和杂色边
    let count = 0;
    for (let i = 0; i < N; i++) {
      if (m[i] === 255) { count++; continue; }
      if (A[i] < 20 || (prot && skin[i])) continue;
      const x = i % W;
      if ((x > 0 && m[i - 1] === 255) || (x < W - 1 && m[i + 1] === 255) || (i >= W && m[i - W] === 255) || (i < N - W && m[i + W] === 255)) m[i] = 110;
    }
    const res = count ? m : null;
    if (maskCache.size > 60) maskCache.delete(maskCache.keys().next().value);
    maskCache.set(key, res);
    return res;
  }

  const hexToRgb = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  function rgbToHsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2;
    if (mx === mn) return [0, 0, l];
    const d = mx - mn, s = l > .5 ? d / (2 - mx - mn) : d / (mx + mn);
    let h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return [h * 60, s, l];
  }
  function hue2(p, q, t) { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6) return p + (q - p) * 6 * t; if (t < .5) return q; if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6; return p; }
  function hslToRgb(h, s, l, out) {
    if (s <= 0) { out[0] = out[1] = out[2] = l * 255; return out; }
    const q = l < .5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q, hh = h / 360;
    out[0] = hue2(p, q, hh + 1 / 3) * 255; out[1] = hue2(p, q, hh) * 255; out[2] = hue2(p, q, hh - 1 / 3) * 255;
    return out;
  }
  const hexToHsl = h => rgbToHsl(...hexToRgb(h));

  function hash(x, y) { let n = x * 374761393 + y * 668265263; n = (n ^ (n >>> 13)) * 1274126177; return ((n ^ (n >>> 16)) >>> 0) / 4294967295; }
  function patternBit(p, x, y) {
    switch (p) {
      case 'stripe': return ((x + y) % 18 + 18) % 18 < 6;
      case 'dots': { const gx = x % 14 - 7, gy = (y + (Math.floor(x / 14) % 2) * 7) % 14 - 7; return gx * gx + gy * gy < 11; }
      case 'plaid': return x % 22 < 4 || y % 22 < 4 || (x % 22 > 10 && x % 22 < 12) || (y % 22 > 10 && y % 22 < 12);
      case 'grid': return x % 16 < 2 || y % 16 < 2;
      case 'zebra': return ((y + 7 * Math.sin(x / 10) + 4 * Math.sin(x / 3.7)) % 20 + 20) % 20 < 7;
      case 'diamond': { const gx = Math.abs(x % 20 - 10), gy = Math.abs(y % 20 - 10); return gx + gy > 8 && gx + gy < 11; }
      case 'glitter': return hash(x >> 1, y >> 1) > .9;
      default: return false;
    }
  }

  function applyEdit(buf, e, mask) {
    const t1 = hexToHsl(e.color), t2 = hexToHsl(e.color2 || '#ffffff');
    let sumL = 0, sumS = 0, n = 0, minY = H, maxY = 0;
    for (let i = 0; i < N; i++) if (mask[i] === 255) {
      sumL += L[i]; sumS += S[i]; n++;
      const y = (i / W) | 0; if (y < minY) minY = y; if (y > maxY) maxY = y;
    }
    if (!n) return;
    const avgL = sumL / n, avgS = sumS / n, light = (e.light || 0) / 100;
    const c1 = hexToRgb(e.color), c2 = hexToRgb(e.color2 || '#ffffff');
    const out = [0, 0, 0];
    for (let i = 0; i < N; i++) {
      const w = mask[i]; if (!w) continue;
      const x = i % W, y = (i / W) | 0;
      let th, ts, tl;
      if (e.pattern === 'gradient') {
        const k = (y - minY) / Math.max(1, maxY - minY);
        const hs = rgbToHsl(c1[0] + (c2[0] - c1[0]) * k, c1[1] + (c2[1] - c1[1]) * k, c1[2] + (c2[2] - c1[2]) * k);
        th = hs[0]; ts = hs[1]; tl = hs[2];
      } else if (e.pattern && patternBit(e.pattern, x, y)) { [th, ts, tl] = t2; }
      else { [th, ts, tl] = t1; }
      const nl = Math.min(1, Math.max(0, L[i] - avgL + tl + light));
      const ns = ts * Math.min(1.15, (S[i] + .3) / (avgS + .3));
      hslToRgb(th, Math.min(1, ns), nl, out);
      const k = w / 255, j = i * 4;
      buf[j] += (out[0] - buf[j]) * k; buf[j + 1] += (out[1] - buf[j + 1]) * k; buf[j + 2] += (out[2] - buf[j + 2]) * k;
    }
  }

  let renderQueued = false;
  function render() {
    if (renderQueued) return;
    renderQueued = true;
    requestAnimationFrame(() => { renderQueued = false; renderNow(); });
  }
  function renderNow() {
    if (!orig) return;
    const out = new ImageData(new Uint8ClampedArray(orig.data), W, H);
    if (!peeking) for (const e of look.edits) {
      const m = computeMask(e.seed, e.tol, e.global, e.protect);
      if (m) applyEdit(out.data, e, m);
    }
    cctx.putImageData(out, 0, 0);
    drawSelection();
    $('edit-count').textContent = `已改 ${look.edits.length} 处`;
  }

  function drawSelection() {
    sctx.clearRect(0, 0, W, H);
    if (!sel || peeking) { selCanvas.classList.remove('on'); return; }
    const m = computeMask(sel.seed, sel.tol, sel.global, sel.protect);
    if (!m) { selCanvas.classList.remove('on'); return; }
    const im = sctx.createImageData(W, H), d = im.data;
    for (let i = 0; i < N; i++) {
      if (m[i] !== 255) continue;
      const x = i % W, y = (i / W) | 0;
      const edge = (x > 0 && m[i - 1] !== 255) || (x < W - 1 && m[i + 1] !== 255) || (y > 0 && m[i - W] !== 255) || (y < H - 1 && m[i + W] !== 255);
      const j = i * 4;
      if (edge) { d[j] = 255; d[j + 1] = 255; d[j + 2] = 255; d[j + 3] = 255; }
      else { const on = ((x + y) >> 2) % 2; d[j] = 25; d[j + 1] = 240; d[j + 2] = 255; d[j + 3] = on ? 190 : 90; }
    }
    sctx.putImageData(im, 0, 0);
    selCanvas.classList.add('on');
  }

  // ---------- 历史 ----------
  const snapshot = () => JSON.stringify(look);
  function pushHistory() {
    undoStack.push(snapshot());
    if (undoStack.length > 60) undoStack.shift();
    redoStack = [];
    syncHistory();
  }
  function syncHistory() { $('undo').disabled = !undoStack.length; $('redo').disabled = !redoStack.length; }
  function restore(json) {
    look = JSON.parse(json);
    sel = null; activeEdit = -1; activeSticker = null;
    refreshAll();
  }
  $('undo').onclick = () => { if (!undoStack.length) return; redoStack.push(snapshot()); restore(undoStack.pop()); syncHistory(); saveDraft(); };
  $('redo').onclick = () => { if (!redoStack.length) return; undoStack.push(snapshot()); restore(redoStack.pop()); syncHistory(); saveDraft(); };

  // 滑块：拖动一次只记一步撤销
  function sliderSession(input, onInput) {
    let started = false;
    input.addEventListener('input', () => { if (!started) { started = onInput.begin ? onInput.begin() : true; } onInput(); });
    input.addEventListener('change', () => { started = false; saveDraft(); });
  }

  let draftTimer = 0;
  function saveDraft() {
    clearTimeout(draftTimer);
    draftTimer = setTimeout(() => store.set(`draft:${member.id}`, look), 250);
  }

  // ---------- 视图 ----------
  const stage = $('stage'), world = $('world');
  function fitScale() {
    const r = stage.getBoundingClientRect();
    return Math.min(r.width / W, (r.height - 8) / H);
  }
  function applyView() {
    const s = fitScale() * view.zoom;
    world.style.transform = `translate(-50%,-50%) translate(${view.tx}px,${view.ty}px) scale(${s})`;
    $('fit').hidden = view.zoom <= 1.02 && Math.abs(view.tx) < 2 && Math.abs(view.ty) < 2;
    document.documentElement.style.setProperty('--inv', String(1 / s));
  }
  window.addEventListener('resize', applyView);
  $('fit').onclick = () => { view = { zoom: 1, tx: 0, ty: 0 }; applyView(); };

  function toImage(cx, cy) {
    const r = world.getBoundingClientRect();
    return [(cx - r.left) / r.width * W, (cy - r.top) / r.height * H];
  }

  // ---------- 舞台手势 ----------
  const pointers = new Map();
  let gesture = null;
  const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
  const ang = (a, b) => Math.atan2(b.y - a.y, b.x - a.x) * 180 / Math.PI;
  const mid = (a, b) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });

  stage.addEventListener('pointerdown', e => {
    if (e.target.closest('button')) return;
    try { stage.setPointerCapture(e.pointerId); } catch (_) {}
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.size === 1) {
      const el = e.target.closest('.sticker');
      if (el && tab === 'sticker') {
        selectSticker(el.dataset.id);
        const st = stickerById(el.dataset.id);
        gesture = { type: 'sticker', st, start: { x: e.clientX, y: e.clientY }, orig: { ...st }, pushed: false };
      } else {
        gesture = { type: 'tap', start: { x: e.clientX, y: e.clientY }, view: { ...view }, moved: false };
      }
    } else if (pointers.size === 2) {
      const [a, b] = [...pointers.values()];
      const base = { d: dist(a, b), a: ang(a, b), m: mid(a, b) };
      if (gesture && gesture.type === 'sticker') {
        gesture = { type: 'sticker2', st: gesture.st, base, orig: { ...gesture.st }, pushed: gesture.pushed };
      } else {
        gesture = { type: 'pinch', base, view: { ...view } };
      }
    }
  });

  stage.addEventListener('pointermove', e => {
    if (!pointers.has(e.pointerId) || !gesture) return;
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    const s = fitScale() * view.zoom;
    if (gesture.type === 'tap') {
      const dx = e.clientX - gesture.start.x, dy = e.clientY - gesture.start.y;
      if (!gesture.moved && Math.hypot(dx, dy) > 8) gesture.moved = true;
      if (gesture.moved && view.zoom > 1.02) { view.tx = gesture.view.tx + dx; view.ty = gesture.view.ty + dy; applyView(); }
    } else if (gesture.type === 'sticker') {
      const dx = e.clientX - gesture.start.x, dy = e.clientY - gesture.start.y;
      if (Math.hypot(dx, dy) < 3) return;
      if (!gesture.pushed) { pushHistory(); gesture.pushed = true; }
      gesture.st.x = gesture.orig.x + dx / s; gesture.st.y = gesture.orig.y + dy / s;
      placeSticker(gesture.st);
    } else if (gesture.type === 'sticker2' || gesture.type === 'pinch') {
      const [a, b] = [...pointers.values()];
      if (!b) return;
      const k = dist(a, b) / gesture.base.d, m = mid(a, b);
      if (gesture.type === 'sticker2') {
        if (!gesture.pushed) { pushHistory(); gesture.pushed = true; }
        gesture.st.size = Math.max(24, Math.min(600, gesture.orig.size * k));
        gesture.st.rot = norm(gesture.orig.rot + ang(a, b) - gesture.base.a);
        gesture.st.x = gesture.orig.x + (m.x - gesture.base.m.x) / s;
        gesture.st.y = gesture.orig.y + (m.y - gesture.base.m.y) / s;
        placeSticker(gesture.st); syncStickerEdit();
      } else {
        const v = gesture.view, r = stage.getBoundingClientRect();
        const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        const s0 = fitScale() * v.zoom, zoom = Math.max(1, Math.min(6, v.zoom * k)), s1 = fitScale() * zoom;
        const px = (gesture.base.m.x - cx - v.tx) / s0, py = (gesture.base.m.y - cy - v.ty) / s0;
        view = { zoom, tx: m.x - cx - px * s1, ty: m.y - cy - py * s1 };
        if (zoom <= 1.001) view = { zoom: 1, tx: 0, ty: 0 };
        applyView();
      }
    }
  });

  const endPointer = e => {
    if (!pointers.has(e.pointerId)) return;
    const wasSingle = pointers.size === 1;
    pointers.delete(e.pointerId);
    if (wasSingle && gesture && gesture.type === 'tap' && !gesture.moved && e.type === 'pointerup') handleTap(e.clientX, e.clientY);
    if (gesture && (gesture.type === 'sticker' || gesture.type === 'sticker2') && gesture.pushed && !pointers.size) saveDraft();
    if (!pointers.size) gesture = null;
    else if (gesture && gesture.type !== 'tap') gesture = { type: 'idle' };
  };
  stage.addEventListener('pointerup', endPointer);
  stage.addEventListener('pointercancel', endPointer);
  const norm = a => ((a + 540) % 360) - 180;

  function handleTap(cx, cy) {
    if (tab === 'sticker') { selectSticker(null); return; }
    if (tab !== 'color' && tab !== 'pattern') setTab('color');
    const [fx, fy] = toImage(cx, cy);
    const x = Math.round(fx), y = Math.round(fy);
    if (x < 0 || y < 0 || x >= W || y >= H || A[y * W + x] < 40) { clearSelection(); hint('点角色身上任意一块衣服或头发'); toast('点到角色身上才能选哦'); return; }
    const i = y * W + x;
    // 点到已改过的地方：直接接着改那一处
    for (let k = look.edits.length - 1; k >= 0; k--) {
      const e = look.edits[k], m = computeMask(e.seed, e.tol, e.global, e.protect);
      if (m && m[i] === 255) {
        activeEdit = k; sel = { seed: e.seed, tol: e.tol, global: e.global, protect: e.protect };
        tol = e.tol; globalMode = e.global; protect = e.protect;
        syncControls(); drawSelection(); hint('选中了改过的地方，可以继续调'); return;
      }
    }
    if (protect && skin[i]) { toast('皮肤受保护，脸和身体不会被改'); return; }
    sel = { seed: [x, y], tol, global: globalMode, protect };
    activeEdit = -1;
    if (!computeMask(sel.seed, tol, globalMode, protect)) { sel = null; toast('这里选不中，换个地方试试'); return; }
    drawSelection(); syncControls();
    hint(tab === 'pattern' ? '选好了，挑个图案吧' : '选好了，挑个颜色吧');
  }

  function clearSelection() { sel = null; activeEdit = -1; drawSelection(); syncControls(); }

  // 对选中区域上色：第一次新建一处改动，之后都改同一处
  function ensureEdit(patch) {
    if (!sel) { toast('先点角色身上一块地方'); return false; }
    pushHistory();
    if (activeEdit < 0) {
      look.edits.push({ seed: sel.seed, tol: sel.tol, global: sel.global, protect: sel.protect, color: lastColor, color2: lastColor2, light: 0, pattern: '' });
      activeEdit = look.edits.length - 1;
    }
    Object.assign(look.edits[activeEdit], patch);
    render(); syncControls(); saveDraft();
    return true;
  }

  // ---------- 配饰 ----------
  const stickerById = id => look.stickers.find(s => s.id === id);
  let stickerSeq = 0;
  function stickerEl(st) {
    let el = document.querySelector(`.sticker[data-id="${st.id}"]`);
    if (!el) {
      el = document.createElement('div');
      el.className = 'sticker'; el.dataset.id = st.id;
      el.innerHTML = '<img alt="" draggable="false">';
    }
    return el;
  }
  function placeSticker(st) {
    const el = stickerEl(st);
    const layer = st.back ? $('back-layer') : $('front-layer');
    if (el.parentNode !== layer) layer.appendChild(el);
    const img = el.firstChild, src = DressStickers.url(st.type, st.color);
    if (img.getAttribute('src') !== src) img.setAttribute('src', src);
    el.style.width = el.style.height = `${st.size}px`;
    el.style.transform = `translate(${st.x - st.size / 2}px,${st.y - st.size / 2}px) rotate(${st.rot}deg) scaleX(${st.flip ? -1 : 1})`;
    el.classList.toggle('active', st.id === activeSticker);
  }
  function renderStickers() {
    const ids = new Set(look.stickers.map(s => s.id));
    document.querySelectorAll('.sticker').forEach(el => { if (!ids.has(el.dataset.id)) el.remove(); });
    look.stickers.forEach(placeSticker);
    syncStickerEdit();
  }
  function selectSticker(id) {
    activeSticker = id;
    document.querySelectorAll('.sticker').forEach(el => el.classList.toggle('active', el.dataset.id === id));
    syncStickerEdit();
  }
  function addSticker(type) {
    const def = DressStickers.get(type);
    const [hx, hy] = member.head;
    const pos = { top: [hx, hy - 62], face: [hx, hy], neck: [hx, hy + 70], back: [hx, hy + 150], free: [hx + 90 - Math.random() * 180, hy + 40 + Math.random() * 200] }[def.anchor];
    pushHistory();
    const st = { id: `s${Date.now().toString(36)}${stickerSeq++}`, type, color: def.color, size: def.size, rot: 0, flip: false, back: !!def.back,
      x: Math.max(30, Math.min(W - 30, pos[0])), y: Math.max(30, Math.min(H - 30, pos[1])) };
    look.stickers.push(st);
    activeSticker = st.id;
    renderStickers(); saveDraft();
    toast(`戴上了${def.name}，拖动调整位置`);
  }
  function syncStickerEdit() {
    const st = activeSticker && stickerById(activeSticker);
    $('sticker-edit').hidden = !st;
    if (!st) return;
    $('st-size').value = Math.round(st.size);
    $('st-rot').value = Math.round(st.rot);
    $('st-depth').textContent = st.back ? '放到身前' : '放到身后';
    markSwatch($('sticker-swatches'), st.color);
  }

  // ---------- 界面 ----------
  function swatchRow(el, onPick, withCustom = true) {
    el.innerHTML = '';
    PALETTE.forEach(c => {
      const b = document.createElement('button');
      b.className = 'sw'; b.style.setProperty('--c', c); b.dataset.c = c; b.setAttribute('aria-label', c);
      b.onclick = () => onPick(c);
      el.appendChild(b);
    });
    if (withCustom) {
      const lab = document.createElement('label');
      lab.className = 'sw custom'; lab.title = '自选颜色';
      lab.innerHTML = '<input type="color" value="#19f0ff" aria-label="自选颜色">';
      const inp = lab.firstChild;
      let begun = false;
      inp.addEventListener('input', () => onPick(inp.value, begun ? 'live' : (begun = true, 'first')));
      inp.addEventListener('change', () => { begun = false; });
      el.appendChild(lab);
    }
  }
  function markSwatch(el, c) {
    el.querySelectorAll('.sw').forEach(b => b.classList.toggle('on', b.dataset.c === c));
  }

  swatchRow($('color-swatches'), (c, phase) => {
    lastColor = c;
    if (phase === 'live' && activeEdit >= 0) { look.edits[activeEdit].color = c; render(); saveDraft(); return; }
    if (ensureEdit({ color: c })) hint('不满意可以换颜色，或调深浅');
  });
  swatchRow($('pattern-swatches'), (c, phase) => {
    lastColor2 = c;
    if (phase === 'live' && activeEdit >= 0) { look.edits[activeEdit].color2 = c; render(); saveDraft(); return; }
    if (sel) ensureEdit({ color2: c }); else markSwatch($('pattern-swatches'), c);
  });
  swatchRow($('sticker-swatches'), (c, phase) => {
    const st = activeSticker && stickerById(activeSticker); if (!st) return;
    if (phase !== 'live') pushHistory();
    st.color = c; placeSticker(st); markSwatch($('sticker-swatches'), c); saveDraft();
  });

  PATTERNS.forEach(p => {
    const b = document.createElement('button');
    b.className = 'pat'; b.dataset.p = p.id;
    b.innerHTML = `<i class="pat-${p.id || 'solid'}"></i><span>${p.name}</span>`;
    b.onclick = () => { if (ensureEdit({ pattern: p.id })) hint('换个图案颜色试试'); };
    $('patterns').appendChild(b);
  });

  DressStickers.list.forEach(def => {
    const b = document.createElement('button');
    b.className = 'st-pick';
    b.innerHTML = `<img src="${DressStickers.url(def.id, def.color)}" alt=""><span>${def.name}</span>`;
    b.onclick = () => addSticker(def.id);
    $('sticker-list').appendChild(b);
  });

  function optionRow(el, items, key) {
    el.innerHTML = '';
    items.forEach(it => {
      const b = document.createElement('button');
      b.className = 'opt'; b.dataset.v = it.id; b.textContent = it.name;
      if (key === 'glow' && it.id !== 'none') b.style.setProperty('--c', it.id);
      b.onclick = () => { pushHistory(); look.stage[key] = it.id; applyStage(); saveDraft(); };
      el.appendChild(b);
    });
  }
  optionRow($('bg-list'), BGS, 'bg');
  optionRow($('light-list'), LIGHTS, 'light');
  optionRow($('glow-list'), GLOWS, 'glow');

  function applyStage() {
    const st = look.stage;
    const bg = BGS.find(b => b.id === st.bg) || BGS[0];
    $('bg').style.background = bg.img ? `#07070d url("${bg.img}") center/cover no-repeat` : bg.css;
    const li = LIGHTS.find(l => l.id === st.light) || LIGHTS[0];
    const light = $('light');
    light.style.display = li.blend ? 'block' : 'none';
    if (li.blend) {
      light.style.mixBlendMode = li.blend;
      light.style.opacity = li.alpha;
      light.style.background = li.grad ? `linear-gradient(160deg,${li.grad[0]},${li.grad[1]})` : li.color;
    }
    charCanvas.style.filter = st.glow !== 'none' ? `drop-shadow(0 0 3px ${st.glow}) drop-shadow(0 0 10px ${st.glow})` : '';
    [['bg-list', st.bg], ['light-list', st.light], ['glow-list', st.glow]].forEach(([id, v]) =>
      $(id).querySelectorAll('.opt').forEach(b => b.classList.toggle('on', b.dataset.v === v)));
  }

  function syncControls() {
    const e = activeEdit >= 0 ? look.edits[activeEdit] : null;
    $('tol').value = tol; $('tol-out').textContent = tol;
    $('light-amt').value = e ? e.light : 0; $('light-out').textContent = e ? e.light : 0;
    $('protect').checked = protect;
    $('mode-seg').querySelectorAll('button').forEach(b => b.classList.toggle('on', (b.dataset.mode === 'all') === globalMode));
    markSwatch($('color-swatches'), e ? e.color : '');
    markSwatch($('pattern-swatches'), e ? e.color2 : lastColor2);
    $('patterns').querySelectorAll('.pat').forEach(b => b.classList.toggle('on', !!e && b.dataset.p === (e.pattern || '')));
    $('del-edit').disabled = !e;
    $('clear-sel').disabled = !sel;
    $('pattern-tip').textContent = sel ? (e ? '给这一块挑个图案' : '先选颜色或直接选图案') : '先点角色身上一块，再选图案';
  }

  sliderSession($('light-amt'), Object.assign(() => {
    const v = +$('light-amt').value; $('light-out').textContent = v;
    if (activeEdit >= 0) { look.edits[activeEdit].light = v; render(); }
  }, { begin: () => { if (activeEdit >= 0) pushHistory(); return true; } }));

  sliderSession($('tol'), Object.assign(() => {
    tol = +$('tol').value; $('tol-out').textContent = tol;
    if (sel) {
      sel.tol = tol;
      if (activeEdit >= 0) { look.edits[activeEdit].tol = tol; render(); } else drawSelection();
    }
  }, { begin: () => { if (activeEdit >= 0) pushHistory(); return true; } }));

  $('mode-seg').addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return;
    globalMode = b.dataset.mode === 'all';
    if (sel) {
      if (activeEdit >= 0) { pushHistory(); look.edits[activeEdit].global = globalMode; render(); saveDraft(); }
      sel.global = globalMode; drawSelection();
    }
    syncControls();
  });
  $('protect').onchange = () => {
    protect = $('protect').checked;
    if (sel) {
      if (activeEdit >= 0) { pushHistory(); look.edits[activeEdit].protect = protect; render(); saveDraft(); }
      sel.protect = protect; drawSelection();
    }
    if (!protect) toast('已关闭皮肤保护，小心改到脸哦');
  };
  $('clear-sel').onclick = () => { clearSelection(); hint('点角色身上任意一块衣服或头发'); };
  $('del-edit').onclick = () => {
    if (activeEdit < 0) return;
    pushHistory(); look.edits.splice(activeEdit, 1); clearSelection(); render(); saveDraft();
  };

  $('st-size').addEventListener('input', () => { const st = stickerById(activeSticker); if (!st) return; st.size = +$('st-size').value; placeSticker(st); });
  $('st-rot').addEventListener('input', () => { const st = stickerById(activeSticker); if (!st) return; st.rot = +$('st-rot').value; placeSticker(st); });
  ['st-size', 'st-rot'].forEach(id => {
    $(id).addEventListener('pointerdown', () => { if (activeSticker) pushHistory(); });
    $(id).addEventListener('change', saveDraft);
  });
  $('st-flip').onclick = () => { const st = stickerById(activeSticker); if (!st) return; pushHistory(); st.flip = !st.flip; placeSticker(st); saveDraft(); };
  $('st-depth').onclick = () => { const st = stickerById(activeSticker); if (!st) return; pushHistory(); st.back = !st.back; placeSticker(st); syncStickerEdit(); saveDraft(); };
  $('st-del').onclick = () => {
    if (!activeSticker) return;
    pushHistory(); look.stickers = look.stickers.filter(s => s.id !== activeSticker); activeSticker = null; renderStickers(); saveDraft();
  };

  // 按住看原图
  const peek = on => { peeking = on; document.body.classList.toggle('peeking', on); renderNow(); };
  $('peek').addEventListener('pointerdown', e => { e.preventDefault(); peek(true); });
  ['pointerup', 'pointerleave', 'pointercancel'].forEach(t => $('peek').addEventListener(t, () => peeking && peek(false)));
  $('peek').addEventListener('contextmenu', e => e.preventDefault());

  // 标签页
  function setTab(t) {
    tab = t; document.body.dataset.tab = t;
    document.querySelectorAll('.tabs button').forEach(b => b.setAttribute('aria-selected', String(b.dataset.tab === t)));
    document.querySelectorAll('.pane').forEach(p => { p.hidden = p.dataset.pane !== t; });
    if (t === 'sticker') { sel && (sel = null, activeEdit = -1, drawSelection()); hint('选一个配饰戴上'); }
    else if (t === 'color' || t === 'pattern') hint(sel ? '可以继续调这一块' : '点角色身上任意一块衣服或头发');
    else if (t === 'stage') hint('换个舞台和灯光');
    else { hint('保存造型或导出图片'); renderSlots(); }
    if (t !== 'sticker') selectSticker(null);
    syncControls();
  }
  document.querySelector('.tabs').addEventListener('click', e => { const b = e.target.closest('button'); if (b) setTab(b.dataset.tab); });

  // 成员
  function renderMembers() {
    const box = $('members'); box.innerHTML = '';
    MEMBERS.forEach(m => {
      const b = document.createElement('button');
      b.className = 'mem'; b.setAttribute('role', 'option'); b.dataset.id = m.id;
      b.setAttribute('aria-selected', String(m.id === member.id));
      const av = 44, bw = av * 4.4, bh = bw * H / W;
      const ox = av / 2 - m.head[0] / W * bw, oy = av / 2 - m.head[1] / H * bh;
      b.innerHTML = `<i style="background-image:url('${m.src}');background-size:${bw}px ${bh}px;background-position:${ox.toFixed(1)}px ${oy.toFixed(1)}px"></i><span>${m.name}</span>`;
      b.onclick = () => switchMember(m.id);
      box.appendChild(b);
    });
  }

  let loadToken = 0;
  async function switchMember(id, initial) {
    const m = MEMBERS.find(x => x.id === id) || MEMBERS[0];
    if (!initial) store.set(`draft:${member.id}`, look);
    member = m; store.set('member', m.id);
    const token = ++loadToken;
    document.body.classList.add('loading');
    $('members').querySelectorAll('.mem').forEach(b => b.setAttribute('aria-selected', String(b.dataset.id === id)));
    $('name-tag').textContent = m.name;
    let im;
    try { im = await loadImage(m.src); } catch (_) { toast('立绘加载失败，请检查网络'); document.body.classList.remove('loading'); return; }
    if (token !== loadToken) return;
    analyse(im);
    look = Object.assign(blankLook(), store.get(`draft:${m.id}`, null) || {});
    undoStack = []; redoStack = []; syncHistory();
    sel = null; activeEdit = -1; activeSticker = null;
    view = { zoom: 1, tx: 0, ty: 0 }; applyView();
    refreshAll();
    document.body.classList.remove('loading');
  }

  function refreshAll() {
    renderNow(); renderStickers(); applyStage(); syncControls();
    $('look-name').value = look.name || '';
  }

  // ---------- 随机造型 ----------
  $('dice').onclick = () => {
    if (!orig) return;
    pushHistory();
    const pick = a => a[(Math.random() * a.length) | 0];
    const edits = [], taken = new Uint8Array(N);
    for (let tries = 0; tries < 500 && edits.length < 3; tries++) {
      const x = 40 + ((Math.random() * (W - 80)) | 0), y = 40 + ((Math.random() * (H - 80)) | 0), i = y * W + x;
      if (A[i] < 220 || skin[i] || taken[i]) continue;
      const m = computeMask([x, y], 40, false, true);
      if (!m) continue;
      let n = 0, overlap = 0;
      for (let k = 0; k < N; k++) if (m[k] === 255) { n++; if (taken[k]) overlap++; }
      if (n < 1500 || n > 90000 || overlap > n * .2) continue;
      for (let k = 0; k < N; k++) if (m[k] === 255) taken[k] = 1;
      const pat = Math.random() < .3 ? pick(PATTERNS.slice(1)).id : '';
      edits.push({ seed: [x, y], tol: 40, global: false, protect: true, color: pick(PALETTE.slice(0, 12)), color2: pick(PALETTE), light: 0, pattern: pat });
    }
    look.edits = edits;
    look.stage.bg = pick(BGS).id; look.stage.light = pick(LIGHTS).id; look.stage.glow = Math.random() < .5 ? 'none' : pick(GLOWS.slice(1)).id;
    sel = null; activeEdit = -1; activeSticker = null;
    refreshAll(); saveDraft();
    toast(edits.length ? `随机改了 ${edits.length} 处，不喜欢就撤销` : '这次没摇到，再试一次');
  };

  // ---------- 导出与造型栏 ----------
  async function compose(scale, mark = true) {
    const c = document.createElement('canvas');
    c.width = W * scale; c.height = H * scale;
    const x = c.getContext('2d');
    const st = look.stage, bg = BGS.find(b => b.id === st.bg) || BGS[0];
    if (bg.img) {
      x.fillStyle = '#07070d'; x.fillRect(0, 0, c.width, c.height);
      try {
        const im = await loadImage(bg.img), k = Math.max(c.width / im.width, c.height / im.height);
        x.drawImage(im, (c.width - im.width * k) / 2, (c.height - im.height * k) / 2, im.width * k, im.height * k);
      } catch (_) {}
    } else {
      const g = x.createLinearGradient(0, 0, 0, c.height);
      bg.stops.forEach(([col, p]) => g.addColorStop(p, col));
      x.fillStyle = g; x.fillRect(0, 0, c.width, c.height);
    }
    const drawStickers = async back => {
      for (const s of look.stickers.filter(s => !!s.back === back)) {
        try {
          const im = await loadImage(DressStickers.url(s.type, s.color));
          x.save(); x.translate(s.x * scale, s.y * scale); x.rotate(s.rot * Math.PI / 180); x.scale(s.flip ? -1 : 1, 1);
          x.drawImage(im, -s.size * scale / 2, -s.size * scale / 2, s.size * scale, s.size * scale); x.restore();
        } catch (_) {}
      }
    };
    await drawStickers(true);
    if (st.glow !== 'none') {
      x.save(); x.shadowColor = st.glow; x.shadowBlur = 12 * scale;
      x.drawImage(charCanvas, 0, 0, c.width, c.height); x.restore();
    }
    x.drawImage(charCanvas, 0, 0, c.width, c.height);
    await drawStickers(false);
    const li = LIGHTS.find(l => l.id === st.light);
    if (li && li.blend) {
      x.save(); x.globalCompositeOperation = li.blend; x.globalAlpha = li.alpha;
      if (li.grad) { const g = x.createLinearGradient(0, 0, c.width, c.height); g.addColorStop(0, li.grad[0]); g.addColorStop(1, li.grad[1]); x.fillStyle = g; }
      else x.fillStyle = li.color;
      x.fillRect(0, 0, c.width, c.height); x.restore();
    }
    if (mark) {
      const fs = 13 * scale;
      x.font = `600 ${fs}px "PingFang SC","Microsoft YaHei",sans-serif`;
      x.textBaseline = 'bottom';
      const text = `${member.name}${look.name ? ` ·「${look.name}」` : ''}`;
      x.fillStyle = 'rgba(0,0,0,.45)'; x.fillRect(0, c.height - fs * 2.4, c.width, fs * 2.4);
      x.fillStyle = '#fff'; x.textAlign = 'left'; x.fillText(text, fs, c.height - fs * .7);
      x.fillStyle = 'rgba(255,255,255,.75)'; x.textAlign = 'right'; x.fillText('CHO-SIREN 造型工坊 · 玩家创作', c.width - fs, c.height - fs * .7);
    }
    return c;
  }

  $('export').onclick = async () => {
    const c = await compose(2);
    const url = c.toDataURL('image/png');
    $('sheet-img').src = url; $('sheet-dl').href = url;
    $('sheet-dl').download = `cho-siren-${member.id}${look.name ? `-${look.name}` : ''}.png`;
    $('sheet').hidden = false;
  };
  $('sheet-close').onclick = () => { $('sheet').hidden = true; };
  $('sheet').onclick = e => { if (e.target === $('sheet')) $('sheet').hidden = true; };

  $('look-name').addEventListener('input', () => { look.name = $('look-name').value.trim(); saveDraft(); });

  function renderSlots() {
    const slots = store.get('slots', [null, null, null, null]);
    const box = $('slots'); box.innerHTML = '';
    slots.forEach((s, i) => {
      const d = document.createElement('div');
      d.className = `slot${s ? ' filled' : ''}`;
      const m = s && MEMBERS.find(x => x.id === s.member);
      d.innerHTML = s
        ? `<img src="${s.thumb}" alt=""><b>${m ? m.name : ''}</b><small>${s.look.name || `造型 ${i + 1}`}</small><div class="slot-btns"><button data-act="load">穿上</button><button data-act="save">覆盖</button></div>`
        : `<div class="slot-empty">空栏位 ${i + 1}</div><div class="slot-btns"><button data-act="save">保存到这里</button></div>`;
      d.addEventListener('click', async e => {
        const act = e.target.closest('button')?.dataset.act;
        if (act === 'save') {
          const thumb = (await compose(.45, false)).toDataURL('image/jpeg', .78);
          slots[i] = { member: member.id, look: JSON.parse(snapshot()), thumb };
          if (store.set('slots', slots)) toast('已保存'); else toast('存储空间不足，保存失败');
          renderSlots();
        } else if (act === 'load') {
          if (s.member !== member.id) await switchMember(s.member);
          pushHistory(); look = JSON.parse(JSON.stringify(s.look));
          sel = null; activeEdit = -1; activeSticker = null; refreshAll(); saveDraft();
          toast('已换上这套造型');
        }
      });
      box.appendChild(d);
    });
  }

  $('reset').onclick = () => {
    if (!look.edits.length && !look.stickers.length) { toast('已经是原样啦'); return; }
    pushHistory(); look = blankLook(); sel = null; activeEdit = -1; activeSticker = null; refreshAll(); saveDraft();
    toast('已重置，可以撤销');
  };

  // ---------- 提示 ----------
  let toastTimer = 0;
  function toast(msg) {
    const t = $('toast'); t.textContent = msg; t.classList.add('show');
    clearTimeout(toastTimer); toastTimer = setTimeout(() => t.classList.remove('show'), 1800);
  }
  function hint(msg) { $('hint').textContent = msg; }

  // ---------- 启动 ----------
  renderMembers();
  applyView();
  switchMember(store.get('member', MEMBERS[0].id), true);
  window.DressLab = { get look() { return look; }, get member() { return member.id; }, render: renderNow, mask: computeMask, switchMember };
})();

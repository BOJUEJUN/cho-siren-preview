(() => {
  const stage = document.querySelector('#home-stage');
  const shell = document.querySelector('#game-shell');
  const input = document.querySelector('#unity-canvas');
  const atmosphere = document.querySelector('#stage-atmosphere');
  const sparks = document.querySelector('#stage-sparks');
  const atmosphereContext = atmosphere.getContext('2d');
  const sparksContext = sparks.getContext('2d');
  const beatMeter = document.querySelector('#stage-beat-meter');
  for (let i = 0; i < 9; i++) {
    const bar = document.createElement('i');
    bar.style.setProperty('--i', String(i));
    beatMeter.appendChild(bar);
  }
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const particles = Array.from({ length: 24 }, () => ({
    x: Math.random(), y: Math.random(), v: .5 + Math.random(), s: Math.random(), n: Math.random()
  }));
  const fract = value => value - Math.floor(value);
  let active = false, uiHidden = false, time = 0, lastFrame = 0, frameId = 0, width = 0, height = 0;
  let bursts = [], rings = [];
  let pointer = { x: .5, y: .45, inside: false };
  let smoothed = { x: .5, y: .45 };

  function resize() {
    const box = shell.getBoundingClientRect();
    width = box.width;
    height = box.height;
    const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
    for (const [surface, context] of [[atmosphere, atmosphereContext], [sparks, sparksContext]]) {
      surface.width = Math.max(1, Math.round(width * dpr));
      surface.height = Math.max(1, Math.round(height * dpr));
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
  }

  function emit(x, y, count, big, color = '#c5ff79') {
    if (reducedMotion.matches) return;
    for (let i = 0; i < count; i++) {
      const angle = i / count * Math.PI * 2 + Math.random() * .4;
      const speed = (big ? .25 : .12) * (Math.random() * .7 + .3);
      bursts.push({ x, y, vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed * .5,
        age: 0, life: big ? 1.9 : 1.05, size: (big ? 2 : 1) * (1 + Math.random() * 2),
        angle, white: Math.random() > .75, color });
    }
    if (bursts.length > 260) bursts.splice(0, bursts.length - 260);
  }

  function star(context, x, y, radius, alpha, color) {
    context.globalAlpha = alpha;
    context.fillStyle = color;
    context.beginPath();
    context.moveTo(x, y - radius);
    context.quadraticCurveTo(x + radius * .15, y - radius * .15, x + radius, y);
    context.quadraticCurveTo(x + radius * .15, y + radius * .15, x, y + radius);
    context.quadraticCurveTo(x - radius * .15, y + radius * .15, x - radius, y);
    context.quadraticCurveTo(x - radius * .15, y - radius * .15, x, y - radius);
    context.fill();
  }

  function drawAtmosphere() {
    const context = atmosphereContext, w = width, h = height;
    context.clearRect(0, 0, w, h);
    if (reducedMotion.matches) return;
    context.save();
    const beat = Math.pow(Math.max(0, Math.sin(time * Math.PI * 2 / 1.12)), 9);
    const glow = context.createRadialGradient(w * .78, h * .71, 0, w * .78, h * .71, w * .6);
    glow.addColorStop(0, `rgba(197,255,121,${.075 * beat})`);
    glow.addColorStop(1, 'rgba(197,255,121,0)');
    context.fillStyle = glow;
    context.fillRect(0, 0, w, h);
    for (const particle of particles) {
      const x = (particle.x < .5 ? particle.x * .35 : .85 + (particle.x - .5) * .3) * w;
      const y = (1 - fract(particle.y + time * .017 * particle.v)) * h;
      star(context, x, y, 1 + particle.s * 2.5, .14 + particle.s * .25,
        particle.n > .5 ? '#e6ffd0' : '#c8a4ff');
    }
    for (let i = 0; i < 3; i++) {
      const phase = fract(time * .44 + i / 3);
      context.globalAlpha = (1 - phase) * .16;
      context.strokeStyle = '#c5ff79';
      context.lineWidth = .7;
      context.beginPath();
      context.ellipse(w * .73, h * .65, w * (.1 + phase * .38),
        h * (.05 + phase * .12), -.4, 0, Math.PI * 2);
      context.stroke();
    }
    context.restore();
  }

  function drawSparks(dt) {
    const context = sparksContext;
    context.clearRect(0, 0, width, height);
    context.save();
    for (const particle of bursts) {
      particle.age += dt;
      particle.x += particle.vx * dt;
      particle.y += particle.vy * dt;
      const alpha = Math.max(0, 1 - particle.age / particle.life);
      context.save();
      context.translate(particle.x * width, particle.y * height);
      context.rotate(particle.angle + particle.age);
      context.globalAlpha = alpha;
      context.fillStyle = particle.white ? '#fff' : particle.color;
      context.fillRect(-particle.size / 2, -particle.size * 2,
        particle.size, particle.size * 4);
      context.restore();
    }
    bursts = bursts.filter(particle => particle.age < particle.life);
    for (const ring of rings) {
      ring.age += dt;
      const progress = Math.min(1, ring.age / ring.life);
      context.globalAlpha = (1 - progress) ** 2 * .65;
      context.strokeStyle = ring.color;
      context.lineWidth = (ring.big ? 3.4 : 2) * (1 - progress * .55);
      context.beginPath();
      context.ellipse(ring.x * width, ring.y * height,
        (ring.big ? 14 : 8) + progress * (ring.big ? 135 : 65),
        (ring.big ? 8 : 5) + progress * (ring.big ? 70 : 32),
        -.25 + Math.sin(time * 7) * .08, 0, Math.PI * 2);
      context.stroke();
    }
    rings = rings.filter(ring => ring.age < ring.life);
    context.restore();
  }

  function frame(stamp) {
    if (!active) return;
    const dt = Math.min((stamp - (lastFrame || stamp)) / 1000, .04);
    lastFrame = stamp;
    if (!document.hidden) {
      time += dt;
      smoothed.x += ((pointer.inside ? pointer.x : .5) - smoothed.x) * .065;
      smoothed.y += ((pointer.inside ? pointer.y : .45) - smoothed.y) * .065;
      stage.style.setProperty('--stage-pointer-x', `${(smoothed.x - .5) * 6}px`);
      stage.style.setProperty('--stage-pointer-y', `${(smoothed.y - .45) * 4}px`);
      drawAtmosphere();
      drawSparks(dt);
    }
    frameId = requestAnimationFrame(frame);
  }

  function setActive(value) {
    if (active === value) return;
    active = value;
    stage.classList.toggle('stage-on', active);
    if (active) {
      time = 0;
      lastFrame = 0;
      pointer = { x: .5, y: .45, inside: false };
      smoothed = { x: .5, y: .45 };
      resize();
      emit(.5, .45, 18, false);
      frameId = requestAnimationFrame(frame);
    } else {
      window.choSirenCharacter?.close();
      setUiHidden(false);
      cancelAnimationFrame(frameId);
      bursts = [];
      rings = [];
      atmosphereContext.clearRect(0, 0, width, height);
      sparksContext.clearRect(0, 0, width, height);
    }
  }

  function setUiHidden(value) {
    uiHidden = value;
    stage.classList.toggle('ui-hidden', uiHidden);
  }

  input.addEventListener('pointermove', event => {
    if (!active) return;
    const box = input.getBoundingClientRect();
    pointer = { x: (event.clientX - box.left) / box.width,
      y: (event.clientY - box.top) / box.height, inside: true };
  }, { passive: true });
  input.addEventListener('pointerleave', () => { pointer.inside = false; }, { passive: true });
  // Only an actual Unity UI action can create a click burst. Character taps,
  // empty space and double-click UI gestures never enter this path.
  function playClick(action, x, y) {
    if (!active || uiHidden) return;
    const album = action === 'AlbumProduction';
    const show = action === 'LiveOnStage';
    const color = album ? '#c5ff43' : show ? '#bca4ff' : '#e0ffcd';
    emit(x, y, show ? 100 : album ? 68 : 24, show || album, color);
    rings.push({ x, y, age: 0, life: show ? .8 : .58, color, big: show || album });
  }
  window.addEventListener('resize', resize);
  new ResizeObserver(resize).observe(shell);
  resize();
  window.choSirenStage = { setActive, setUiHidden, playClick,
    get active() { return active; }, get uiHidden() { return uiHidden; } };
})();

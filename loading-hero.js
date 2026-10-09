// Brings the title-screen character to life. It plays the lobby's idle loop (the same
// green-screen file and ?v= token, so the lobby later reads it from the asset cache)
// and keys it on the GPU, drawing the white cut-in outline in the shader instead of
// with CSS filters, which would be re-rendered on every frame. The still image stays
// until the first frame is drawn, and wherever video, WebGL2 or motion is unavailable.
(() => {
  const hero = document.querySelector('.ld-hero');
  const still = hero?.querySelector('.ld-hero-main');
  const loading = document.querySelector('#loading');
  const moment = document.querySelector('#character-moment');
  const clip = moment?.dataset?.idleClip;
  if (!hero || !still || !loading || !clip || moment.dataset.clipFormat !== 'green') return;
  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;

  // The figure's box inside the 1080×1920 clip, as cropped for loading-hero.webp,
  // plus room around it for the outline.
  const PAD = 12;
  const CROP = { x: 67 - PAD, y: 40 - PAD, w: 948 + 2 * PAD, h: 1860 + 2 * PAD };
  // Canvas pixels per clip pixel: the screen's own density, capped so the per-pixel
  // outline stays cheap while Unity is still loading underneath.
  const box = hero.getBoundingClientRect();
  const SCALE = Math.min(0.75, Math.max(0.4, box.height * (window.devicePixelRatio || 1) / 1860));
  const OUTLINE = 8;            // outline width in clip pixels (about 3 CSS px on a phone)

  const canvas = document.createElement('canvas');
  canvas.className = 'ld-hero-live';
  canvas.width = Math.round(CROP.w * SCALE);
  canvas.height = Math.round(CROP.h * SCALE);
  canvas.style.left = `${-PAD / 948 * 100}%`;
  canvas.style.top = `${-PAD / 1860 * 100}%`;
  canvas.style.width = `${CROP.w / 948 * 100}%`;
  canvas.style.height = `${CROP.h / 1860 * 100}%`;
  const gl = canvas.getContext('webgl2', { premultipliedAlpha: true, antialias: false, alpha: true });
  if (!gl) return;

  const shader = (type, source) => {
    const object = gl.createShader(type);
    gl.shaderSource(object, source);
    gl.compileShader(object);
    if (!gl.getShaderParameter(object, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(object));
    return object;
  };
  let program;
  try {
    program = gl.createProgram();
    gl.attachShader(program, shader(gl.VERTEX_SHADER, `#version 300 es
void main() {
  gl_Position = vec4(gl_VertexID == 1 ? 3.0 : -1.0, gl_VertexID == 2 ? 3.0 : -1.0, 0.0, 1.0);
}`));
    gl.attachShader(program, shader(gl.FRAGMENT_SHADER, `#version 300 es
precision highp float;
uniform sampler2D uFrame;
uniform vec2 uSize;     // clip size in pixels
uniform vec4 uCrop;     // x, y, w, h of the drawn box in clip pixels
uniform vec2 uCanvas;   // canvas size in pixels
uniform float uOutline; // outline width in clip pixels
out vec4 outColor;
// Same key as the lobby: how far green rises above the other two channels.
float keyAt(vec2 v) {
  if (any(lessThan(v, vec2(0.0))) || any(greaterThan(v, uSize))) return 0.0;
  vec3 c = texture(uFrame, v / uSize).rgb;
  return 1.0 - smoothstep(0.10, 0.30, c.g - max(c.r, c.b));
}
void main() {
  vec2 p = vec2(gl_FragCoord.x, uCanvas.y - gl_FragCoord.y);
  vec2 v = uCrop.xy + p * (uCrop.zw / uCanvas);
  vec3 rgb = texture(uFrame, v / uSize).rgb;
  float alpha = keyAt(v);
  rgb.g = min(rgb.g, max(rgb.r, rgb.b) + 0.03);
  float ring = alpha;
  for (int i = 0; i < 12; i++) {
    float a = float(i) * 0.5235988;
    ring = max(ring, keyAt(v + vec2(cos(a), sin(a)) * uOutline));
  }
  // Character over a white outline, premultiplied.
  vec3 color = rgb * alpha + vec3(1.0) * ring * (1.0 - alpha);
  outColor = vec4(color, max(alpha, ring));
}`));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  } catch (_) { return; }

  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.useProgram(program);
  const at = name => gl.getUniformLocation(program, name);
  gl.uniform1i(at('uFrame'), 0);
  gl.uniform4f(at('uCrop'), CROP.x, CROP.y, CROP.w, CROP.h);
  gl.uniform2f(at('uCanvas'), canvas.width, canvas.height);
  gl.uniform1f(at('uOutline'), OUTLINE);
  gl.bindVertexArray(gl.createVertexArray());
  gl.viewport(0, 0, canvas.width, canvas.height);

  const video = document.createElement('video');
  video.muted = true;
  video.loop = true;
  video.playsInline = true;
  video.setAttribute('playsinline', '');
  video.setAttribute('webkit-playsinline', '');
  video.preload = 'auto';
  video.src = `media/${clip}.green.mp4?v=${moment.dataset.clipVersion || '1'}`;

  let stopped = false, shown = false, sized = false;
  function draw() {
    if (stopped || video.readyState < 2) return;
    if (!sized) {
      gl.uniform2f(at('uSize'), video.videoWidth, video.videoHeight);
      sized = true;
    }
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, video);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    if (!shown) {
      shown = true;
      hero.classList.add('is-live');
    }
  }
  const frames = typeof video.requestVideoFrameCallback === 'function';
  function onFrame() {
    if (stopped) return;
    draw();
    if (frames) video.requestVideoFrameCallback(onFrame);
    else window.requestAnimationFrame(onFrame);
  }
  function stop() {
    if (stopped) return;
    stopped = true;
    video.pause();
    video.removeAttribute('src');
    video.load();
    gl.getExtension('WEBGL_lose_context')?.loseContext();
  }

  hero.appendChild(canvas);
  video.play().then(() => {
    if (frames) video.requestVideoFrameCallback(onFrame);
    else window.requestAnimationFrame(onFrame);
  }).catch(stop);  // autoplay refused (e.g. iPhone Low Power Mode): keep the still
  // Done once the title screen has been dismissed.
  new MutationObserver(() => {
    if (loading.classList.contains('is-hidden')) window.setTimeout(stop, 1000);
  }).observe(loading, { attributes: true, attributeFilter: ['class'] });
})();

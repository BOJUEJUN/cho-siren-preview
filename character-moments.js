(() => {
  const root = document.querySelector('#character-moment');
  const clips = ['catalena-look', 'catalena-whisper', 'catalena-live'];
  // Safari and every iOS browser drop the alpha channel of VP9 WebM. They get a copy
  // packed side by side instead, [colour | PACKED_GAP px | alpha as luma], built by
  // scripts/build-packed-video.sh and recombined on the GPU in uploadPackedFrame():
  // VP9 WebM where WebM plays (the WebKit player that already played the plain clips
  // on iPhone), HEVC MP4 otherwise, and the next copy in line whenever one fails to
  // load. ?alphaVideo=webm|packed|packed-mp4 overrides the choice for testing.
  const PACKED_GAP = 32;
  const VARIANTS = {
    webm: { name: 'webm', packed: false, file: clip => `media/${clip}.webm?v=20260928-r10` },
    packedWebm: { name: 'packed-webm', packed: true, file: clip => `media/${clip}.packed.webm?v=20261006-r17g` },
    packedMp4: { name: 'packed-mp4', packed: true, file: clip => `media/${clip}.packed.mp4?v=20261005-r17a` }
  };
  const query = name => globalThis.location ? new URL(globalThis.location.href).searchParams.get(name) : null;
  const variants = (() => {
    const forced = query('alphaVideo');
    if (forced === 'webm') return [VARIANTS.webm];
    if (forced === 'packed-mp4') return [VARIANTS.packedMp4];
    if (forced === 'packed') return [VARIANTS.packedWebm, VARIANTS.packedMp4];
    const nav = globalThis.navigator || {};
    const ua = nav.userAgent || '';
    const appleTouch = /iP(hone|ad|od)/.test(ua) || (/Macintosh/.test(ua) && nav.maxTouchPoints > 1);
    const safari = /Version\/[\d.]+.*Safari\//.test(ua) && !/(Chrome|Chromium|CriOS|FxiOS|EdgiOS|Edg|OPR)\//.test(ua);
    if (!appleTouch && !safari) return [VARIANTS.webm];
    const probe = document.createElement('video');
    const webm = typeof probe.canPlayType === 'function' &&
      ['video/webm; codecs="vp9"', 'video/webm; codecs="vp09.00.10.08"'].some(type => probe.canPlayType(type) !== '');
    return webm ? [VARIANTS.packedWebm, VARIANTS.packedMp4] : [VARIANTS.packedMp4];
  })();
  // ?debug=clip prints each clip's lifecycle on screen, for checking a phone without devtools.
  const debug = (() => {
    if (query('debug') !== 'clip') return null;
    const panel = document.createElement('pre');
    panel.style.cssText = 'position:fixed;left:0;right:0;bottom:0;z-index:99;max-height:45%;overflow:hidden;' +
      'margin:0;padding:6px;font:11px/1.35 monospace;color:#fff;background:rgba(0,0,0,.75);pointer-events:none;white-space:pre-wrap';
    document.body.append(panel);
    const start = Date.now();
    return (...parts) => {
      panel.textContent = `${((Date.now() - start) / 1000).toFixed(1)}s ${parts.join(' ')}\n${panel.textContent}`.slice(0, 4000);
    };
  })();
  debug?.('clip copies:', variants.map(variant => variant.name).join(' > '), '|', (globalThis.navigator || {}).userAgent);
  let current = null, pending = null, sequence = 0;
  let uploadedVideo = null, uploadedTime = -1, uploadedTexture = null;
  // iOS can refuse play() outside a tap even for muted video (Low Power Mode, in-app
  // browsers), and Unity starts a clip a frame after the tap. A media element played
  // once during a tap stays allowed, so every tap unlocks a couple of spare elements
  // and each clip takes one of them.
  const SPARE_PLAYERS = 2;
  const spares = [];
  function unlockSpares() {
    while (spares.length < SPARE_PLAYERS) spares.push({ video: document.createElement('video'), unlocked: false });
    for (const spare of spares) {
      if (spare.unlocked) continue;
      spare.unlocked = true;
      spare.video.muted = true;
      spare.video.playsInline = true;
      try {
        spare.video.play()?.catch(() => {});
        spare.video.pause();
      } catch (_) {}
    }
  }
  for (const type of ['touchend', 'pointerup', 'click', 'keydown']) {
    document.addEventListener(type, unlockSpares, { capture: true, passive: true });
  }
  function player() {
    const index = spares.findIndex(spare => spare.unlocked);
    return index >= 0 ? spares.splice(index, 1)[0].video : document.createElement('video');
  }
  const dispose = item => {
    if (!item) return;
    clearTimeout(item.timeout);
    item.voice?.stop();
    item.voice = null;
    item.video.pause();
    item.video.removeAttribute('src');
    item.video.load();
    item.video.remove();
  };
  function status() {
    root.dataset.state = pending ? 'loading' : current ? 'playing' : 'idle';
    root.dataset.clip = current ? String(current.index) : '';
    // The game music steps back while a clip with sound is on screen (audio-skin.js).
    window.choSirenAudio?.duck(!!current?.withAudio);
  }
  function close() {
    ++sequence;
    const old = current, loading = pending;
    current = pending = null;
    dispose(old); dispose(loading);
    uploadedVideo = uploadedTexture = null;
    status();
  }
  function showClip(index, withAudio) {
    if (!window.choSirenStage?.active) return false;
    if (!Number.isInteger(index) || index < 0 || index >= clips.length) return false;
    load(index, withAudio, 0);
    return true;
  }
  function load(index, withAudio, variant) {
    const request = ++sequence;
    const oldPending = pending;
    pending = null;
    dispose(oldPending);
    // Each request owns its media element and callbacks. The current frame keeps
    // rendering until the new clip is actually playing, including on slow networks.
    const video = player();
    video.playsInline = true;
    video.preload = 'auto';
    video.muted = !withAudio;
    video.crossOrigin = 'anonymous';
    video.src = new URL(variants[variant].file(clips[index]), document.baseURI).href;
    const item = { video, index, timeout: 0, packed: variants[variant].packed,
      withAudio: !!withAudio, voiceNeeded: false, voice: null };
    if (debug) {
      debug('load', clips[index], variants[variant].name, withAudio ? 'with sound' : 'muted');
      for (const type of ['loadedmetadata', 'canplay', 'playing', 'waiting', 'stalled', 'ended']) {
        video.addEventListener(type, () => debug(type, `${video.videoWidth}x${video.videoHeight}`, `t=${video.currentTime.toFixed(2)}`));
      }
      video.addEventListener('error', () => debug('error', video.error && video.error.code, video.error && video.error.message));
    }
    // When the browser refused to start the video with sound (iOS outside a tap), the
    // clip's voice track plays through Web Audio, restarted at the video's time
    // whenever playback (re)starts.
    const syncVoice = () => {
      item.voice?.stop();
      item.voice = item.voiceNeeded && current === item && window.choSirenAudio
        ? window.choSirenAudio.playVoice(clips[index], () => video.currentTime) : null;
    };
    pending = item;
    root.append(video);
    status();
    const fail = () => {
      if (pending !== item || request !== sequence) return;
      debug?.('gave up on', clips[index]);
      pending = null;
      dispose(item);
      status();
    };
    // A copy this browser cannot decode hands over to the next one in line.
    const fallback = () => {
      if (pending !== item || request !== sequence) return;
      if (variant + 1 < variants.length) load(index, withAudio, variant + 1);
      else fail();
    };
    video.addEventListener('playing', () => {
      if (current === item) { syncVoice(); return; }
      if (pending !== item || request !== sequence) return;
      clearTimeout(item.timeout);
      const old = current;
      current = item;
      pending = null;
      dispose(old);
      status();
      syncVoice();
    });
    for (const type of ['waiting', 'pause']) {
      video.addEventListener(type, () => { item.voice?.stop(); item.voice = null; });
    }
    video.addEventListener('ended', () => {
      if (current !== item) return;
      current = null;
      dispose(item);
      status();
    });
    video.addEventListener('error', () => {
      if (pending === item) fallback();
      else if (current === item) { current = null; dispose(item); status(); }
    });
    item.timeout = setTimeout(fail, 15000);
    const unsupported = error => error && error.name === 'NotSupportedError';
    const attempt = video.play();
    attempt?.catch(error => {
      debug?.('play refused:', error && error.name);
      if (pending !== item || request !== sequence) return;
      if (unsupported(error)) { fallback(); return; }
      video.muted = true;
      item.voiceNeeded = item.withAudio;
      // A rejected old retry must never close a newer performance.
      video.play().catch(retryError => {
        debug?.('muted play refused:', retryError && retryError.name);
        if (unsupported(retryError)) fallback(); else fail();
      });
    });
  }
  function uploadFrame(gl, texture) {
    const video = current?.video;
    if (!video || video.readyState < 2) return false;
    if (uploadedVideo === video && uploadedTime === video.currentTime && uploadedTexture === texture) return true;
    if (current.packed) return uploadPackedFrame(gl, texture, video);
    const bound = gl.getParameter(gl.TEXTURE_BINDING_2D);
    const flipped = gl.getParameter(gl.UNPACK_FLIP_Y_WEBGL);
    const premultiplied = gl.getParameter(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL);
    try {
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
      gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, video);
      uploadedVideo = video; uploadedTime = video.currentTime; uploadedTexture = texture;
      root.dataset.renderedFrame = String(video.currentTime);
      return true;
    } catch (_) {
      close();
      return false;
    } finally {
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, flipped);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, premultiplied);
      gl.bindTexture(gl.TEXTURE_2D, bound);
    }
  }
  // Draws one packed frame into Unity's texture with the same result as a plain
  // texSubImage2D of a transparent video (flipped rows, straight alpha). Unity keeps
  // its own copy of the GL state between frames, so everything touched here is read
  // first and restored afterwards.
  const CAPABILITIES = ['BLEND', 'CULL_FACE', 'DEPTH_TEST', 'POLYGON_OFFSET_FILL', 'RASTERIZER_DISCARD',
    'SAMPLE_ALPHA_TO_COVERAGE', 'SAMPLE_COVERAGE', 'SCISSOR_TEST', 'STENCIL_TEST'];
  const compositors = new WeakMap();
  function compositor(gl) {
    let entry = compositors.get(gl);
    if (entry) return entry;
    const shader = (type, source) => {
      const object = gl.createShader(type);
      gl.shaderSource(object, source);
      gl.compileShader(object);
      if (!gl.getShaderParameter(object, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(object));
      return object;
    };
    const program = gl.createProgram();
    gl.attachShader(program, shader(gl.VERTEX_SHADER, `#version 300 es
void main() {
  gl_Position = vec4(gl_VertexID == 1 ? 3.0 : -1.0, gl_VertexID == 2 ? 3.0 : -1.0, 0.0, 1.0);
}`));
    gl.attachShader(program, shader(gl.FRAGMENT_SHADER, `#version 300 es
precision highp float;
uniform highp sampler2D uFrame;
uniform ivec3 uPacking;  // colour width, height, first alpha column
uniform bool uLinear;    // sRGB target: write linear values so the stored bytes match a plain upload
out vec4 outColor;
void main() {
  ivec2 p = ivec2(gl_FragCoord.xy);
  ivec2 texel = ivec2(p.x, uPacking.y - 1 - p.y);
  vec3 rgb = texelFetch(uFrame, texel, 0).rgb;
  float alpha = texelFetch(uFrame, texel + ivec2(uPacking.z, 0), 0).g;
  alpha = clamp((alpha - 2.0 / 255.0) * (255.0 / 251.0), 0.0, 1.0);
  if (uLinear) rgb = mix(rgb / 12.92, pow((rgb + 0.055) / 1.055, vec3(2.4)), step(0.04045, rgb));
  outColor = vec4(rgb, alpha);
}`));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
    entry = { program, frame: gl.createTexture(), framebuffer: gl.createFramebuffer(),
      vertexArray: gl.createVertexArray(), width: 0, height: 0,
      uFrame: gl.getUniformLocation(program, 'uFrame'), uPacking: gl.getUniformLocation(program, 'uPacking'),
      uLinear: gl.getUniformLocation(program, 'uLinear') };
    compositors.set(gl, entry);
    return entry;
  }
  function uploadPackedFrame(gl, texture, video) {
    const width = (video.videoWidth - PACKED_GAP) / 2, height = video.videoHeight;
    if (!Number.isInteger(width) || width <= 0 || !height) return false;
    const saved = {
      activeTexture: gl.getParameter(gl.ACTIVE_TEXTURE),
      framebuffer: gl.getParameter(gl.DRAW_FRAMEBUFFER_BINDING),
      viewport: gl.getParameter(gl.VIEWPORT),
      program: gl.getParameter(gl.CURRENT_PROGRAM),
      vertexArray: gl.getParameter(gl.VERTEX_ARRAY_BINDING),
      unpackBuffer: gl.getParameter(gl.PIXEL_UNPACK_BUFFER_BINDING),
      flipped: gl.getParameter(gl.UNPACK_FLIP_Y_WEBGL),
      premultiplied: gl.getParameter(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL),
      colorMask: gl.getParameter(gl.COLOR_WRITEMASK),
      enabled: CAPABILITIES.map(name => gl.isEnabled(gl[name]))
    };
    gl.activeTexture(gl.TEXTURE0);
    saved.texture = gl.getParameter(gl.TEXTURE_BINDING_2D);
    saved.sampler = gl.getParameter(gl.SAMPLER_BINDING);
    try {
      const c = compositor(gl);
      gl.bindBuffer(gl.PIXEL_UNPACK_BUFFER, null);
      gl.bindSampler(0, null);
      gl.bindTexture(gl.TEXTURE_2D, c.frame);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
      if (c.width !== video.videoWidth || c.height !== height) {
        for (const name of ['TEXTURE_MIN_FILTER', 'TEXTURE_MAG_FILTER']) gl.texParameteri(gl.TEXTURE_2D, gl[name], gl.NEAREST);
        for (const name of ['TEXTURE_WRAP_S', 'TEXTURE_WRAP_T']) gl.texParameteri(gl.TEXTURE_2D, gl[name], gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, video);
        c.width = video.videoWidth; c.height = height;
      } else {
        gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, video);
      }
      gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, c.framebuffer);
      gl.framebufferTexture2D(gl.DRAW_FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);
      if (gl.checkFramebufferStatus(gl.DRAW_FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) throw new Error('target not renderable');
      const srgb = gl.getFramebufferAttachmentParameter(gl.DRAW_FRAMEBUFFER, gl.COLOR_ATTACHMENT0,
        gl.FRAMEBUFFER_ATTACHMENT_COLOR_ENCODING) === gl.SRGB;
      for (const name of CAPABILITIES) gl.disable(gl[name]);
      gl.colorMask(true, true, true, true);
      gl.viewport(0, 0, width, height);
      gl.useProgram(c.program);
      gl.uniform1i(c.uFrame, 0);
      gl.uniform3i(c.uPacking, width, height, width + PACKED_GAP);
      gl.uniform1i(c.uLinear, srgb ? 1 : 0);
      gl.bindVertexArray(c.vertexArray);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
      gl.framebufferTexture2D(gl.DRAW_FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, null, 0);
      uploadedVideo = video; uploadedTime = video.currentTime; uploadedTexture = texture;
      root.dataset.renderedFrame = String(video.currentTime);
      return true;
    } catch (error) {
      debug?.('compositor failed:', error && error.message);
      close();
      return false;
    } finally {
      gl.bindVertexArray(saved.vertexArray);
      gl.useProgram(saved.program);
      gl.viewport(saved.viewport[0], saved.viewport[1], saved.viewport[2], saved.viewport[3]);
      gl.colorMask(saved.colorMask[0], saved.colorMask[1], saved.colorMask[2], saved.colorMask[3]);
      CAPABILITIES.forEach((name, i) => saved.enabled[i] ? gl.enable(gl[name]) : gl.disable(gl[name]));
      gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, saved.framebuffer);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, saved.flipped);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, saved.premultiplied);
      gl.bindTexture(gl.TEXTURE_2D, saved.texture);
      gl.bindSampler(0, saved.sampler);
      gl.bindBuffer(gl.PIXEL_UNPACK_BUFFER, saved.unpackBuffer);
      gl.activeTexture(saved.activeTexture);
    }
  }
  document.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
  // iOS pauses media when the page goes to the background and never resumes it, which
  // would leave the clip frozen on screen with the music still ducked; it ends instead.
  document.addEventListener('visibilitychange', () => { if (document.hidden) close(); });
  window.choSirenCharacter = {
    play(index, withAudio) { return showClip(((index % clips.length) + clips.length) % clips.length, withAudio); },
    close, uploadFrame,
    get active() { return !!(current || pending); },
    get currentClip() { return current ? current.index : -1; }
  };
  status();
})();

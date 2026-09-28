(() => {
  const root = document.querySelector('#character-moment');
  const clips = ['catalena-look', 'catalena-whisper', 'catalena-live'];
  let current = null, pending = null, sequence = 0;
  let uploadedVideo = null, uploadedTime = -1, uploadedTexture = null;
  const dispose = item => {
    if (!item) return;
    clearTimeout(item.timeout);
    item.video.pause();
    item.video.removeAttribute('src');
    item.video.load();
    item.video.remove();
  };
  function status() {
    root.dataset.state = pending ? 'loading' : current ? 'playing' : 'idle';
    root.dataset.clip = current ? String(current.index) : '';
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
    const request = ++sequence;
    const oldPending = pending;
    pending = null;
    dispose(oldPending);
    // Each request owns its media element and callbacks. The current frame keeps
    // rendering until the new clip is actually playing, including on slow networks.
    const video = document.createElement('video');
    video.playsInline = true;
    video.preload = 'auto';
    video.muted = !withAudio;
    video.crossOrigin = 'anonymous';
    video.src = new URL(`media/${clips[index]}.webm?v=20260928-r10`, document.baseURI).href;
    const item = { video, index, timeout: 0 };
    pending = item;
    root.append(video);
    status();
    const fail = () => {
      if (pending !== item || request !== sequence) return;
      pending = null;
      dispose(item);
      status();
    };
    video.addEventListener('playing', () => {
      if (pending !== item || request !== sequence) return;
      clearTimeout(item.timeout);
      const old = current;
      current = item;
      pending = null;
      dispose(old);
      status();
    });
    video.addEventListener('ended', () => {
      if (current !== item) return;
      current = null;
      dispose(item);
      status();
    });
    video.addEventListener('error', () => {
      if (pending === item) fail();
      else if (current === item) { current = null; dispose(item); status(); }
    });
    item.timeout = setTimeout(fail, 15000);
    const attempt = video.play();
    attempt?.catch(() => {
      if (pending !== item || request !== sequence) return;
      video.muted = true;
      // A rejected old retry must never close a newer performance.
      video.play().catch(fail);
    });
    return true;
  }
  function uploadFrame(gl, texture) {
    const video = current?.video;
    if (!video || video.readyState < 2) return false;
    if (uploadedVideo === video && uploadedTime === video.currentTime && uploadedTexture === texture) return true;
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
  document.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
  window.choSirenCharacter = {
    play(index, withAudio) { return showClip(((index % clips.length) + clips.length) % clips.length, withAudio); },
    close, uploadFrame,
    get active() { return !!(current || pending); },
    get currentClip() { return current ? current.index : -1; }
  };
  status();
})();

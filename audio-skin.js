// Unity owns the one global soundtrack and its saved music/SFX switches.
// This layer skins only short UI placeholders, resumes Web Audio on mobile
// gestures, and lets character videos duck Unity's real music without replacing it.
(() => {
  const media = name => new URL(`media/${name}?v=20261005-r17d`, document.baseURI).href;
  const SKINS = [
    { kind: 'click', length: 1654, file: 'ui-click.wav' },
    { kind: 'success', length: 7497, file: 'ui-success.wav' }
  ];
  const voices = new Map();
  const contexts = new Set();
  let unityContext = null, ownContext = null;
  let bridgeName = null, musicEnabled = true, pendingMusicChoice = null;
  let ducked = false, hasSoundtrack = false;

  function state() {
    return { enabled: pendingMusicChoice ?? musicEnabled,
      ready: !!bridgeName && !!window.choSirenUnityInstance?.SendMessage, hasSoundtrack, ducked };
  }
  function publishState() {
    if (window.CustomEvent && window.dispatchEvent)
      window.dispatchEvent(new window.CustomEvent('cho-siren-music-state', { detail: state() }));
  }
  function send(method, value) {
    const instance = window.choSirenUnityInstance;
    if (!bridgeName || !instance?.SendMessage) return false;
    try { instance.SendMessage(bridgeName, method, value ? '1' : '0'); return true; }
    catch (_) { return false; }
  }
  function flushBridge() {
    send('SetMusicDucked', ducked);
    if (pendingMusicChoice !== null && send('SetMusicEnabled', pendingMusicChoice)) {
      musicEnabled = pendingMusicChoice;
      pendingMusicChoice = null;
    }
    publishState();
  }

  const Context = window.AudioContext || window.webkitAudioContext;
  const Source = window.AudioBufferSourceNode;
  const Offline = window.OfflineAudioContext || window.webkitOfflineAudioContext;
  function decode(url) {
    return fetch(url).then(response => {
      if (!response.ok) throw new Error(`Audio fetch failed (${response.status})`);
      return response.arrayBuffer();
    }).then(data => new Promise((resolve, reject) => new Offline(2, 1, 48000).decodeAudioData(data, resolve, reject)));
  }
  if (Source && Offline) {
    for (const skin of SKINS) decode(media(skin.file)).then(buffer => { skin.buffer = buffer; }).catch(() => {});
    const bufferProp = Object.getOwnPropertyDescriptor(Source.prototype, 'buffer');
    if (bufferProp?.set && bufferProp?.get) Object.defineProperty(Source.prototype, 'buffer', {
      configurable: true, enumerable: bufferProp.enumerable,
      get() { return bufferProp.get.call(this); },
      set(value) {
        if (value && this.context && this.context !== ownContext) unityContext = this.context;
        let skin = null;
        try {
          if (value?.sampleRate === 22050 && value.numberOfChannels === 1) {
            skin = SKINS.find(item => item.buffer && item.length === value.length);
            if (skin) {
              const data = value.getChannelData(0);
              let audible = false;
              for (let i = 0; i < data.length; i += 97) if (Math.abs(data[i]) > 1e-4) { audible = true; break; }
              if (!audible) skin = null;
            }
          }
        } catch (_) { skin = null; }
        bufferProp.set.call(this, skin ? skin.buffer : value);
      }
    });
  }

  function context() {
    if (unityContext?.state === 'running') return unityContext;
    if (!ownContext) ownContext = new Context();
    return ownContext;
  }
  const nativeResume = Context?.prototype.resume;
  if (nativeResume) Context.prototype.resume = function (...args) {
    contexts.add(this);
    return nativeResume.apply(this, args);
  };
  const unlock = () => {
    for (const ctx of new Set([...contexts, unityContext, ownContext])) {
      if (!ctx || (ctx.state !== 'suspended' && ctx.state !== 'interrupted')) continue;
      try { (nativeResume || ctx.resume).call(ctx).catch(() => {}); } catch (_) {}
    }
  };
  for (const type of ['pointerdown', 'pointerup', 'touchend', 'keydown'])
    window.addEventListener(type, unlock, { capture: true, passive: true });

  window.choSirenAudio = {
    bindMusicBridge(name, enabled, soundtrack) {
      bridgeName = name;
      musicEnabled = !!enabled;
      hasSoundtrack = !!soundtrack;
      flushBridge();
    },
    // Awake can run before createUnityInstance resolves. Replay when it does.
    attachUnity() { flushBridge(); },
    unbindMusicBridge(name) { if (bridgeName === name) { bridgeName = null; publishState(); } },
    syncMusicEnabled(enabled) { musicEnabled = !!enabled; publishState(); },
    setMusicEnabled(enabled) {
      pendingMusicChoice = !!enabled;
      flushBridge();
      unlock();
    },
    get musicState() { return state(); },
    playVoice(name, currentTime, onEnded) {
      let source = null, stopped = false;
      if (!Context || !Offline) return { stop() {} };
      if (!voices.has(name)) voices.set(name, decode(media(`${name}.voice.mp3`)));
      voices.get(name).then(buffer => {
        if (stopped) return;
        const ctx = context();
        source = ctx.createBufferSource();
        source.buffer = buffer;
        source.connect(ctx.destination);
        source.onended = () => { if (!stopped) onEnded?.(); };
        source.start(0, Math.max(0, Math.min(buffer.duration, currentTime())));
      }).catch(() => {});
      return { stop() { stopped = true; try { source?.stop(); } catch (_) {} } };
    },
    duck(on) { ducked = !!on; send('SetMusicDucked', ducked); },
    get skins() { return SKINS.map(({ kind, buffer }) => ({ kind, ready: !!buffer })); }
  };
})();

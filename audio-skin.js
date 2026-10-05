// Gives the game its composed sound without touching Unity's audio routing.
//
// Unity currently generates three placeholder sounds at runtime: an 8-second music
// loop, a 75 ms click and a 0.34 s "success" arpeggio (22.05 kHz mono). When Unity
// assigns one of those buffers to a source, the composed version from media/ is
// assigned instead. Unity still creates, starts, stops, pitches and mutes every
// voice, so the in-game music and sound switches keep working. Placeholders are
// recognised by their exact shape and non-silent content; a real soundtrack added in
// Unity later (any other rate, length or channel count) plays untouched.
//
// It also plays the character clips' voice tracks for character-moments.js when a
// browser refuses to start a video with sound outside a tap (iOS), and ducks the
// music while a clip with sound is on screen.
(() => {
  const Source = window.AudioBufferSourceNode;
  const Node = window.AudioNode;
  if (!Source || !Node) return;
  const media = name => new URL(`media/${name}?v=20261005-r17d`, document.baseURI).href;
  const SKINS = [
    { kind: 'music', length: 176400, file: 'lobby-theme.mp3', loopSeconds: 64 },
    { kind: 'click', length: 1654, file: 'ui-click.wav' },
    { kind: 'success', length: 7497, file: 'ui-success.wav' }
  ];
  const PLACEHOLDER_RATE = 22050;
  const DUCKED = 0.28;
  const voices = new Map();      // clip name -> Promise<AudioBuffer>
  const duckGains = new Set();
  let unityContext = null, ownContext = null, ducked = false;

  function decoder() {
    const Offline = window.OfflineAudioContext || window.webkitOfflineAudioContext;
    return new Offline(2, 1, 48000);
  }
  function decode(url) {
    return fetch(url).then(response => {
      if (!response.ok) throw new Error(`${response.status} ${url}`);
      return response.arrayBuffer();
    }).then(data => new Promise((resolve, reject) => decoder().decodeAudioData(data, resolve, reject)));
  }

  // The loop file holds 64 s plus its first 2 s again, so wherever the decoder puts
  // sample 0 the loop is the next 64 s from the first audible sample.
  function musicLoop(buffer, skin) {
    const left = buffer.getChannelData(0);
    let first = 0;
    while (first < Math.min(left.length, buffer.sampleRate) && Math.abs(left[first]) < 0.003) first++;
    skin.loopStart = first / buffer.sampleRate;
    skin.loopEnd = Math.min(buffer.duration, skin.loopStart + skin.loopSeconds);
  }
  for (const skin of SKINS) {
    decode(media(skin.file)).then(buffer => {
      if (skin.kind === 'music') musicLoop(buffer, skin);
      skin.buffer = buffer;
    }).catch(() => {});
  }

  function match(buffer) {
    if (!buffer || buffer.sampleRate !== PLACEHOLDER_RATE || buffer.numberOfChannels !== 1) return null;
    const skin = SKINS.find(item => item.buffer && item.length === buffer.length);
    if (!skin) return null;
    // Unity keeps a silent twin of each generated clip; only the audible one is swapped.
    const data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i += 97) if (Math.abs(data[i]) > 1e-4) return skin;
    return null;
  }

  const skinned = new WeakMap();
  const accessor = name => Object.getOwnPropertyDescriptor(Source.prototype, name);
  const bufferProp = accessor('buffer'), loopStartProp = accessor('loopStart'), loopEndProp = accessor('loopEnd');
  if (!bufferProp || !bufferProp.set || !loopStartProp || !loopEndProp) return;
  Object.defineProperty(Source.prototype, 'buffer', {
    configurable: true, enumerable: bufferProp.enumerable,
    get() { return bufferProp.get.call(this); },
    set(value) {
      let skin = null;
      try { skin = match(value); } catch (_) {}
      if (!skin) return bufferProp.set.call(this, value);
      unityContext = this.context;
      skinned.set(this, skin);
      bufferProp.set.call(this, skin.buffer);
      if (skin.kind === 'music') {
        loopStartProp.set.call(this, skin.loopStart);
        loopEndProp.set.call(this, skin.loopEnd);
      }
    }
  });
  // Unity sets loop points for its 8 s clip; the composed loop keeps its own.
  for (const [name, prop] of [['loopStart', loopStartProp], ['loopEnd', loopEndProp]]) {
    Object.defineProperty(Source.prototype, name, {
      configurable: true, enumerable: prop.enumerable,
      get() { return prop.get.call(this); },
      set(value) { if (skinned.get(this)?.kind !== 'music') prop.set.call(this, value); }
    });
  }
  // Music voices pass through a gain of ours so they can be ducked under clip audio.
  const connect = Node.prototype.connect;
  Node.prototype.connect = function (destination, ...rest) {
    if (skinned.get(this)?.kind !== 'music' || !(destination instanceof Node)) return connect.call(this, destination, ...rest);
    try {
      const gain = this.context.createGain();
      gain.gain.value = ducked ? DUCKED : 1;
      duckGains.add(gain);
      connect.call(this, gain);
      return connect.call(gain, destination, ...rest);
    } catch (_) {
      return connect.call(this, destination, ...rest);
    }
  };

  const Context = window.AudioContext || window.webkitAudioContext;
  function context() {
    if (unityContext && unityContext.state === 'running') return unityContext;
    if (!ownContext) ownContext = new Context();
    return ownContext;
  }
  // Audio contexts start suspended until a tap, and iOS suspends ("interrupts") them
  // again after a call or an app switch. Unity retries resume() every 400 ms and on
  // touchstart/mousedown, but iOS only lets audio start on touchend, so every tap
  // here also resumes Unity's context (learnt from those retries) and ours.
  const contexts = new Set();
  const nativeResume = Context && Context.prototype.resume;
  if (nativeResume) {
    Context.prototype.resume = function (...args) {
      contexts.add(this);
      return nativeResume.apply(this, args);
    };
  }
  const unlock = () => {
    for (const ctx of new Set([...contexts, unityContext, ownContext])) {
      if (!ctx || (ctx.state !== 'suspended' && ctx.state !== 'interrupted')) continue;
      try { (nativeResume || ctx.resume).call(ctx).catch(() => {}); } catch (_) {}
    }
  };
  for (const type of ['pointerdown', 'pointerup', 'touchend', 'keydown']) {
    window.addEventListener(type, unlock, { capture: true, passive: true });
  }

  window.choSirenAudio = {
    // Starts a clip's voice track at the video's current time; returns { stop() }.
    playVoice(name, currentTime) {
      let source = null, stopped = false;
      if (!voices.has(name)) voices.set(name, decode(media(`${name}.voice.mp3`)));
      voices.get(name).then(buffer => {
        if (stopped) return;
        const ctx = context();
        source = ctx.createBufferSource();
        source.buffer = buffer;
        source.connect(ctx.destination);
        source.start(0, Math.max(0, Math.min(buffer.duration, currentTime())));
      }).catch(() => {});
      return { stop() { stopped = true; try { source?.stop(); } catch (_) {} } };
    },
    duck(on) {
      ducked = !!on;
      for (const gain of duckGains) {
        try {
          const now = gain.context.currentTime;
          gain.gain.cancelScheduledValues(now);
          gain.gain.setTargetAtTime(ducked ? DUCKED : 1, now, 0.12);
        } catch (_) {}
      }
    },
    get skins() { return SKINS.map(({ kind, buffer, loopStart, loopEnd }) => ({ kind, ready: !!buffer, loopStart, loopEnd })); }
  };
})();

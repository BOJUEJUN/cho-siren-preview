// The movie's own BGM takes priority; the global transport resumes on dismissal.
(() => {
  const loading = document.querySelector('#loading');
  const video = document.querySelector('#loading-cg');
  const sound = document.querySelector('#sound-button');
  if (!loading || !video) return;
  const source = video.getAttribute('src');
  const hasBgm = video.getAttribute('data-has-bgm') === 'true';
  const reduced = !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  let done = false, failed = false, unlocked = false;
  const enabled = () => window.choSirenAudio?.musicState.enabled ?? true;
  const ownership = () => window.choSirenAudio?.setMediaMusicActive('loading-cg',
    hasBgm && !reduced && !failed && !done);
  const applySound = () => {
    video.volume = hasBgm ? .45 : 0;
    // A returning player's saved mute choice arrives from Unity. Until then only
    // an explicit title music choice can make the preference known.
    const known = window.choSirenAudio?.musicState.settingKnown === true;
    video.muted = !hasBgm || !unlocked || !known || !enabled() || done || failed || document.hidden;
    sound?.setAttribute('aria-pressed', enabled() ? 'true' : 'false');
    sound?.setAttribute('aria-label', enabled() ? '关闭音乐' : '开启音乐');
  };
  const fail = () => { if (done || failed) return; failed = true; video.muted = true; video.pause(); ownership(); };
  const play = () => {
    if (done || failed || reduced || document.hidden) return;
    try {
      video.play().catch(error => {
        if (done) return;
        if (error?.name === 'NotAllowedError') {
          // Autoplay rejection waits for a tap without exposing global BGM underneath.
          unlocked = false; applySound(); video.play().catch(() => {});
        } else if (error?.name !== 'AbortError') fail();
      });
    } catch (_) { fail(); }
  };
  const gesture = () => {
    if (done || reduced || failed) return;
    unlocked = true; applySound(); play();
  };
  video.muted = true; ownership(); applySound();
  video.addEventListener('error', fail);
  window.addEventListener('cho-siren-music-state', applySound);
  for (const type of ['pointerdown', 'pointerup', 'touchend', 'keydown'])
    window.addEventListener(type, gesture, { capture: true, passive: true });
  sound?.addEventListener('click', event => {
    event.stopPropagation(); window.choSirenAudio?.setMusicEnabled(!enabled()); gesture();
  });
  if (reduced) { video.removeAttribute('autoplay'); video.pause(); } else play();
  document.addEventListener('visibilitychange', () => {
    applySound(); if (document.hidden) video.pause(); else play();
  });
  const dismiss = () => {
    if (done) return;
    done = true; video.muted = true; video.pause(); ownership();
    window.setTimeout(() => {
      video.removeAttribute('src'); video.load();
      if (source) fetch(source, { credentials: 'same-origin' }).catch(() => {});
    }, 1000);
  };
  window.addEventListener('pagehide', event => {
    // A page in the back/forward cache retains the title's lease and decoder.
    if (event.persisted) { video.muted = true; video.pause(); } else dismiss();
  });
  window.addEventListener('pageshow', () => { applySound(); play(); });
  new MutationObserver(() => { if (loading.classList.contains('is-hidden')) dismiss(); })
    .observe(loading, { attributes: true, attributeFilter: ['class'] });
})();

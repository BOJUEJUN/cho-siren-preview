// The CG is a silent visual. The speaker controls the same saved global music
// setting as the game, including a choice made while Unity is still loading.
(() => {
  const loading = document.querySelector('#loading');
  const video = document.querySelector('#loading-cg');
  const sound = document.querySelector('#sound-button');
  if (!loading || !video) return;
  const source = video.getAttribute('src');
  let done = false;
  const showSound = on => {
    if (!sound) return;
    sound.setAttribute('aria-pressed', on ? 'true' : 'false');
    sound.setAttribute('aria-label', on ? '关闭音乐' : '开启音乐');
  };
  const play = () => { if (!done && !document.hidden) video.play().catch(() => {}); };
  video.muted = true;
  video.volume = 0;
  showSound(window.choSirenAudio?.musicState.enabled ?? true);
  window.addEventListener('cho-siren-music-state', event => showSound(event.detail.enabled));
  sound?.addEventListener('click', event => {
    event.stopPropagation();
    const enabled = window.choSirenAudio?.musicState.enabled ?? true;
    window.choSirenAudio?.setMusicEnabled(!enabled);
  });

  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
    video.removeAttribute('autoplay');
    video.pause();
  } else {
    play();
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) video.pause(); else play();
    });
  }
  new MutationObserver(() => {
    if (done || !loading.classList.contains('is-hidden')) return;
    done = true;
    video.muted = true;
    window.setTimeout(() => {
      video.pause();
      video.removeAttribute('src');
      video.load();
      if (source) fetch(source, { credentials: 'same-origin' }).catch(() => {});
    }, 1000);
  }).observe(loading, { attributes: true, attributeFilter: ['class'] });
})();

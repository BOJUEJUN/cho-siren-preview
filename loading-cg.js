// The title screen's full-screen CG: a looping video behind the loading strip.
// The poster (its first frame) shows until playback starts, and stays wherever autoplay
// is refused (e.g. iPhone Low Power Mode) or the player prefers reduced motion. The
// speaker button turns the CG's own music on or off; it starts muted because phones
// block sound until the page is touched, and the choice is remembered. Once the title
// screen is dismissed the video stops and lets go of its decoder; a plain download then
// lets the asset worker keep the file, so the next launch plays it from the phone.
(() => {
  const loading = document.querySelector('#loading');
  const video = document.querySelector('#loading-cg');
  const sound = document.querySelector('#sound-button');
  if (!loading || !video) return;
  const source = video.getAttribute('src');
  const storageKey = 'cho-loading-sound';
  let wantsSound = false;
  try { wantsSound = window.localStorage.getItem(storageKey) === '1'; } catch (_) {}
  let done = false;

  const showSound = on => {
    if (!sound) return;
    sound.setAttribute('aria-pressed', on ? 'true' : 'false');
    sound.setAttribute('aria-label', on ? '关闭声音' : '开启声音');
  };
  const play = () => { if (!done) video.play().catch(() => {}); };
  // Unmuting is only allowed inside a user gesture, so it happens on a tap.
  const applySound = on => {
    video.muted = !on;
    showSound(on);
    if (on) play();
  };

  video.muted = true;
  showSound(wantsSound);
  if (sound) {
    sound.addEventListener('click', event => {
      event.stopPropagation();
      wantsSound = !wantsSound;
      try { window.localStorage.setItem(storageKey, wantsSound ? '1' : '0'); } catch (_) {}
      applySound(wantsSound);
    });
  }
  // A returning player who left sound on hears it from their first touch.
  if (wantsSound) {
    window.addEventListener('pointerdown', () => { if (!done && wantsSound) applySound(true); },
      { once: true, capture: true });
  }

  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
    video.removeAttribute('autoplay');
    video.pause();
  } else {
    play();
    // Phones pause inline video in the background; carry on when the page is back.
    document.addEventListener('visibilitychange', () => { if (!document.hidden) play(); });
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

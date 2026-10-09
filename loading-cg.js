// The title screen's full-screen CG: a looping, muted video behind the loading strip.
// The poster (its first frame) shows until playback starts, and stays wherever autoplay
// is refused (e.g. iPhone Low Power Mode) or the player prefers reduced motion. Once
// the title screen is dismissed the video stops and lets go of its decoder; a plain
// download then lets the asset worker keep the file, so the next launch plays it
// from the phone instead of the network.
(() => {
  const loading = document.querySelector('#loading');
  const video = document.querySelector('#loading-cg');
  if (!loading || !video) return;
  const source = video.getAttribute('src');
  if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
    video.removeAttribute('autoplay');
    video.pause();
    return;
  }
  let done = false;
  const play = () => { if (!done) video.play().catch(() => {}); };
  video.muted = true;
  play();
  // Phones pause inline video in the background; carry on when the page is back.
  document.addEventListener('visibilitychange', () => { if (!document.hidden) play(); });
  new MutationObserver(() => {
    if (done || !loading.classList.contains('is-hidden')) return;
    done = true;
    window.setTimeout(() => {
      video.pause();
      video.removeAttribute('src');
      video.load();
      if (source) fetch(source, { credentials: 'same-origin' }).catch(() => {});
    }, 1000);
  }).observe(loading, { attributes: true, attributeFilter: ['class'] });
})();

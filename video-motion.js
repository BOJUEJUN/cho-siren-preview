const lobbyScreen = document.querySelector('#lobby');
const heroVideo = document.querySelector('.hero-video');

if (lobbyScreen && heroVideo) {
  const revealVideo = () => lobbyScreen.classList.add('video-ready');
  const restoreStill = () => lobbyScreen.classList.remove('video-ready');
  const syncPlayback = () => {
    const shouldPlay = lobbyScreen.classList.contains('active') && !document.hidden;
    if (shouldPlay) heroVideo.play().catch(() => {});
    else heroVideo.pause();
  };

  heroVideo.addEventListener('canplay', revealVideo, { once: true });
  heroVideo.addEventListener('error', restoreStill);
  document.addEventListener('visibilitychange', syncPlayback);
  new MutationObserver(syncPlayback).observe(lobbyScreen, {
    attributes: true,
    attributeFilter: ['class']
  });

  if (heroVideo.readyState >= 3) revealVideo();
  syncPlayback();
}

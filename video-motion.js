const lobbyScreen = document.querySelector('#lobby');
const heroVideo = document.querySelector('.hero-video');
const heroAnimation = document.querySelector('.hero-animation');

if (lobbyScreen && heroVideo && heroAnimation) {
  const useMobileFallback =
    /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
    matchMedia('(pointer: coarse)').matches;
  const motionClass = useMobileFallback ? 'mobile-motion' : 'desktop-motion';
  const revealMotion = () => lobbyScreen.classList.add(motionClass, 'motion-ready');
  const restoreStill = () => lobbyScreen.classList.remove('motion-ready');
  const syncPlayback = () => {
    if (useMobileFallback) return;
    const shouldPlay = lobbyScreen.classList.contains('active') && !document.hidden;
    if (shouldPlay) heroVideo.play().catch(() => {});
    else heroVideo.pause();
  };

  if (useMobileFallback) {
    heroAnimation.addEventListener('load', revealMotion, { once: true });
    heroAnimation.addEventListener('error', restoreStill, { once: true });
    if (heroAnimation.complete && heroAnimation.naturalWidth) revealMotion();
  } else {
    heroVideo.preload = 'auto';
    heroVideo.addEventListener('canplay', revealMotion, { once: true });
    heroVideo.addEventListener('error', restoreStill);
    document.addEventListener('visibilitychange', syncPlayback);
    new MutationObserver(syncPlayback).observe(lobbyScreen, {
      attributes: true,
      attributeFilter: ['class']
    });
    heroVideo.load();
    if (heroVideo.readyState >= 3) revealMotion();
    syncPlayback();
  }
}

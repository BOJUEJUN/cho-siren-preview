const lobbyScreen = document.querySelector('#lobby');
const heroVideo = document.querySelector('.hero-video');
const heroAnimation = document.querySelector('.hero-animation');
const heroVideoSource = heroVideo?.querySelector('source');

if (lobbyScreen && heroVideo && heroAnimation && heroVideoSource) {
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

  const loadMotion = () => {
    if (useMobileFallback) {
      if (!heroAnimation.src) heroAnimation.src = heroAnimation.dataset.src;
      return;
    }
    if (!heroVideoSource.src) heroVideoSource.src = heroVideoSource.dataset.src;
    heroVideo.preload = 'auto';
    heroVideo.load();
    syncPlayback();
  };

  if (useMobileFallback) {
    heroAnimation.addEventListener('load', revealMotion, { once: true });
    heroAnimation.addEventListener('error', restoreStill, { once: true });
  } else {
    heroVideo.addEventListener('canplay', revealMotion, { once: true });
    heroVideo.addEventListener('error', restoreStill);
    document.addEventListener('visibilitychange', syncPlayback);
    new MutationObserver(syncPlayback).observe(lobbyScreen, {
      attributes: true,
      attributeFilter: ['class']
    });
    if (heroVideo.readyState >= 3) revealMotion();
  }

  const scheduleMotion = () => {
    if ('requestIdleCallback' in window) requestIdleCallback(loadMotion, { timeout: 1400 });
    else setTimeout(loadMotion, 450);
  };
  if (document.readyState === 'complete') scheduleMotion();
  else window.addEventListener('load', scheduleMotion, { once: true });
}

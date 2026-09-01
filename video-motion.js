const lobbyScreen = document.querySelector('#lobby');
const heroVideo = document.querySelector('.hero-video');
const heroAnimation = document.querySelector('.hero-animation');
const heroVideoSource = heroVideo?.querySelector('source');

if (lobbyScreen && heroVideo && heroAnimation && heroVideoSource) {
  const useMobileFallback =
    /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
    matchMedia('(pointer: coarse)').matches;
  const reducedMotionQuery = matchMedia('(prefers-reduced-motion: reduce)');
  const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
  const shouldSkipHeavyMotion = () => reducedMotionQuery.matches
    || document.documentElement.classList.contains('motion-disabled')
    || Boolean(connection?.saveData)
    || ['slow-2g', '2g'].includes(connection?.effectiveType);
  const motionClass = useMobileFallback ? 'mobile-motion' : 'desktop-motion';
  lobbyScreen.classList.add(motionClass);
  const revealMotion = () => lobbyScreen.classList.add('motion-ready');
  const restoreStill = () => lobbyScreen.classList.remove('motion-ready');
  const syncPlayback = () => {
    if (useMobileFallback) return;
    const shouldPlay = lobbyScreen.classList.contains('active') && !document.hidden && !shouldSkipHeavyMotion();
    if (!shouldPlay) {
      restoreStill();
      heroVideo.pause();
      return;
    }
    heroVideo.play()
      .then(() => {
        if (!document.hidden && lobbyScreen.classList.contains('active') && heroVideo.readyState >= 2) revealMotion();
      })
      .catch(restoreStill);
  };

  const loadMotion = () => {
    if (shouldSkipHeavyMotion()) return;
    if (useMobileFallback) {
      heroAnimation.fetchPriority = 'low';
      if (!heroAnimation.getAttribute('src')) heroAnimation.src = heroAnimation.dataset.src;
      if (heroAnimation.complete && heroAnimation.naturalWidth) revealMotion();
      return;
    }
    if (!heroVideoSource.getAttribute('src')) heroVideoSource.src = heroVideoSource.dataset.src;
    heroVideo.preload = 'auto';
    heroVideo.load();
    syncPlayback();
  };

  if (useMobileFallback) {
    heroAnimation.addEventListener('load', revealMotion, { once: true });
    heroAnimation.addEventListener('error', restoreStill, { once: true });
  } else {
    heroVideo.addEventListener('canplay', syncPlayback);
    heroVideo.addEventListener('playing', () => {
      if (!document.hidden && lobbyScreen.classList.contains('active')) revealMotion();
    });
    heroVideo.addEventListener('waiting', restoreStill);
    heroVideo.addEventListener('stalled', restoreStill);
    heroVideo.addEventListener('emptied', restoreStill);
    heroVideo.addEventListener('error', restoreStill);
    document.addEventListener('visibilitychange', syncPlayback);
    window.addEventListener('pageshow', syncPlayback);
    window.addEventListener('pagehide', () => {
      restoreStill();
      heroVideo.pause();
    });
    let wasActive = lobbyScreen.classList.contains('active');
    new MutationObserver(() => {
      const isActive = lobbyScreen.classList.contains('active');
      if (isActive === wasActive) return;
      wasActive = isActive;
      syncPlayback();
    }).observe(lobbyScreen, {
      attributes: true,
      attributeFilter: ['class']
    });
    if (heroVideo.readyState >= 2) syncPlayback();
  }

  const scheduleMotion = () => {
    if ('requestIdleCallback' in window) requestIdleCallback(loadMotion, { timeout: 1800 });
    else setTimeout(loadMotion, 650);
  };
  if (document.readyState === 'complete') scheduleMotion();
  else window.addEventListener('load', scheduleMotion, { once: true });

  const syncMotionPreference = () => {
    if (shouldSkipHeavyMotion()) {
      restoreStill();
      heroVideo.pause();
      return;
    }
    loadMotion();
    if (!useMobileFallback) syncPlayback();
  };
  window.addEventListener('cho-siren-preferences', syncMotionPreference);
  reducedMotionQuery.addEventListener?.('change', syncMotionPreference);
}

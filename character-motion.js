const lobby = document.querySelector('#lobby');
const animatedCharacter = document.querySelector('.hero-animation');
const characterVideo = document.querySelector('.hero-video');
const characterVideoSource = characterVideo?.querySelector('source');

if (lobby && animatedCharacter) {
  const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  let selectedMode = '';
  let videoStartTimer = 0;
  let lobbyWasActive = lobby.classList.contains('active');

  const motionDisabled = () => reducedMotion.matches
    || document.documentElement.classList.contains('motion-disabled');
  const slowConnection = () => Boolean(connection?.saveData)
    || ['slow-2g', '2g'].includes(connection?.effectiveType);
  const videoSupported = () => Boolean(characterVideo && characterVideoSource
    && characterVideo.canPlayType('video/webm; codecs="vp9"'));
  const reveal = mode => {
    clearTimeout(videoStartTimer);
    lobby.classList.remove('video-motion', 'image-motion');
    lobby.classList.add(`${mode}-motion`, 'motion-ready');
  };
  const hide = () => {
    clearTimeout(videoStartTimer);
    lobby.classList.remove('motion-ready', 'video-motion', 'image-motion');
    characterVideo?.pause();
  };
  const loadImage = () => {
    selectedMode = 'image';
    characterVideo?.pause();
    if (!animatedCharacter.getAttribute('src')) animatedCharacter.src = animatedCharacter.dataset.src;
    if (animatedCharacter.complete && animatedCharacter.naturalWidth) reveal('image');
  };
  const loadVideo = () => {
    if (!characterVideo || !characterVideoSource) return loadImage();
    selectedMode = 'video';
    if (!characterVideoSource.getAttribute('src')) {
      characterVideoSource.src = characterVideoSource.dataset.src;
      characterVideo.load();
    }
    clearTimeout(videoStartTimer);
    videoStartTimer = setTimeout(() => {
      if (selectedMode === 'video' && !lobby.classList.contains('video-motion')) loadImage();
    }, 4500);
    characterVideo.play().catch(loadImage);
  };
  const sync = () => {
    if (motionDisabled()) return hide();
    if (!lobby.classList.contains('active')) return hide();
    if (slowConnection()) return hide();
    if (videoSupported()) loadVideo();
    else loadImage();
  };

  const revealPlayingVideo = () => {
    if (selectedMode !== 'video' || motionDisabled() || !characterVideo) return;
    if (characterVideo.videoWidth >= 600 && characterVideo.readyState >= 2 && characterVideo.currentTime > 0) {
      reveal('video');
    }
  };

  animatedCharacter.addEventListener('load', () => {
    if (selectedMode === 'image' && !motionDisabled()) reveal('image');
  });
  animatedCharacter.addEventListener('error', hide);
  characterVideo?.addEventListener('playing', revealPlayingVideo);
  characterVideo?.addEventListener('timeupdate', revealPlayingVideo);
  characterVideo?.addEventListener('error', loadImage);
  characterVideoSource?.addEventListener('error', loadImage);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
      clearTimeout(videoStartTimer);
      characterVideo?.pause();
    }
    else if (selectedMode === 'video' && lobby.classList.contains('active')) loadVideo();
  });
  new MutationObserver(() => {
    const isActive = lobby.classList.contains('active');
    if (isActive === lobbyWasActive) return;
    lobbyWasActive = isActive;
    if (isActive) sync();
    else hide();
  }).observe(lobby, { attributes: true, attributeFilter: ['class'] });
  window.addEventListener('cho-siren-preferences', sync);
  reducedMotion.addEventListener?.('change', sync);
  if (document.readyState === 'complete') sync();
  else window.addEventListener('load', sync, { once: true });
}

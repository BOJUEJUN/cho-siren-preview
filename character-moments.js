(() => {
  const root = document.querySelector('#character-moment');
  const video = document.querySelector('#moment-video');
  const clips = [
    'media/catalena-look.webm?v=20260928-r10',
    'media/catalena-whisper.webm?v=20260928-r10',
    'media/catalena-live.webm?v=20260928-r10'
  ];
  let active = false;
  let concealed = false;
  let sequence = 0;

  function setPortraitConcealed(value) {
    if (concealed === value) return;
    concealed = value;
    try {
      window.choSirenUnityInstance?.SendMessage('PsdHome20260921',
        'SetCharacterPlayback', value ? 1 : 0);
    } catch (_) { /* The player may have just left the homepage. */ }
  }

  function close() {
    if (!active && !concealed) return;
    active = false;
    ++sequence;
    video.pause();
    video.removeAttribute('src');
    video.load();
    setPortraitConcealed(false);
    root.classList.remove('is-active');
    root.setAttribute('aria-hidden', 'true');
  }

  function showClip(index, withAudio) {
    if (!active && !window.choSirenStage?.active) return false;
    const next = Number(index);
    if (!Number.isInteger(next) || next < 0 || next >= clips.length) return false;
    active = true;
    const playback = ++sequence;
    video.pause();
    video.muted = !withAudio;
    video.src = new URL(clips[next], document.baseURI).href;
    video.load();
    root.dataset.clip = String(next);
    root.setAttribute('aria-hidden', 'false');
    root.classList.add('is-active');
    document.querySelector('#character-dialogue').classList.remove('is-visible');
    const attempt = video.play();
    if (attempt?.catch) attempt.catch(() => {
      if (!active || sequence !== playback) return;
      // A browser may refuse audio when Unity's click callback arrives later;
      // keep the requested animation available even if sound is blocked.
      video.muted = true;
      video.play().catch(close);
    });
    return true;
  }

  video.addEventListener('playing', () => { if (active) setPortraitConcealed(true); });
  video.addEventListener('ended', close);
  video.addEventListener('error', close);
  document.addEventListener('keydown', event => { if (active && event.key === 'Escape') close(); });
  window.choSirenCharacter = { play(index, withAudio) {
    return showClip(((index % clips.length) + clips.length) % clips.length, withAudio);
  }, close, get active() { return active; } };
})();

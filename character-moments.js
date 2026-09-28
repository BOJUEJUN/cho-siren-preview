(() => {
  const root = document.querySelector('#character-moment');
  const video = document.querySelector('#moment-video');
  const title = document.querySelector('#moment-title');
  const status = document.querySelector('#moment-status');
  const playButton = document.querySelector('#moment-play');
  const clips = [
    { src: 'media/catalena-look.webm', title: '她在看着你', status: '对白 01 · 注视' },
    { src: 'media/catalena-whisper.webm', title: '悄悄话', status: '对白 02 · 耳边的声音' },
    { src: 'media/catalena-live.webm', title: '第二段表演 LIVE', status: 'LIVE · 只唱歌，不说话' }
  ];
  const buttons = [...root.querySelectorAll('[data-moment]')];
  let active = false;
  let selected = 0;
  let audioEnabled = true;
  let concealed = false;
  let sequence = 0;
  let endTimer = 0;

  function setPortraitConcealed(value) {
    if (concealed === value) return;
    concealed = value;
    try {
      window.choSirenUnityInstance?.SendMessage('PsdHome20260921',
        'SetCharacterPlayback', value ? 1 : 0);
    } catch (_) { /* The lobby may have just been left. */ }
  }

  function close() {
    if (!active && !concealed) return;
    active = false;
    ++sequence;
    window.clearTimeout(endTimer);
    video.pause();
    video.removeAttribute('src');
    video.load();
    setPortraitConcealed(false);
    root.classList.remove('is-active', 'needs-play');
    root.setAttribute('aria-hidden', 'true');
  }

  function showClip(index, withAudio) {
    if (!active && !window.choSirenStage?.active) return false;
    const next = Number(index);
    if (!Number.isInteger(next) || next < 0 || next >= clips.length) return false;
    active = true;
    selected = next;
    audioEnabled = !!withAudio;
    const playSequence = ++sequence;
    const clip = clips[next];
    window.clearTimeout(endTimer);
    video.pause();
    setPortraitConcealed(false);
    video.muted = !audioEnabled;
    video.src = new URL(clip.src, document.baseURI).href;
    video.load();
    title.textContent = clip.title;
    status.textContent = clip.status;
    buttons.forEach(button => button.classList.toggle('is-selected', Number(button.dataset.moment) === next));
    root.setAttribute('aria-hidden', 'false');
    root.classList.add('is-active');
    root.classList.remove('needs-play');
    document.querySelector('#character-dialogue').classList.remove('is-visible');
    const attempt = video.play();
    if (attempt?.catch) attempt.catch(() => {
      if (active && sequence === playSequence) {
        root.classList.add('needs-play');
        status.textContent = '轻触继续 · ' + clip.status;
      }
    });
    return true;
  }

  function playFromCharacter(lineIndex, withAudio) {
    return showClip(((lineIndex % clips.length) + clips.length) % clips.length, withAudio);
  }

  document.querySelector('#moment-close').addEventListener('click', close);
  root.addEventListener('click', event => {
    const button = event.target.closest('[data-moment]');
    if (button && root.contains(button)) showClip(Number(button.dataset.moment), audioEnabled);
  });
  playButton.addEventListener('click', () => {
    const attempt = video.play();
    if (attempt?.then) attempt.then(() => root.classList.remove('needs-play')).catch(() => {
      status.textContent = '播放失败，请切换片段重试';
    });
  });
  video.addEventListener('playing', () => { if (active) setPortraitConcealed(true); });
  video.addEventListener('ended', () => {
    if (!active) return;
    status.textContent = selected === 2 ? 'LIVE 演出结束' : '对白结束';
    endTimer = window.setTimeout(close, 1200);
  });
  video.addEventListener('error', () => {
    if (active) {
      status.textContent = '视频加载失败，点击角色可重试';
      endTimer = window.setTimeout(close, 1800);
    }
  });
  document.addEventListener('keydown', event => { if (active && event.key === 'Escape') close(); });
  window.choSirenCharacter = { play: playFromCharacter, close,
    get active() { return active; }, get selected() { return selected; } };
})();

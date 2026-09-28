(() => {
  const root = document.querySelector('#character-moment');
  const video = document.querySelector('#moment-video');
  const title = document.querySelector('#moment-title');
  const status = document.querySelector('#moment-status');
  const playButton = document.querySelector('#moment-play');
  const clips = [
    { src: 'media/catalena-look.mp4', poster: 'media/catalena-look.jpg', title: '她在看着你', status: '对白 01 · 注视' },
    { src: 'media/catalena-whisper.mp4', poster: 'media/catalena-whisper.jpg', title: '悄悄话', status: '对白 02 · 耳边的声音' },
    { src: 'media/catalena-live.mp4', poster: 'media/catalena-live.jpg', title: '第二段表演 LIVE', status: 'LIVE · 只唱歌，不说话' }
  ];
  const buttons = [...root.querySelectorAll('[data-moment]')];
  let active = false;
  let selected = 0;
  let audioEnabled = true;
  let sequence = 0;

  function close() {
    if (!active) return;
    active = false;
    ++sequence;
    video.pause();
    video.removeAttribute('src');
    video.load();
    root.classList.remove('is-active', 'needs-play');
    root.setAttribute('aria-hidden', 'true');
  }

  function showClip(index, withAudio) {
    if (!window.choSirenStage?.active) return false;
    const next = Number(index);
    if (!Number.isInteger(next) || next < 0 || next >= clips.length) return false;
    active = true;
    selected = next;
    audioEnabled = withAudio;
    const playSequence = ++sequence;
    const clip = clips[next];
    video.pause();
    video.muted = !audioEnabled;
    video.poster = new URL(clip.poster, document.baseURI).href;
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
        status.textContent = '轻触播放 · ' + clip.status;
      }
    });
    return true;
  }

  function playFromCharacter(lineIndex, withAudio) {
    return showClip(Math.abs(lineIndex) % 2, withAudio);
  }

  document.querySelector('#moment-close').addEventListener('click', close);
  buttons.forEach(button => button.addEventListener('click', () => showClip(Number(button.dataset.moment), audioEnabled)));
  playButton.addEventListener('click', () => {
    const attempt = video.play();
    if (attempt?.then) attempt.then(() => root.classList.remove('needs-play')).catch(() => {
      status.textContent = '播放失败，请切换片段重试';
    });
  });
  video.addEventListener('ended', () => {
    status.textContent = selected === 2 ? 'LIVE 已结束 · 选一段对白或返回首页' : '演出结束 · 选下一段或返回首页';
    root.classList.add('needs-play');
  });
  video.addEventListener('error', () => {
    if (active) {
      status.textContent = '视频加载失败，请切换片段或返回首页';
      root.classList.remove('needs-play');
    }
  });
  document.addEventListener('keydown', event => { if (active && event.key === 'Escape') close(); });
  window.choSirenCharacter = { play: playFromCharacter, close, get active() { return active; } };
})();

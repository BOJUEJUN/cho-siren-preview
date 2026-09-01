const lobby = document.querySelector('#lobby');
const animatedCharacter = document.querySelector('.hero-animation');

if (lobby && animatedCharacter) {
  const reveal = () => lobby.classList.add('motion-ready');
  const hide = () => lobby.classList.remove('motion-ready');
  const load = () => {
    if (document.documentElement.classList.contains('motion-disabled')) {
      hide();
      return;
    }
    if (!animatedCharacter.getAttribute('src')) {
      animatedCharacter.src = animatedCharacter.dataset.src;
    }
    if (animatedCharacter.complete && animatedCharacter.naturalWidth) reveal();
  };

  animatedCharacter.addEventListener('load', reveal);
  animatedCharacter.addEventListener('error', hide);
  window.addEventListener('cho-siren-preferences', load);
  load();
}

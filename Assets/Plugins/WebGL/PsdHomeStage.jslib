mergeInto(LibraryManager.library, {
  ChoSirenCharacterMomentStop: function () {
    if (window.choSirenCharacter) window.choSirenCharacter.close();
    if (window.speechSynthesis) window.speechSynthesis.cancel();
  },
  ChoSirenHomeStageClick: function (action, x, y) {
    if (window.choSirenStage) window.choSirenStage.playClick(UTF8ToString(action), x, y);
  },
  ChoSirenCharacterCurrentClip: function () {
    return window.choSirenCharacter ? window.choSirenCharacter.currentClip + 1 : 0;
  },
  ChoSirenCharacterIdle: function (enabled) {
    try {
      if (window.choSirenCharacter && window.choSirenCharacter.setIdle) window.choSirenCharacter.setIdle(!!enabled);
    } catch (_) {}
  },
  // Width and height of the current clip packed as width * 65536 + height; 0 when none.
  ChoSirenCharacterClipSize: function () {
    var size = window.choSirenCharacter && window.choSirenCharacter.clipSize;
    return size ? size.width * 65536 + size.height : 0;
  },
  // 1 when the current clip is a green-screen copy framed on the lobby portrait.
  ChoSirenCharacterClipKeyed: function () {
    var size = window.choSirenCharacter && window.choSirenCharacter.clipSize;
    return size && size.keyed ? 1 : 0;
  },
  ChoSirenCharacterUploadFrame: function (textureId) {
    if (!window.choSirenCharacter || !GL.textures[textureId]) return 0;
    return window.choSirenCharacter.uploadFrame(GLctx, GL.textures[textureId]) ? 1 : 0;
  },
  ChoSirenHomeStageActive: function (active) {
    try {
      if (window.choSirenStage) window.choSirenStage.setActive(!!active);
    } catch (_) {}
  },
  ChoSirenHomeStageUiHidden: function (hidden) {
    try {
      if (window.choSirenStage) window.choSirenStage.setUiHidden(!!hidden);
    } catch (_) {}
  },
  ChoSirenCharacterMomentPlay: function (index, audioEnabled) {
    try {
      return window.choSirenCharacter && window.choSirenCharacter.play(index, !!audioEnabled) ? 1 : 0;
    } catch (_) { return 0; }
  }
});

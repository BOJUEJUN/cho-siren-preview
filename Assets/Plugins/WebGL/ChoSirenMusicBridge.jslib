mergeInto(LibraryManager.library, {
  ChoSirenMusicBridgeHasMediaOwner: function () {
    return window.choSirenAudio && window.choSirenAudio.musicState.mediaOwnsMusic ? 1 : 0;
  },
  ChoSirenMusicBridgeBind: function (name, enabled, hasSoundtrack) {
    if (window.choSirenAudio && window.choSirenAudio.bindMusicBridge)
      window.choSirenAudio.bindMusicBridge(UTF8ToString(name), !!enabled, !!hasSoundtrack);
  },
  ChoSirenMusicBridgeSync: function (enabled) {
    if (window.choSirenAudio && window.choSirenAudio.syncMusicEnabled)
      window.choSirenAudio.syncMusicEnabled(!!enabled);
  },
  ChoSirenMusicBridgeUnbind: function (name) {
    if (window.choSirenAudio && window.choSirenAudio.unbindMusicBridge)
      window.choSirenAudio.unbindMusicBridge(UTF8ToString(name));
  }
});

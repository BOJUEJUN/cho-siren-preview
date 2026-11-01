mergeInto(LibraryManager.library, {
  ChoSirenStartupReport: function (type, progress, message) {
    if (window.choSirenStartup && window.choSirenStartup.report)
      window.choSirenStartup.report(UTF8ToString(type), progress, UTF8ToString(message));
  }
});

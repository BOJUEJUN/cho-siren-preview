mergeInto(LibraryManager.library, {
  // 2026-10-21 save transfer: copy / download / paste the save code (called from a tap).
  ChoSirenCopyText: function (pointer) {
    var text = UTF8ToString(pointer);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(text); return 1; }
    } catch (_) {}
    try {
      var area = document.createElement('textarea');
      area.value = text; area.style.position = 'fixed'; area.style.opacity = '0';
      document.body.appendChild(area); area.select();
      var ok = document.execCommand('copy');
      document.body.removeChild(area);
      return ok ? 1 : 0;
    } catch (_) { return 0; }
  },
  ChoSirenDownloadText: function (namePointer, pointer) {
    try {
      var blob = new Blob([UTF8ToString(pointer)], { type: 'text/plain' });
      var link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = UTF8ToString(namePointer);
      document.body.appendChild(link); link.click();
      setTimeout(function () { URL.revokeObjectURL(link.href); link.remove(); }, 1500);
      return 1;
    } catch (_) { return 0; }
  },
  ChoSirenPromptText: function (messagePointer) {
    var answer = '';
    try { answer = window.prompt(UTF8ToString(messagePointer), '') || ''; } catch (_) {}
    var size = lengthBytesUTF8(answer) + 1;
    var buffer = _malloc(size);
    stringToUTF8(answer, buffer, size);
    return buffer;
  }
});

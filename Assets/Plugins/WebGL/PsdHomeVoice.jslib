mergeInto(LibraryManager.library, {
  ChoSirenSpeakJapanese: function (pointer) {
    try {
      var synth = window.speechSynthesis;
      if (!synth || !window.SpeechSynthesisUtterance) return;
      var line = UTF8ToString(pointer);
      // 2026-10-28 (少爷: 首页点击后是男声): the first Japanese voice is often male (macOS Eddy/Otoya,
      // Windows Ichiro), so only a known female voice — or an unknown one that is not a known male — may speak.
      var female = /kyoko|o-?ren|flo|sandy|shelley|grandma|haruka|ayumi|sayaka|nanami|aoi|mayu|shiori|google|\bfemale\b/i;
      var male = /otoya|hattori|eddy|reed|rocko|grandpa|ichiro|keita|daichi|naoki|\bmale\b/i;
      var pick = function () {
        var japanese = synth.getVoices().filter(function (voice) { return /^ja[-_]/i.test(voice.lang); });
        var women = japanese.filter(function (voice) { return female.test(voice.name) && !male.test(voice.name); });
        if (women.length) return women[0];
        var unknown = japanese.filter(function (voice) { return !male.test(voice.name); });
        return unknown.length ? unknown[0] : null;
      };
      var say = function (voice) {
        // Only male Japanese voices on this device: stay silent rather than speak in the wrong voice.
        if (!voice) return;
        synth.cancel();
        var phrase = new SpeechSynthesisUtterance(line);
        phrase.lang = voice.lang;
        phrase.voice = voice;
        phrase.rate = 1.08;
        phrase.pitch = 1.17;
        phrase.volume = 0.72;
        synth.speak(phrase);
      };
      if (synth.getVoices().length) { say(pick()); return; }
      // The voice list loads asynchronously the first time; speak once it arrives instead of on the default voice.
      var ready = function () { synth.removeEventListener('voiceschanged', ready); say(pick()); };
      synth.addEventListener('voiceschanged', ready);
    } catch (_) {}
  }
});

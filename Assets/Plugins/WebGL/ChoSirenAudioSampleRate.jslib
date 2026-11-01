// Unity 6000.6 clears channel.soundClip when pausing an AudioBufferSourceNode.
// FakeMOD still queries this channel while seeking and recalculating pitch.
// Preserve the SDK lookup order; read the paused mock's real buffer rate before
// falling back to 44100, otherwise a 48 kHz clip resumes at 48000 / 44100 speed.
mergeInto(LibraryManager.library, {
  JS_Sound_GetAudioBufferSampleRate__proxy: 'sync',
  JS_Sound_GetAudioBufferSampleRate__sig: 'ii',
  JS_Sound_GetAudioBufferSampleRate__deps: ['$WEBAudio'],
  JS_Sound_GetAudioBufferSampleRate: function (soundInstance) {
    if (WEBAudio.audioWebEnabled == 0)
      return WEBAudio.FAKEMOD_SAMPLERATE;

    var audioInstance = WEBAudio.audioInstances[soundInstance];
    if (!audioInstance)
      return WEBAudio.FAKEMOD_SAMPLERATE;

    // Original SDK: a SoundClip owns its frequency.
    if (typeof audioInstance.getFrequency === 'function')
      return audioInstance.getFrequency();

    // Original SDK: a playing Channel delegates to its SoundClip.
    if (audioInstance.soundClip)
      return audioInstance.soundClip.getFrequency();

    // A paused Channel has no soundClip but retains the decoded AudioBuffer.
    var source = audioInstance.source;
    if (source && source.isPausedMockNode && source.buffer && source.buffer.sampleRate > 0)
      return source.buffer.sampleRate;

    return WEBAudio.FAKEMOD_SAMPLERATE;
  }
});

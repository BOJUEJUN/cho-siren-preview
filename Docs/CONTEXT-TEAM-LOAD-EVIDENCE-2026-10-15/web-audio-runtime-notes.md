# Web audio runtime investigation — 2026-10-15

Status: asynchronous decoding now succeeds in the real browser. Home resume still runs at `1.088435411453247` in the v3 and v4 evidence below. The v5 `Stop()` change is awaiting the main agent's browser verification; this document does **not** establish that the speed issue is fixed.

Only this document was changed for this evidence-recording task. No Unity process or browser was started by the documenting agent.

## Actual runtime evidence

Evidence directory: `/Users/bojuejun/Documents/Codex/2026-10-15/new-chat/work/context-music-20261015/qa`.

| Build / observation | Evidence file | Active source / offset | Actual playbackRate | Decoded buffer |
| --- | --- | --- | --- | --- |
| v3 first playback | `music-home.json` | id 2 / 0 s | 1 | 172.3335 s, stereo, 48000 Hz, 8272008 frames |
| v3 resumed playback, before the front-loaded seek change | `music-home-resumed.json` | id 7 / 20.832 s | 1.088435411453247 | Same buffer format |
| v4 first playback | `music-home-first-final.json` | id 2 / 0 s | 1 | Same buffer format |
| v4 resumed playback, bookmark set before `Play()` | `music-home-resumed-final.json` | id 5 / 19.482666666666667 s | 1.088435411453247 | Same buffer format |
| v5, `Stop()` before clearing released Web clip | No browser result yet | Pending | Pending | Pending |

The v3 resume record contains starts at id 5 offset 0/rate 1, id 6 offset 20.832/rate 1, then the active id 7 at the same offset/rate 1.088435411453247. The v4 record similarly contains id 3 offset 0/rate 1, id 4 bookmarked offset/rate 1, and active id 5 bookmarked offset/rate 1.088435411453247.

These rates are actual `AudioBufferSourceNode.playbackRate.value` readings, not estimates based on elapsed time. Each active Home source reaches the output with gain approximately 0.18. Output-graph measurements establish a digital signal, not physical speaker audibility.

The ratio matches `48000 / 44100` to float precision. Correct offset alone therefore does not establish correct music playback. The pre-seek change did not fix the observed rate.

## Unity SDK JavaScript evidence

Installed SDK source:

`/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/lib/Audio.js`

Verified line references in that installed file:

- Line 10: `FAKEMOD_SAMPLERATE: 44100`.
- Lines 335–342: an uncompressed sound clip returns length 0 while its decoded `buffer` is absent.
- Lines 460–473: `jsAudioCreateUncompressedSoundClipFromCompressedAudio` returns immediately and fills `soundClip.buffer` in the asynchronous `decodeAudioData` success callback; the error callback sets `soundClip.error`.
- Line 1082: Chromium forces `decompress = 1` to avoid retained `MediaElementAudioSourceNode` objects. `compressed=true` does not guarantee a compressed playback path on Chrome.
- Lines 1327–1337: `JS_Sound_GetLoadState` distinguishes absent decoded data from a loaded buffer and errors. HTTP completion alone is insufficient.
- Lines 553–573: `channel.stop(0)` clears `this.soundClip` and disconnects the old source.
- Lines 600–638: `channel.pause()` copies position, rate, loop bounds and the decoded buffer into a paused mock; it calls `this.stop(0)`, which clears the channel's `soundClip` reference.
- Lines 643–675: `channel.resume()` creates a new source from the saved buffer and restores the paused source's rate. It does not itself multiply the rate by `48000/44100`.
- Lines 732–740 and 1245–1252: the channel/JS pitch functions apply the pitch value supplied by the native side.
- Lines 1429–1449: `JS_Sound_GetAudioBufferSampleRate(soundInstance)` reads a sound clip's frequency, or a channel's current `soundClip` frequency; absent references fall back to 44100.

Thus a paused channel can have a valid paused buffer while its sample-rate lookup still returns the fallback. The browser observations and the native call sequence below are consistent with stale paused-channel state. A complete runtime trace of the native arguments has not been captured, so that final causal connection remains a diagnosis to verify with v5.

## FakeMOD object-code evidence

Archive:

`/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/lib/modules_wasm23/WebGLSupport_UnityPlayer.AudioModule_Dynamic.a`

Relevant objects:

- `PlatformDependent_WebGL_Source_Fakemod_0_xq6ye.o`
- `Modules_Audio_Public_1_xq6ye.o`

Tools used, read-only, with output to stdout:

- `/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/llvm/llvm-nm`
- `/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/llvm/llvm-ar`
- `/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/llvm/llvm-objdump`

Reproduction commands (do not build or modify files):

```sh
cho_llvm_dir=/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/llvm
cho_audio_archive=/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/lib/modules_wasm23/WebGLSupport_UnityPlayer.AudioModule_Dynamic.a

"$cho_llvm_dir/llvm-ar" p "$cho_audio_archive" PlatformDependent_WebGL_Source_Fakemod_0_xq6ye.o |
  "$cho_llvm_dir/llvm-objdump" -d --disassemble-symbols=_ZN4FMOD8ChannelI11UpdatePitchEv,_ZN4FMOD8ChannelI11setPositionEjj -

"$cho_llvm_dir/llvm-ar" p "$cho_audio_archive" PlatformDependent_WebGL_Source_Fakemod_0_xq6ye.o |
  "$cho_llvm_dir/llvm-objdump" -r -

"$cho_llvm_dir/llvm-ar" p "$cho_audio_archive" Modules_Audio_Public_1_xq6ye.o |
  "$cho_llvm_dir/llvm-objdump" -d --disassemble-symbols=_ZN11AudioSource14SetSecPositionEf -
```

`FMOD::ChannelI::UpdatePitch()` (`_ZN4FMOD8ChannelI11UpdatePitchEv`, address `0x4818`):

```text
0x4828  f32.load 164
0x482e  f32.load 176
0x4832  f32.mul
0x4835  call JS_Sound_GetAudioBufferSampleRate
0x483c  f32.div
0x483e  call JS_Sound_SetPitch
```

The formula is native channel frequency times its multiplier, divided by the JavaScript channel's reported buffer sample rate. A 48000 numerator and 44100 fallback denominator produce the observed rate.

`FMOD::ChannelI::setPosition(unsigned int, unsigned int)` (`_ZN4FMOD8ChannelI11setPositionEjj`, address `0x499e`) contains this order:

```text
0x4b46  call JS_Sound_Play
0x4b50  i32.load8_u 185       # existing paused-state field
0x4b6c  call JS_Sound_SetPaused
0x4bbf  f32.load 164
0x4bc5  f32.load 176
0x4bc9  f32.mul
0x4bcc  call JS_Sound_GetAudioBufferSampleRate
0x4bd3  f32.div
0x4bd5  call JS_Sound_SetPitch
```

Relocation entries identify the external calls at offsets `0x4b47`, `0x4b6d`, `0x4bcd`, and `0x4bd6` respectively. Disassembly instruction offsets above precede the relocation operand by one byte.

`AudioSource::SetSecPosition(float)` (`_ZN11AudioSource14SetSecPositionEf`, address `0xf302`) multiplies seconds by clip frequency at `0xf440–0xf44c`, applies the PCM position to an existing channel when one exists at `0xf452–0xf471`, and stores the position at member offset 792 (`0xf473–0xf477`). This supports setting an initial position before playback when no old channel remains. The v4 result demonstrates that merely moving the C# assignment before `Play()` did not establish that clean-channel condition.

## Current changes and verification gate

Private source: `/Users/bojuejun/Documents/Codex/2026-10-15/new-chat/work/context-music-20261015/project/Assets/Scripts/GameAudio.cs`.

- Web downloads explicitly use `streamAudio=false`, `compressed=false`; the pending clip remains owned until Loaded plus valid metadata, with a 20-second decode limit. Request identity, current context, music switch, media ownership and foreground state are checked between yields. Cancellation releases both the request and pending clip.
- Bookmarks use float seconds keyed by Home/Battle. They are updated only while `musicSource.isPlaying`, preventing repeated paused `ApplySettings()` calls from overwriting a saved position with 0. Unity 6.6 documents `AudioSource.time` returning 0 while not playing: [AudioSource.time](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AudioSource-time.html).
- v4 sets the Web bookmark before `Play()`; native/Editor playback order remains unchanged.
- v5 additionally executes `musicSource.Stop()` after capturing/pausing the old Web clip and before clearing `source.clip`. This is limited to releasing an external Web clip. Same-clip CG/music-switch/background interruption still uses Pause/UnPause. The v5 change is a candidate fix, pending browser evidence.

Acceptance requires real Home → silent menu → Home playback with the saved offset **and rate 1**, followed by Battle and CG interruption checks: correct track/context, no forbidden vocal leakage, no overlapping long sources, correct gain, and rate 1 after resuming. No change to MP3 encoding or global Web Audio/SFX code was made for the speed issue.

## Final v6 validation

The precise paused-buffer sample-rate interop patch is compiled into framework cec6028803ef98d0bbf52e649f5a2d82. Real Chrome Home resumed at offset 4.8266666667 seconds, playbackRate 1, gain 0.18; Battle resumed at offset 33.632 seconds, playbackRate 1, gain 0.055. Muting, routing while muted and reenabling retained a 50.0746666667 second bookmark at rate 1. See music-*-v6.json. The eleven ABI/lookup regression tests passed. v5 Stop alone did not fix the speed issue.

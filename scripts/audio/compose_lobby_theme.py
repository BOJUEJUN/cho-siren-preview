"""Lobby theme and UI sounds for CHO-SIREN 幻域魅声 — original, rendered from code.

Theme: synth-pop loop in A major at 120 BPM, 32 bars (64 s): pads and arpeggio, then
bass and drums, then a lead melody with electric-piano comping. It replaces Unity's
8-second generated placeholder loop at runtime (audio-skin.js), so it keeps the same
key and tempo. The file holds the 64 s loop followed by its first 2 s again, which
lets the player loop seamlessly whatever start offset the AAC decoder reports.

Also renders the two UI sounds Unity generates: a 75 ms click and a rising
"success" arpeggio.

Run from the repository root:  python3 scripts/audio/compose_lobby_theme.py
Needs numpy and ffmpeg (libmp3lame). Deterministic (fixed random seed).
"""
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
from synth import SR, Mixer, bell, clap, convolve, filt2, kick, midi, osc, pad_note, pluck, reverb_ir, rng, stereo_noise, svf, tt  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MEDIA = ROOT / "media"
BPM = 120
BEAT = 60 / BPM
BAR = 4 * BEAT
BARS = 32
LOOP = BARS * BAR          # 64 s
TAIL = 6.0                 # rendered past the loop end, folded back onto the start
mx = Mixer(LOOP + TAIL)
P = mx.place

# One chord per bar, eight-bar phrase: (bass note, pad voicing).
CHORDS = [
    (45, [61, 64, 68, 71]),   # Amaj9
    (42, [57, 61, 64, 68]),   # F#m9
    (38, [54, 57, 61, 64]),   # Dmaj9
    (40, [56, 59, 61, 66]),   # E6/9
    (45, [61, 64, 68, 71]),   # Amaj9
    (37, [56, 59, 64, 68]),   # C#m7
    (38, [54, 57, 61, 64]),   # Dmaj9
    (40, [56, 59, 62, 66]),   # E9
]
A_MAJOR = [57, 59, 61, 62, 64, 66, 68]   # A B C# D E F# G#


def bar_t(bar, beat=0.0):
    return bar * BAR + beat * BEAT


# ---------------------------------------------------------------- one-shots
KICK = [kick(1.0) * 0.9 for _ in range(2)]
CLAP = [clap(1.0) for _ in range(3)]


def closed_hat():
    n = int(0.05 * SR)
    t = np.arange(n) / SR
    return filt2(stereo_noise(n), 9000, 0.8, "hp") * np.exp(-t * 85)


def open_hat():
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    return filt2(stereo_noise(n), 7500, 0.8, "hp") * np.exp(-t * 16)


HATS = [closed_hat() for _ in range(4)]
OHATS = [open_hat() for _ in range(2)]


def bass_note(m, dur, accent=1.0):
    f = midi(m)
    t = tt(dur + 0.03)
    saw = sum(osc(f * h, t) / h for h in range(1, 8))
    body = svf(saw, 380 + 900 * np.exp(-t * 18), 0.9) * 0.55
    sub = osc(f, t) * 0.5
    env = np.minimum(1, t / 0.004) * np.clip((dur + 0.03 - t) / 0.03, 0, 1) * (0.75 + 0.25 * np.exp(-t * 8))
    return np.tanh(1.3 * (body + sub)) * env * accent


def lead_note(m, dur):
    f = midi(m)
    t = tt(dur + 0.25)
    vib = 1 + 0.0065 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.16) / 0.25, 0, 1)
    out = np.zeros((2, len(t)))
    for side, cents in enumerate((-6, 6)):
        fr = f * 2 ** (cents / 1200) * vib
        tone = sum(osc(fr * h, t, rng.uniform(0, 6.28)) * (1 / h) * (0.85 ** h) for h in range(1, 9))
        tone += 0.45 * osc(fr / 2, t)
        out[side] = svf(tone, 2400 + 1600 * np.exp(-t * 6), 0.8)
    env = np.minimum(1, t / 0.012) * np.clip((dur + 0.25 - t) / 0.25, 0, 1) ** 1.6
    return out * env * 0.5


def ep_note(m, dur):
    f = midi(m)
    t = tt(dur)
    tone = (osc(f, t) * np.exp(-t * 2.4) + 0.32 * osc(2 * f, t) * np.exp(-t * 4.5)
            + 0.09 * osc(4.02 * f, t) * np.exp(-t * 11))
    tine = osc(f * 7.1, t) * np.exp(-t * 60) * 0.05
    env = np.minimum(1, t / 0.003) * np.clip((dur - t) / 0.08, 0, 1)
    return (tone + tine) * env


# ---------------------------------------------------------------- arrangement
kicks = []
for bar in range(BARS):
    section = bar // 8
    bass_m, voicing = CHORDS[bar % 8]
    tb = bar_t(bar)

    # Pads: every bar, softer in the opening section.
    for m in voicing:
        P(pad_note(midi(m), BAR + 0.45, att=0.35, rel=0.55, bright=0.35), tb, 0.06,
          rev=0.45, bus="pad")

    # Arpeggio: eighths in the opening, sixteenths after; two octaves up and down.
    tones = voicing + [v + 12 for v in voicing]
    order = tones + tones[-2:0:-1]
    step = 0.5 if section == 0 else 0.25
    for i in range(int(4 / step)):
        m = order[i % len(order)]
        P(pluck(midi(m), 0.32, decay=0.75), tb + i * step * BEAT, 0.07 if section else 0.075,
          rev=0.2, pan=0.35 if i % 2 else -0.35, bus="arp")

    # Drums.
    if section == 0:
        kicks.append(tb)
        P(KICK[0], tb, 0.42, bus="drums")
        if bar >= 4:
            for b in (1, 3):
                P(CLAP[b % 3], bar_t(bar, b), 0.3, rev=0.18, bus="drums")
        for e in range(8):
            P(HATS[e % 4], bar_t(bar, e / 2), 0.12 if e % 2 else 0.075, bus="drums")
    else:
        for b in (0, 2):
            kicks.append(bar_t(bar, b))
            P(KICK[b // 2], bar_t(bar, b), 0.6, bus="drums")
        if bar % 2:
            kicks.append(bar_t(bar, 2.75))
            P(KICK[1], bar_t(bar, 2.75), 0.36, bus="drums")
        for b in (1, 3):
            P(CLAP[(bar + b) % 3], bar_t(bar, b), 0.46, rev=0.2, bus="drums")
        for s in range(16):
            P(HATS[s % 4], bar_t(bar, s / 4), 0.105 if s % 2 == 0 else 0.055, pan=0.15, bus="drums")
        for b in range(4):
            P(OHATS[b % 2], bar_t(bar, b + 0.5), 0.065, pan=-0.15, bus="drums")

    # Bass: a soft sustained root through the opening, then a driving eighth-note line.
    if section == 0:
        P(bass_note(bass_m, BAR * 0.95, 0.55), tb, 0.36, bus="bass")
    elif section > 0:
        for e, (off, interval) in enumerate([(0, 0), (0.5, 0), (1, 12), (1.5, 0),
                                             (2, 0), (2.5, 0), (3, 12), (3.5, 7)]):
            P(bass_note(bass_m + interval, 0.21, 1.0 if e % 2 == 0 else 0.72), bar_t(bar, off), 0.36, bus="bass")

    # Electric-piano offbeat stabs under the melody.
    if section >= 2:
        for off in (1.5, 3.5):
            for k, m in enumerate(voicing):
                P(ep_note(m, 0.42), bar_t(bar, off), 0.048, rev=0.3, pan=-0.2 + 0.13 * k, bus="ep")

# Last bar: clap fill, then a soft reverse swell into the loop start.
for s in range(4):
    P(CLAP[s % 3], bar_t(31, 3 + s / 4), 0.16 + 0.07 * s, rev=0.2, bus="drums")
n = int(BAR * SR)
swell = filt2(stereo_noise(n), 5000, 0.6, "lp") * (np.arange(n) / n) ** 3
P(swell, bar_t(31), 0.035, rev=0.3, bus="drums")

# ---------------------------------------------------------------- melody
MELODY_C = [
    [(0, 76, 1), (1, 73, .5), (1.5, 76, .5), (2, 78, 1), (3, 76, 1)],
    [(0, 73, 1.5), (1.5, 71, .5), (2, 69, 1), (3, 73, 1)],
    [(0, 69, .5), (.5, 71, .5), (1, 73, 1), (2, 76, 1.5), (3.5, 78, .5)],
    [(0, 80, 1.5), (1.5, 78, .5), (2, 76, 2)],
    [(0, 76, 1), (1, 73, .5), (1.5, 76, .5), (2, 81, 1.5), (3.5, 80, .5)],
    [(0, 80, 1), (1, 76, 1), (2, 71, 1), (3, 73, 1)],
    [(0, 78, 1), (1, 76, .5), (1.5, 73, .5), (2, 71, 1), (3, 69, 1)],
    [(0, 71, 1.5), (1.5, 73, .5), (2, 71, .5), (2.5, 68, 1.5)],
]
MELODY_D_END = [
    [(0, 76, 1), (1, 73, .5), (1.5, 71, .5), (2, 69, 1), (3, 73, 1)],
    [(0, 76, 1.5), (1.5, 80, .5), (2, 78, 1), (3, 76, 1)],
    [(0, 78, 1), (1, 81, 1), (2, 80, 1), (3, 76, 1)],
    [(0, 78, 1.5), (1.5, 76, .5), (2, 71, 2)],
]


def third_below(m):
    """Diatonic third below in A major (octave-aware)."""
    octave, pc = divmod(m - 57, 12)
    scale = [s - 57 for s in A_MAJOR]
    idx = min(range(7), key=lambda i: abs(scale[i] - pc))
    j = idx - 2
    return 57 + 12 * (octave + (j // 7)) + scale[j % 7]


def play_line(bar, line, gain=0.17, harmony=False):
    for off, m, beats in line:
        dur = beats * BEAT * 0.94
        P(lead_note(m, dur), bar_t(bar, off), gain, rev=0.28, bus="lead")
        if harmony:
            P(lead_note(third_below(m), dur), bar_t(bar, off), gain * 0.45, rev=0.32, pan=0.0, bus="lead")


for i, line in enumerate(MELODY_C):
    play_line(16 + i, line)
for i, line in enumerate(MELODY_C[:4]):
    play_line(24 + i, line, harmony=True)
for i, line in enumerate(MELODY_D_END):
    play_line(28 + i, line, harmony=i < 3)

# ---------------------------------------------------------------- mix
L = mx.n
t_all = np.arange(L) / SR


def sidechain(depth, tau=0.11):
    g = np.ones(L)
    for tk in kicks + [k + LOOP for k in kicks if k < TAIL]:
        i0 = int(tk * SR)
        if i0 >= L:
            continue
        seg = t_all[i0:] - tk
        g[i0:] = np.minimum(g[i0:], 1 - depth * np.exp(-seg / tau))
    return g


def pingpong(x, delay, fb, mix):
    d = int(delay * SR)
    mono = x.mean(0)
    wet = np.zeros_like(x)
    for k in range(1, 7):
        if k * d >= L:
            break
        wet[k % 2, k * d:] += mono[:L - k * d] * fb ** (k - 1)
    f = np.fft.rfftfreq(L, 1 / SR)
    lp = 1 / (1 + (f / 3500) ** 2)
    wet = np.stack([np.fft.irfft(np.fft.rfft(wet[c]) * lp, L) for c in range(2)])
    return wet * mix


def bus(name):
    return mx.buses.get(name, [np.zeros((2, L)), np.zeros((2, L))])


duck_soft, duck_bass = sidechain(0.3), sidechain(0.5)
dry = np.zeros((2, L))
send = np.zeros((2, L))
for name, duck in (("pad", duck_soft), ("arp", duck_soft), ("ep", duck_soft), ("bass", duck_bass),
                   ("drums", 1.0), ("lead", 1.0)):
    d, s = bus(name)
    dry += d * duck
    send += s * duck
arp_d, _ = bus("arp")
lead_d, _ = bus("lead")
dry += pingpong(arp_d * duck_soft, 0.375, 0.42, 0.55) + pingpong(lead_d, 0.375, 0.3, 0.28)
mix = dry + 0.55 * convolve(send, reverb_ir(seconds=2.6, rt_lo=2.1, rt_hi=1.0))

# Fold everything rendered past the loop end back onto its start, then keep 64 s.
n_loop = int(round(LOOP * SR))
body = mix[:, :n_loop].copy()
over = mix[:, n_loop:]
body[:, :over.shape[1]] += over

# Gentle glue compression, high-pass, then level to background loudness.
f = np.fft.rfftfreq(n_loop, 1 / SR)
hp = 1 / (1 + (35 / np.maximum(f, 1)) ** 4)
# Gentle tilt for small speakers: up to +4 dB of presence above ~2 kHz, a little less sub.
hp *= (1 + 0.6 * (f / 2000) ** 2 / (1 + (f / 2000) ** 2)) / (1 + 0.25 / (1 + (f / 90) ** 2))
body = np.stack([np.fft.irfft(np.fft.rfft(body[c]) * hp, n_loop) for c in range(2)])
win = int(0.03 * SR)
power = np.convolve(np.mean(body ** 2, axis=0), np.ones(win) / win, mode="same")
rms = np.sqrt(np.maximum(power, 1e-12))
thr = np.percentile(rms, 70)
gain = np.where(rms > thr, (thr / rms) ** (1 - 1 / 2.2), 1.0)
gain = np.convolve(gain, np.ones(int(0.02 * SR)) / int(0.02 * SR), mode="same")
body *= gain
body *= 0.068 / np.sqrt(np.mean(body ** 2))          # ~Unity's placeholder level (RMS 0.059)
body = np.tanh(body * 2.2) / 2.2
print(f"theme: rms {np.sqrt(np.mean(body ** 2)):.3f}, peak {np.abs(body).max():.3f}")

theme = np.concatenate([body, body[:, :2 * SR]], axis=1)   # loop + first 2 s again


def write_wav(path, x):
    x = np.atleast_2d(x)
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16).T.copy()
    with wave.open(str(path), "wb") as w:
        w.setnchannels(x.shape[0])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


tmp = ROOT / "scripts" / "audio" / ".lobby-theme.wav"
write_wav(tmp, theme)
# MP3 decodes in every browser's Web Audio (AAC is missing from open-source Chromium builds).
subprocess.run(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", str(tmp), "-c:a", "libmp3lame", "-b:a", "192k",
                str(MEDIA / "lobby-theme.mp3")], check=True)
tmp.unlink()

# ---------------------------------------------------------------- UI sounds
t = tt(0.075)
click = (osc(1850, t) * np.exp(-t * 95) + 0.45 * osc(3720, t) * np.exp(-t * 150)
         + 0.4 * osc(430, t) * np.exp(-t * 70))
click += svf(rng.standard_normal(len(t)), 3500, 0.7, "hp") * np.exp(-t * 700) * 0.25
click *= np.minimum(1, t / 0.0008) * np.clip((0.075 - t) / 0.01, 0, 1)
click *= 0.11 / np.abs(click).max()            # Unity plays this sound twice at once
write_wav(MEDIA / "ui-click.wav", click)

# Same length as Unity's 0.34 s arpeggio: Unity times the voice by its own clip length.
SUCCESS = 0.34
success = np.zeros(int(SUCCESS * SR))
for i, m in enumerate((81, 85, 88)):           # A5 C#6 E6
    n_i = len(success) - int(i * 0.055 * SR)
    ti = np.arange(n_i) / SR
    tone = bell(midi(m), (n_i + 2) / SR, decay=0.32)[:n_i] + 0.22 * osc(midi(m + 12), ti) * np.exp(-ti * 14)
    k = int(i * 0.055 * SR)
    success[k:k + n_i] += tone[:n_i] * (0.8 + 0.12 * i)
ts = np.arange(len(success)) / SR
success *= np.clip((SUCCESS - ts) / 0.06, 0, 1)
success *= 0.22 / np.abs(success).max()
write_wav(MEDIA / "ui-success.wav", success)
for name in ("lobby-theme.mp3", "ui-click.wav", "ui-success.wav"):
    print(name, (MEDIA / name).stat().st_size, "bytes")

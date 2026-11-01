#!/usr/bin/env python3
"""Voice post-processing (CHO-SIREN 2026-10-31): decode MiniMax MP3 -> trim silence -> optional
internal-pause tightening -> loudness match -> 44.1 kHz mono PCM16 WAV.

No pitch/time-stretch of the performance. Pauses longer than `max_gap` seconds (the "…" in the
script) are shortened by cutting silence only, with 15 ms crossfades.
Enemy voices may get a light character FX chain (eq/chorus/reverb) via `fx`.
"""
import sys
import numpy as np
import soundfile as sf
from math import gcd
from scipy.signal import resample_poly
sys.path.insert(0, '/Users/bojuejun/cho-siren-unity/Tools/audio')
from loudnorm import lufs, limit  # noqa: E402

SR = 44100


def load(path):
    x, sr = sf.read(path, always_2d=True, dtype='float64')
    x = x.mean(1)
    if sr != SR:
        g = gcd(SR, sr)
        x = resample_poly(x, SR // g, sr // g)
    return x


def frames_db(x, win=.01):
    n = int(win * SR)
    m = len(x) // n
    f = x[:m * n].reshape(m, n)
    return 20 * np.log10(np.sqrt((f ** 2).mean(1)) + 1e-12), n


def trim(x, rel_db=-42, pad_in=.012, pad_out=.06):
    db, n = frames_db(x)
    th = db.max() + rel_db
    on = np.where(db > th)[0]
    if len(on) == 0:
        return x
    a = max(0, on[0] * n - int(pad_in * SR))
    b = min(len(x), (on[-1] + 1) * n + int(pad_out * SR))
    y = x[a:b].copy()
    k = int(.004 * SR)
    y[:k] *= np.linspace(0, 1, k)
    k = int(.04 * SR)
    y[-k:] *= np.linspace(1, 0, k) ** 2
    return y


def tighten(x, max_gap=.22, keep=.16, rel_db=-40):
    """Shorten silent gaps longer than max_gap to `keep` seconds (crossfaded)."""
    db, n = frames_db(x)
    th = db.max() + rel_db
    quiet = db < th
    out, i, last = [], 0, 0
    xf = int(.015 * SR)
    while i < len(quiet):
        if quiet[i]:
            j = i
            while j < len(quiet) and quiet[j]:
                j += 1
            if (j - i) * n / SR > max_gap and i > 0 and j < len(quiet):
                cut_a = i * n + int(keep / 2 * SR)
                cut_b = j * n - int(keep / 2 * SR)
                out.append(x[last:cut_a + xf])
                last = cut_b
            i = j
        else:
            i += 1
    out.append(x[last:])
    y = out[0]
    for seg in out[1:]:
        if len(y) > xf and len(seg) > xf:
            r = np.linspace(0, 1, xf)
            y = np.concatenate([y[:-xf], y[-xf:] * (1 - r) + seg[:xf] * r, seg[xf:]])
        else:
            y = np.concatenate([y, seg])
    return y


def fx_chain(x, kind):
    if not kind:
        return x
    import pedalboard as pb
    chains = {
        # drowned: dull + watery chorus + short dark room
        'drowned': [pb.LowpassFilter(5200), pb.Chorus(rate_hz=.9, depth=.18, mix=.28),
                    pb.Reverb(room_size=.35, damping=.7, wet_level=.16, dry_level=.9, width=0)],
        # ghost: thin airy doubling + longer verb
        'ghost': [pb.HighpassFilter(180), pb.Chorus(rate_hz=1.6, depth=.12, mix=.22),
                  pb.Reverb(room_size=.55, damping=.4, wet_level=.18, dry_level=.9, width=0)],
        # berserk diva: grit + slap
        'berserk': [pb.Distortion(drive_db=7), pb.HighShelfFilter(4000, gain_db=2),
                    pb.Delay(delay_seconds=.07, feedback=.05, mix=.12)],
        # queen / singer: hall glamour
        'hall': [pb.Reverb(room_size=.6, damping=.35, wet_level=.14, dry_level=.92, width=0)],
        'room': [pb.Reverb(room_size=.25, damping=.6, wet_level=.08, dry_level=.95, width=0)],
    }
    board = pb.Pedalboard(chains[kind])
    tail = np.zeros(int(.35 * SR))
    y = board(np.concatenate([x, tail]).astype(np.float32)[None, :], SR)[0].astype(np.float64)
    return trim(y, rel_db=-48, pad_in=.012, pad_out=.05)


def loud(x, target=-17.0, ceiling=-1.5):
    pad = x if len(x) >= int(.5 * SR) else np.pad(x, (0, int(.5 * SR) - len(x)))
    g = target - lufs(pad[:, None], SR)
    y = x
    for _ in range(4):
        y = limit((x * 10 ** (g / 20))[:, None], SR, ceiling)[:, 0]
        p2 = y if len(y) >= int(.5 * SR) else np.pad(y, (0, int(.5 * SR) - len(y)))
        now = lufs(p2[:, None], SR)
        if abs(now - target) < .2:
            break
        g += target - now
    return y


def process(src, dst, target=-17.0, max_gap=.22, keep=.16, fx=None):
    x = load(src)
    x = trim(x)
    if max_gap:
        x = tighten(x, max_gap, keep)
    x = fx_chain(x, fx)
    y = loud(x, target)
    sf.write(dst, y.astype(np.float32), SR, subtype='PCM_16')
    return len(y) / SR


if __name__ == '__main__':
    print(process(sys.argv[1], sys.argv[2]))

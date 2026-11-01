#!/usr/bin/env python3
"""Objective one-shot metrics for candidate selection (CHO-SIREN 2026-10-31).

attack_ms  : time from file start to the first sample within 6 dB of the peak envelope (0 latency = good)
lead_ms    : leading silence before the signal rises above -50 dBFS
centroid   : spectral centroid (Hz) of the first 300 ms (brightness)
peak_db / rms_db / crest_db, clip_count (|x| >= .999), lufs (BS.1770 integrated, short files padded)
noise_db   : level of the quietest 10% of 20 ms frames inside the active region (hiss/garbage floor)
tail_db    : level of the last 50 ms (should be near silence: no truncated tails)
"""
import sys
import json
import numpy as np
import soundfile as sf
sys.path.insert(0, '/Users/bojuejun/cho-siren-unity/Tools/audio')
from loudnorm import lufs, true_peak_env  # noqa: E402


def load_mono(path):
    x, sr = sf.read(path, always_2d=True, dtype='float64')
    return x.mean(1), sr, x


def env(x, sr, win=.005):
    n = max(1, int(win * sr))
    return np.sqrt(np.convolve(x * x, np.ones(n) / n, 'same') + 1e-20)


def metrics(path):
    x, sr, raw = load_mono(path)
    if len(x) == 0:
        return {'path': path, 'error': 'empty'}
    e = env(x, sr)
    pk = e.max()
    db = lambda v: 20 * np.log10(max(v, 1e-12))
    above = np.where(e > 10 ** (-50 / 20))[0]
    lead = above[0] / sr if len(above) else 0.0
    attack = np.where(e >= pk * 10 ** (-6 / 20))[0][0] / sr
    a0 = int(lead * sr)
    seg = x[a0:a0 + int(.3 * sr)]
    if len(seg) < 256:
        seg = np.pad(seg, (0, 256 - len(seg)))
    spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg))))
    freqs = np.fft.rfftfreq(len(seg), 1 / sr)
    centroid = float((spec * freqs).sum() / (spec.sum() + 1e-12))
    active = x[a0:]
    fr = int(.02 * sr)
    frames = [np.sqrt(np.mean(active[i:i + fr] ** 2)) for i in range(0, max(1, len(active) - fr), fr)]
    frames = np.array(frames) if frames else np.array([1e-9])
    loud = frames[frames > frames.max() * 10 ** (-40 / 20)]
    noise = np.percentile(loud, 10) if len(loud) else 1e-9
    padded = raw if len(raw) >= int(.5 * sr) else np.pad(raw, ((0, int(.5 * sr) - len(raw)), (0, 0)))
    tp = db(true_peak_env(raw).max())
    return {
        'path': path, 'sr': sr, 'channels': raw.shape[1], 'dur': round(len(x) / sr, 3),
        'lead_ms': round(lead * 1000, 1), 'attack_ms': round(attack * 1000, 1),
        'centroid': round(centroid), 'peak_db': round(db(np.abs(raw).max()), 2), 'tp_db': round(tp, 2),
        'rms_db': round(db(np.sqrt(np.mean(active ** 2))), 2),
        'crest_db': round(db(np.abs(active).max()) - db(np.sqrt(np.mean(active ** 2))), 2),
        'clip_count': int((np.abs(raw) >= .999).sum()), 'lufs': round(lufs(padded, sr), 2),
        'noise_db': round(db(noise), 1), 'tail_db': round(db(np.sqrt(np.mean(x[-int(.05 * sr):] ** 2))), 1),
    }


if __name__ == '__main__':
    for p in sys.argv[1:]:
        print(json.dumps(metrics(p)))

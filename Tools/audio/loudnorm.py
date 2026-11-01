#!/usr/bin/env python3
"""Loudness-normalise music to a target LUFS (ITU-R BS.1770) with a look-ahead true-peak limiter.

Usage (venv with numpy/scipy/soundfile, e.g. ~/cho-siren-audio-src/venv/bin/python):
  loudnorm.py --target -14 --ceiling -1 IN OUT [--measure-only]
WAV keeps its subtype and frame count; MP3 is re-encoded (CBR, ~192 kbps).
"""
import argparse
import numpy as np
import soundfile as sf
from scipy.signal import lfilter, resample_poly
from scipy.ndimage import minimum_filter1d


def _biquads(fs):
    from math import cos, sin, sqrt, pi
    G, Q, fc = 3.99984385397, 0.7071752369554193, 1681.9744509555319
    A = 10 ** (G / 40); w0 = 2 * pi * fc / fs; al = sin(w0) / (2 * Q)
    shelf = ([A * ((A + 1) + (A - 1) * cos(w0) + 2 * sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * cos(w0)),
              A * ((A + 1) + (A - 1) * cos(w0) - 2 * sqrt(A) * al)],
             [(A + 1) - (A - 1) * cos(w0) + 2 * sqrt(A) * al, 2 * ((A - 1) - (A + 1) * cos(w0)),
              (A + 1) - (A - 1) * cos(w0) - 2 * sqrt(A) * al])
    Q, fc = 0.5003270373253953, 38.13547087613982
    w0 = 2 * pi * fc / fs; al = sin(w0) / (2 * Q)
    hp = ([(1 + cos(w0)) / 2, -(1 + cos(w0)), (1 + cos(w0)) / 2], [1 + al, -2 * cos(w0), 1 - al])
    return shelf, hp


def lufs(x, fs):
    shelf, hp = _biquads(fs)
    y = lfilter(*shelf, x, axis=0)
    y = lfilter(*hp, y, axis=0)
    blk, hop = int(.4 * fs), int(.1 * fs)
    starts = range(0, max(1, len(y) - blk + 1), hop)
    z = np.array([np.mean(y[s:s + blk] ** 2, axis=0).sum() for s in starts])
    l = -0.691 + 10 * np.log10(z + 1e-15)
    z1 = z[l > -70]
    if len(z1) == 0: return -99.0
    rel = -0.691 + 10 * np.log10(z1.mean()) - 10
    z2 = z[(l > -70) & (l > rel)]
    return float(-0.691 + 10 * np.log10(z2.mean()))


def true_peak_env(y):
    up = resample_poly(y, 4, 1, axis=0)
    env = np.abs(up).max(axis=1)
    n = len(y)
    return env[:n * 4].reshape(n, 4).max(axis=1)


def limit(y, fs, ceiling_db, lookahead_ms=5.0, release_ms=120.0, block=32):
    c = 10 ** (ceiling_db / 20)
    env = true_peak_env(y)
    need = np.minimum(1.0, c / np.maximum(env, 1e-9))
    n = len(need); nb = (n + block - 1) // block
    pad = np.pad(need, (0, nb * block - n), constant_values=1.0).reshape(nb, block).min(axis=1)
    la = max(1, int(np.ceil(lookahead_ms / 1000 * fs / block)))
    pad = minimum_filter1d(pad, size=2 * la + 1, mode='nearest')  # gain is already down when the peak arrives
    rel = np.exp(-block / (release_ms / 1000 * fs))
    g = np.empty_like(pad); cur = 1.0
    for i, v in enumerate(pad):
        cur = v if v < cur else v + (cur - v) * rel
        g[i] = cur
    gs = np.interp(np.arange(n), np.arange(nb) * block + block / 2, g)
    gs = np.minimum(gs, need)  # hard safety for any residual sample
    return y * gs[:, None]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('dst', nargs='?')
    ap.add_argument('--target', type=float, default=-14.0)
    ap.add_argument('--ceiling', type=float, default=-1.0)
    ap.add_argument('--measure-only', action='store_true')
    a = ap.parse_args()
    info = sf.info(a.src)
    x, fs = sf.read(a.src, dtype='float64', always_2d=True)
    before = lufs(x, fs)
    tp = 20 * np.log10(true_peak_env(x).max() + 1e-12)
    if a.measure_only or not a.dst:
        print(f'{before:6.2f} LUFS  TP {tp:5.2f} dBTP  {a.src}'); return
    gain_db = a.target - before
    for _ in range(4):
        y = limit(x * 10 ** (gain_db / 20), fs, a.ceiling)
        after = lufs(y, fs)
        if abs(after - a.target) < .2: break
        gain_db += a.target - after
    tp2 = 20 * np.log10(true_peak_env(y).max() + 1e-12)
    if a.dst.lower().endswith('.mp3'):
        sf.write(a.dst, y.astype(np.float32), fs, format='MP3', subtype='MPEG_LAYER_III',
                 bitrate_mode='CONSTANT', compression_level=0.4)
    else:
        sf.write(a.dst, y, fs, subtype=info.subtype)
    print(f'{before:6.2f} -> {after:6.2f} LUFS  gain {gain_db:+5.2f} dB  TP {tp:5.2f} -> {tp2:5.2f} dBTP  {a.dst}')


if __name__ == '__main__':
    main()

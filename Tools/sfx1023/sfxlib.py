"""Small layering toolkit for CHO-SIREN battle SFX (pedalboard + numpy). Mono, 48 kHz float."""
import numpy as np, soundfile as sf
from math import gcd
from scipy.signal import resample_poly, butter, sosfilt
import pedalboard as pb

SR = 48000
SON = '/Users/bojuejun/cho-siren-audio-src/sonniss-gdc2026/part1/'
LEN = '/Users/bojuejun/cho-siren-audio-src/lentikula/files/'

def load(path, start=0.0, end=None):
    x, sr = sf.read(path, always_2d=True)
    x = x.mean(1)
    a = int(start * sr); b = int(end * sr) if end else len(x)
    x = x[a:b]
    if sr != SR:
        g = gcd(SR, sr); x = resample_poly(x, SR // g, sr // g)
    return x.astype(np.float32)

def onsets(x, thresh_db=-30, min_gap=.12, win=.005):
    """Start times (s) of separate hits in a multi-take file."""
    n = int(win * SR); e = np.sqrt(np.convolve(x * x, np.ones(n) / n, 'same'))
    pk = e.max(); th = pk * 10 ** (thresh_db / 20)
    out, last = [], -1e9
    above = e > th
    for i in np.where(above[1:] & ~above[:-1])[0]:
        t = i / SR
        if t - last >= min_gap: out.append(t); last = t
    return out

def seg(x, t0, dur, pre=.004):
    a = max(0, int((t0 - pre) * SR)); return x[a:a + int(dur * SR)].copy()

def speed(x, ratio):
    """Varispeed: ratio > 1 = higher and shorter (tape style; keeps transients natural)."""
    if abs(ratio - 1) < 1e-3: return x
    up, down = 1000, int(round(1000 * ratio))
    g = gcd(up, down); return resample_poly(x, up // g, down // g).astype(np.float32)

def fade(x, fin=.002, fout=.08):
    x = x.copy(); n1 = int(fin * SR); n2 = min(len(x), int(fout * SR))
    if n1: x[:n1] *= np.linspace(0, 1, n1)
    if n2: x[-n2:] *= np.linspace(1, 0, n2) ** 2
    return x

def env_decay(x, tau):
    t = np.arange(len(x)) / SR; return x * np.exp(-t / tau)

def hp(x, f, order=2):
    return sosfilt(butter(order, f, 'hp', fs=SR, output='sos'), x).astype(np.float32)

def lp(x, f, order=2):
    return sosfilt(butter(order, f, 'lp', fs=SR, output='sos'), x).astype(np.float32)

def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], 'bp', fs=SR, output='sos'), x).astype(np.float32)

def db(g): return 10 ** (g / 20)

def norm(x, peak_db=-1.0):
    m = np.abs(x).max(); return x * (db(peak_db) / m) if m > 0 else x

# ---- synth layers ----
def sub_drop(f0=140, f1=42, dur=.35, tau=.18):
    t = np.arange(int(dur * SR)) / SR
    f = f1 + (f0 - f1) * np.exp(-t / (dur * .28))
    ph = 2 * np.pi * np.cumsum(f) / SR
    return fade(env_decay(np.sin(ph), tau), .001, .05).astype(np.float32)

def crack(dur=.03, tone=5500, seed=0):
    r = np.random.default_rng(seed); n = r.standard_normal(int(dur * SR))
    return fade(env_decay(hp(n, tone, 2), dur / 4), .0005, .01).astype(np.float32)

def sparkle(freqs=(2637, 3136, 3951, 5274), dur=.6, step=.035, tau=.12, seed=0):
    r = np.random.default_rng(seed); out = np.zeros(int(dur * SR), np.float32)
    for i, f in enumerate(freqs):
        a = int(i * step * SR); t = np.arange(len(out) - a) / SR
        out[a:] += (np.sin(2 * np.pi * f * t) * np.exp(-t / tau) * (0.6 + 0.4 * r.random())).astype(np.float32)
    return fade(out, .001, .1)

def noise_swell(dur=.25, lo=800, hi=6000, seed=1):
    """Rising air whoosh (pre-hit suck-in)."""
    r = np.random.default_rng(seed); n = bp(r.standard_normal(int(dur * SR)), lo, hi)
    t = np.linspace(0, 1, len(n)); return (n * t ** 2.5).astype(np.float32)

def mix(length, *layers):
    """layers: (signal, offset_s, gain_db)"""
    out = np.zeros(int(length * SR), np.float32)
    for s, off, g in layers:
        a = int(off * SR)
        if a >= len(out): continue
        s = s[:len(out) - a]; out[a:a + len(s)] += s * db(g)
    return out

def bus(x, phone_bass=True, comp=True, verb=0.0, tail=.12):
    """Glue + phone-speaker bass harmonics + limiter."""
    x = x.astype(np.float32)
    if phone_bass:
        low = lp(x, 160, 4)
        harm = pb.Pedalboard([pb.Distortion(drive_db=18)])(low.reshape(1, -1), SR)[0]
        harm = hp(bp(harm, 180, 1400), 180)
        x = x + harm * db(-8)
    board = []
    if comp: board.append(pb.Compressor(threshold_db=-16, ratio=3, attack_ms=2, release_ms=80))
    if verb > 0: board.append(pb.Reverb(room_size=.35, damping=.6, wet_level=verb, dry_level=1.0, width=0.0))
    board.append(pb.Limiter(threshold_db=-1.5, release_ms=60))
    y = pb.Pedalboard(board)(x.reshape(1, -1), SR)[0] if board else x
    y = fade(y, .0005, tail)
    return norm(y, -1.0)

def save(path, x):
    sf.write(path, x.astype(np.float32), SR, subtype='PCM_16')

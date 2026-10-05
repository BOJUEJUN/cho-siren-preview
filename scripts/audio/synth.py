# Small numpy synthesis toolkit used by compose_lobby_theme.py (vendored from the
# project's video soundtrack work). 48 kHz, stereo helpers, TPT filters, FFT reverb.
"""Small additive/subtractive synth toolkit: every sound is computed from sines and noise."""
import numpy as np

SR = 48000
rng = np.random.default_rng(20261004)


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def tt(dur):
    return np.arange(int(dur * SR)) / SR


def osc(freq, t, phase=0.0):
    if np.ndim(freq) == 0:
        return np.sin(2 * np.pi * freq * t + phase)
    return np.sin(2 * np.pi * np.cumsum(freq) / SR + phase)


def expsweep(f0, f1, n):
    return f0 * (f1 / f0) ** np.linspace(0, 1, n)


def fade(sig, a=0.005, r=0.005):
    n = sig.shape[-1]
    env = np.ones(n)
    na, nr = max(1, int(a * SR)), max(1, int(r * SR))
    env[:na] = np.linspace(0, 1, na) ** 2
    env[n - nr:] *= np.linspace(1, 0, nr) ** 2
    return sig * env


def svf(x, fc, q=0.707, mode="lp"):
    """Zavalishin TPT state-variable filter with a per-sample cutoff."""
    fc = np.broadcast_to(np.asarray(fc, float), x.shape)
    g = np.tan(np.pi * np.clip(fc, 10, SR * 0.45) / SR)
    k = 1.0 / q
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    xs, A1, A2, A3 = x.tolist(), a1.tolist(), a2.tolist(), a3.tolist()
    out = [0.0] * len(xs)
    ic1 = ic2 = 0.0
    sel = {"lp": 0, "bp": 1, "hp": 2}[mode]
    for i, v0 in enumerate(xs):
        v3 = v0 - ic2
        v1 = A1[i] * ic1 + A2[i] * v3
        v2 = ic2 + A2[i] * ic1 + A3[i] * v3
        ic1 = 2 * v1 - ic1
        ic2 = 2 * v2 - ic2
        out[i] = v2 if sel == 0 else v1 if sel == 1 else v0 - k * v1 - v2
    return np.array(out)


def stereo_noise(n):
    return rng.standard_normal((2, n))


def filt2(x, fc, q=0.707, mode="lp"):
    return np.stack([svf(x[0], fc, q, mode), svf(x[1], fc, q, mode)])


# ---------------------------------------------------------------- instruments
def thump(f0, f1, dur, decay, sweep=30.0, drive=1.6):
    t = tt(dur)
    f = f1 + (f0 - f1) * np.exp(-t * sweep)
    return np.tanh(drive * osc(f, t)) * (1 - np.exp(-t * 400)) * np.exp(-t * decay)


def kick(vel=1.0):
    t = tt(0.55)
    body = thump(160, 46, 0.55, 7.5, sweep=38, drive=2.2)
    click = svf(rng.standard_normal(len(t)), 4000, 0.7, "hp") * np.exp(-t * 280) * 0.35
    return (body + click) * vel


def clap(vel=1.0):
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    env = np.zeros(n)
    for i, off in enumerate((0.0, 0.011, 0.022)):
        k = int(off * SR)
        env[k:] += np.exp(-(t[: n - k]) * (220 if i < 2 else 26))
    x = stereo_noise(n)
    return filt2(x, 1400, 1.1, "bp") * env * 1.8 * vel


def hat(vel=1.0, dur=0.06):
    n = int(dur * SR)
    t = np.arange(n) / SR
    return filt2(stereo_noise(n), 8500, 0.7, "hp") * np.exp(-t * 70) * vel


def snare(vel=1.0, tone=200):
    t = tt(0.22)
    body = osc(tone, t) * np.exp(-t * 30) * 0.6
    nz = svf(rng.standard_normal(len(t)), 1800, 0.7, "hp") * np.exp(-t * 22)
    return (body + nz) * vel


def bell(freq, dur, decay=1.2):
    t = tt(dur)
    s = np.zeros_like(t)
    for ratio, amp, d in ((1.0, 1.0, 1.0), (2.0, 0.35, 1.6), (2.76, 0.22, 2.4), (5.4, 0.08, 4.0)):
        s += amp * osc(freq * ratio, t, rng.uniform(0, 6.28)) * np.exp(-t * d / decay)
    return s * (1 - np.exp(-t * 600))


def pluck(freq, dur, decay=1.6):
    t = tt(dur)
    s = np.zeros_like(t)
    for h in range(1, 7):
        s += osc(freq * h, t, rng.uniform(0, 6.28)) / h ** 1.2 * np.exp(-t * h / decay)
    return s * (1 - np.exp(-t * 900))


def pad_note(freq, dur, att, rel, bright=0.4):
    """Detuned additive saw voice, stereo (different detune per side)."""
    t = tt(dur)
    out = np.zeros((2, len(t)))
    for side, cents in enumerate(((-7, 0, 5), (-5, 0, 7))):
        for c in cents:
            f = freq * 2 ** (c / 1200)
            for h in range(1, 9):
                if f * h > 9000:
                    break
                out[side] += osc(f * h, t, rng.uniform(0, 6.28)) * np.exp(-h * bright) / h
    env = np.minimum(1.0, t / att) ** 2
    env *= np.clip((dur - t) / rel, 0, 1) ** 1.5
    return out * env / 3


def sub_bass(freq, dur, att=0.02, rel=0.1):
    t = tt(dur)
    s = np.tanh(1.4 * (osc(freq, t) + 0.35 * osc(2 * freq, t)))
    env = np.minimum(1.0, t / att) * np.clip((dur - t) / rel, 0, 1)
    return s * env


def noise_swell(dur, f0, f1, power=3.0, q=1.2):
    n = int(dur * SR)
    y = filt2(stereo_noise(n), expsweep(f0, f1, n), q, "bp")
    return y * np.linspace(0, 1, n) ** power


def riser_tone(dur, f0, f1, power=2.5):
    t = tt(dur)
    f = expsweep(f0, f1, len(t))
    return (osc(f, t) + 0.4 * osc(2 * f, t) + 0.2 * osc(3 * f, t)) * (t / t[-1]) ** power


def crash(dur=3.0, f0=9000, f1=350, decay=1.4):
    n = int(dur * SR)
    y = filt2(stereo_noise(n), expsweep(f0, f1, n))
    return y * np.exp(-np.arange(n) / SR * decay)


# ---------------------------------------------------------------- mixing
class Mixer:
    def __init__(self, dur):
        self.n = int(dur * SR)
        self.buses = {}

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = [np.zeros((2, self.n)), np.zeros((2, self.n))]
        return self.buses[name]

    def place(self, sig, start, gain=1.0, rev=0.0, pan=0.0, bus="main"):
        if sig.ndim == 1:
            a = (pan + 1) * np.pi / 4
            sig = np.stack([sig * np.cos(a), sig * np.sin(a)])
        i0 = int(round(start * SR))
        k = min(sig.shape[1], self.n - i0)
        if k <= 0:
            return
        dry, send = self.bus(bus)
        dry[:, i0:i0 + k] += sig[:, :k] * gain
        send[:, i0:i0 + k] += sig[:, :k] * gain * rev

    def silence(self, t0, t1, buses=None):
        """Hard gap (5 ms ramps) on the dry signal of the given buses."""
        i0, i1 = int(t0 * SR), int(t1 * SR)
        r = int(0.005 * SR)
        for name, (dry, _) in self.buses.items():
            if buses and name not in buses:
                continue
            dry[:, i0 - r:i0] *= np.linspace(1, 0, r)
            dry[:, i0:i1] = 0
            dry[:, i1:i1 + r] *= np.linspace(0, 1, r)


def reverb_ir(seconds=3.0, rt_lo=2.8, rt_hi=1.3, predelay=0.025):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = []
    for _ in range(2):
        nz = rng.standard_normal(n)
        freqs = np.fft.rfftfreq(n, 1 / SR)
        lo = np.fft.irfft(np.fft.rfft(nz) / (1 + (freqs / 1800) ** 2), n)
        side = lo * np.exp(-t * 6.9 / rt_lo) + 0.5 * (nz - lo) * np.exp(-t * 6.9 / rt_hi)
        side[: int(predelay * SR)] = 0
        out.append(side / np.sqrt(np.sum(side ** 2)))
    return out


def convolve(x, ir):
    n = x.shape[1] + len(ir[0])
    nfft = 1 << int(np.ceil(np.log2(n)))
    return np.stack([np.fft.irfft(np.fft.rfft(x[c], nfft) * np.fft.rfft(ir[c], nfft), nfft)[: x.shape[1]] for c in range(2)])

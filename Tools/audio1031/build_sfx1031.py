#!/usr/bin/env python3
"""CHO-SIREN battle SFX v2 (2026-10-31).

Body layer  : Suno v6 Sounds one-shots (RunningHub, see Docs/AUDIO-1031-ledger.json), cut by segment.py
Other layers: Sonniss GDC 2026 (royalty-free) + lentikula Basic Spell Impacts (CC0) — real recordings only,
              no oscillator/noise synthesis. Five-layer method (Docs/SFX-RESEARCH-2026-10-23.md §5.1):
              ① sub/low impact ② body ③ transient ④ sweetener ⑤ tail (pedalboard room on the bus).
Mastering   : glue compressor -> BS.1770 max-momentary loudness target per event -> true-peak limiter
              at -1 dBTP (Tools/audio/loudnorm.py) -> head trim (0 ms latency) -> 44.1 kHz mono PCM16.

  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/segment.py
  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/build_sfx1031.py          # candidates + install
  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/build_sfx1031.py --dry    # candidates only

Every candidate is kept in Builds/qa-logs/zj-1031/audio/candidates/sfx/<event>/ and listed in
Builds/qa-logs/zj-1031/audio/sfx-candidates.json (metrics, layers, chosen slot).
"""
import sys
import json
import glob
import shutil
import hashlib
import pathlib
import numpy as np
import soundfile as sf
import pedalboard as pb
from scipy.signal import resample_poly, lfilter

REPO = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'Tools/sfx1023'))
sys.path.insert(0, str(REPO / 'Tools/audio'))
sys.path.insert(0, str(REPO / 'Tools/audio1031'))
from sfxlib import load, seg, speed, fade, hp, lp, bp, db, SR, SON, LEN  # noqa: E402  (SR = 48000)
from loudnorm import _biquads, limit  # noqa: E402
from metrics import metrics  # noqa: E402
from segment import SPEC  # noqa: E402

LOG = REPO / 'Builds/qa-logs/zj-1031/audio'
CAND = LOG / 'candidates/sfx'
OUT_SR = 44100
SFX_DIR = REPO / 'Assets/Resources/Audio/Sfx'

# Max momentary (400 ms, K-weighted) loudness per event, LUFS. Relative order = importance in the mix.
TARGET = {'hit': -14, 'crit': -11, 'hurt': -14, 'block': -14, 'shield': -14.5, 'heal': -15,
          'ko': -11.5, 'strike-charm': -13, 'strike-demon': -13, 'strike-mermaid': -13, 'strike-bloodelf': -13,
          'ultimate': -10.5, 'boss-hit': -10.5, 'down': -13.5,
          'grade-perfect': -10.5, 'grade-great': -11.5, 'grade-good': -12.5, 'grade-miss': -17}
# number of shipped takes (file, file-2, file-3); grade stingers ship one deterministic file each.
TAKES = {e: (1 if e.startswith('grade-') else 3) for e in TARGET}
CANDIDATES_PER_EVENT = 6


# ------------------------------------------------------------------ professional layer library
def G(pat):
    return glob.glob(SON + '*/' + pat + '*')[0]


class Lib:
    def __init__(self):
        punch = load(G('FGHTImpt_4 x Punch'))
        self.punch = [fade(seg(punch, t, .24), .0005, .1) for t in (0.43, 1.44, 2.43, 3.45)]
        sword = load(G('WEAPSwrd_Sword Slide Cuts'))
        self.sword = [seg(sword, t, .7) for t in (0.15, 2.26, 4.34, 6.34, 8.48, 10.82)]
        wood = load(G('WOODImpt_Wooden Hit'), 0, 6)
        self.wood = [seg(wood, t, .5) for t in (2.27, 3.93, 4.62)]
        bang = load(G('METLImpt_Metal Bangs'), 0, 3)
        self.bang = [seg(bang, t, .45) for t in (0.26, 0.76, 1.27, 1.68)]
        spear = load(G('WEAPBlnt_Spear And Stick Impact'))
        self.spear = [fade(seg(spear, t, .3), .0005, .12) for t in _onsets(spear)[:6]]
        bells = load(G('MAGMisc_Magic Christmas Bells'), 0, 6)
        self.bells = [seg(bells, t, .9) for t in (0.34, 1.71, 3.59, 4.72)]
        self.shield = load(G('WEAPArmr_Metal Shield Spin'), 0, 6)
        self.evil = load(G('AMBDsgn_Evil Spell'), .25, 2.0)
        self.water = load(G('WATRMisc_Water, Liquid Impact'))
        self.whoosh = load(G('WINDDsgn_Wind, Rush, Whoosh'))
        self.metal_whoosh = load(G('METLMisc_Metal, Slow Whoosh'))
        # low-end layers: designed bass drops (lowpassed so they only add weight)
        self.sub = [lp(load(G(p))[:int(1.6 * SR)], 140, 4) for p in (
            'DSGNBass_Bass Drop & Downer Fast 16', 'DSGNBass_Bass Drop & Downer Fast 12',
            'DSGNBass_Bass Drop & Downer Medium 04', 'DSGNBass_Bass Drop & Downer Medium 15',
            'DSGNBass_Jump Start Drop 3', 'DSGNBass_Rattling Downer 3')]
        self.sub = [_from_attack(s) for s in self.sub]
        self.len = {k: [load(LEN + f'{k} Spell Impacts/{k} Spell Impact {i}.wav') for i in range(1, 6)]
                    for k in ('Fire', 'Ice', 'Lightning', 'Water')}


def _onsets(x):
    from sfxlib import onsets
    return onsets(x, -28, .2)


def _from_attack(x, rel=-30):
    e = np.abs(x)
    i = np.argmax(e > e.max() * 10 ** (rel / 20))
    return fade(x[max(0, i - 24):], .001, .2)


def snap(x, max_ms=12, pre_ms=4):
    """Impacts: if the body's peak comes late (a whoosh lead-in), start it 4 ms before the attack."""
    e = np.convolve(np.abs(x), np.ones(48) / 48, 'same')
    i = int(np.argmax(e >= e.max() * .5))
    if i <= int(max_ms / 1000 * SR):
        return x
    y = x[max(0, i - int(pre_ms / 1000 * SR)):].copy()
    k = int(.0015 * SR)
    y[:k] *= np.linspace(0, 1, k)
    return y


def snap(x, max_ms=12, pre_ms=4):
    """Impacts: if the body's peak comes late (a whoosh lead-in), start it 4 ms before the attack."""
    e = np.convolve(np.abs(x), np.ones(48) / 48, 'same')
    i = int(np.argmax(e >= e.max() * .5))
    if i <= int(max_ms / 1000 * SR):
        return x
    y = x[max(0, i - int(pre_ms / 1000 * SR)):].copy()
    k = int(.0015 * SR)
    y[:k] *= np.linspace(0, 1, k)
    return y


def pick(lst, v):
    return lst[v % len(lst)]


def peak_time(x, within=.2):
    n = int(within * SR)
    e = np.convolve(np.abs(x[:n]), np.ones(48) / 48, 'same')
    return int(np.argmax(e))


def place(length, *layers):
    """layers: (signal, offset_samples, gain_db). Length in seconds."""
    out = np.zeros(int(length * SR), np.float32)
    for s, off, g in layers:
        off = max(0, int(off))
        if off >= len(out):
            continue
        s = np.asarray(s, np.float32)[:len(out) - off]
        s = fade(s, .0005, min(.08, len(s) / SR / 4))   # no clicks / hard stops at layer cuts
        out[off:off + len(s)] += s.astype(np.float32) * db(g)
    return out


# Envelope shaping (transient-designer style): hold, then exponential decay. Turns dense Suno bodies
# into hit + tail and removes abrupt gates at the end of a generated gesture.
SHAPE = {'hit': (.06, .12), 'crit': (.12, .22), 'hurt': (.06, .12), 'block': (.08, .2), 'shield': (.35, .4),
         'heal': (.45, .4), 'ko': (.25, .35), 'strike-charm': (.1, .2), 'strike-demon': (.1, .2),
         'strike-mermaid': (.1, .22), 'strike-bloodelf': (.08, .2), 'ultimate': (.45, .45),
         'boss-hit': (.12, .25), 'down': (.2, .3), 'grade-perfect': (.3, .45), 'grade-great': (.2, .3),
         'grade-good': (.1, .2), 'grade-miss': (.08, .15)}


def shape(x, event):
    hold, tau = SHAPE[event]
    t = np.arange(len(x)) / SR
    return (x * np.where(t < hold, 1.0, np.exp(-(t - hold) / tau))).astype(np.float32)


# ------------------------------------------------------------------ recipes
# Each recipe gets the Suno body (48 kHz mono, starts at its attack) and a variant index.
# `t` = the body's hit frame (sample index of its envelope peak); support layers are aligned to it.
def r_hit(L, b, v):
    t = peak_time(b)
    return dict(len=.45, verb=0, layers=[
        (hp(b, 90), 0, 0),
        (speed(hp(pick(L.punch, v), 80), (1.15, 1.25, 1.05, 1.2, 1.1, 1.3)[v % 6]), t - 60, -6),   # ① weight
        (hp(pick(L.spear, v), 2500)[:int(.06 * SR)], t - 20, -10),                                  # ③ snap
        (pick(L.sub, v)[:int(.25 * SR)], t, -11),                                                   # ① sub
    ])


def r_crit(L, b, v):
    t = peak_time(b)
    return dict(len=.95, verb=.07, layers=[
        (hp(b, 100), 0, 0),
        (speed(hp(pick(L.punch, v + 1), 70), 1.0), t - 60, -4),
        (pick(L.sub, v + 2)[:int(.5 * SR)], t, -6),
        (hp(pick(L.sword, v * 2), 2500)[:int(.12 * SR)], t - 30, -10),
        (hp(pick(L.bells, v), 3500), t + int(.02 * SR), -15),                                       # ④ glitter
    ])


def r_hurt(L, b, v):
    t = peak_time(b)
    return dict(len=.55, verb=0, layers=[
        (lp(hp(b, 70), 6000), 0, 0),
        (speed(lp(pick(L.punch, v + 3), 3500), (.85, .9, .8, .95, .88, .82)[v % 6]), t - 60, -5),
        (pick(L.sub, v + 1)[:int(.3 * SR)], t, -9),
        (bp(pick(L.wood, v), 300, 4000)[:int(.25 * SR)], t - 40, -7),
        (bp(pick(L.punch, v + 1), 400, 5000), t - 60, -8),
    ])


def r_block(L, b, v):
    t = peak_time(b)
    s = L.shield[int((.44, 1.33, 3.73, .44, 1.33, 3.73)[v % 6] * SR):][:int(.6 * SR)]
    return dict(len=.8, verb=.05, layers=[
        (hp(b, 200), 0, 0),
        (hp(s, 400), t - 40, -8),
        (hp(pick(L.bang, v), 1500)[:int(.15 * SR)], t - 30, -12),
    ])


def r_shield(L, b, v):
    return dict(len=1.2, verb=.08, layers=[
        (hp(b, 180), 0, 0),
        (hp(pick(L.bells, v + 1), 3000), int(.05 * SR), -16),
        (hp(L.metal_whoosh, 600)[int(v * .3 * SR):][:int(.9 * SR)], 0, -20),
    ])


def r_heal(L, b, v):
    return dict(len=1.3, verb=.1, layers=[
        (hp(b, 200), 0, 0),
        (hp(pick(L.bells, v), 2500), int(.06 * SR), -14),
    ])


def r_ko(L, b, v):
    t = peak_time(b, .3)
    return dict(len=1.5, verb=.1, layers=[
        (hp(b, 110), 0, 0),
        (pick(L.sub, v + 3)[:int(.9 * SR)], t, -5),
        (hp(fade(pick(L.len['Ice'], v)[:int(1.0 * SR)], .001, .4), 500), t - 30, -10),
        (lp(speed(pick(L.bang, v + 1), 1.15), 4000), t - 30, -14),
    ])


def r_strike(kind):
    def r(L, b, v):
        t = peak_time(b)
        layers = [(hp(b, 120), 0, 0)]
        if kind == 'charm':
            layers += [(hp(fade(pick(L.len['Fire'], v)[:int(.6 * SR)], .001, .25), 300), t - 40, -10),
                       (hp(pick(L.bells, v + 2), 3200), t, -15)]
        elif kind == 'demon':
            e = hp(L.evil[int((v % 4) * .35 * SR):][:int(.7 * SR)], 120)
            layers += [(fade(e, .002, .3), 0, -9), (lp(speed(pick(L.bang, v), .7), 2500)[:int(.4 * SR)], t - 40, -9),
                       (pick(L.sub, v + 4)[:int(.35 * SR)], t, -12)]
        elif kind == 'mermaid':
            w = speed(L.water, (1.0, 1.12, .9, 1.06, .95, 1.18)[v % 6])
            layers += [(hp(w, 160), t - 40, -8),
                       (hp(fade(pick(L.len['Water'], v)[:int(.7 * SR)], .001, .3), 200), t - 30, -12)]
        else:  # bloodelf
            layers += [(hp(pick(L.sword, v * 2 + 1), 700)[:int(.5 * SR)], t - 50, -7),
                       (hp(fade(pick(L.len['Ice'], v + 2)[:int(.5 * SR)], .001, .2), 1500), t, -13)]
        return dict(len=.85, verb=.05, layers=layers)
    return r


def r_ultimate(L, b, v):
    t = peak_time(b, 1.2)
    light = hp(fade(pick(L.len['Lightning'], v)[:int(1.3 * SR)], .001, .6), 300)
    return dict(len=2.3, verb=.14, layers=[
        (hp(b, 80), 0, 0),
        (speed(hp(pick(L.punch, v), 70), .85), t - 60, -4),
        (pick(L.sub, v + 2)[:int(1.4 * SR)], t, -4),
        (light, t, -12),
        (hp(pick(L.bells, v + 2), 2500), t + int(.12 * SR), -16),
    ])


def r_boss_hit(L, b, v):
    t = peak_time(b)
    return dict(len=1.1, verb=.06, layers=[
        (hp(b, 80), 0, 0),
        (speed(hp(pick(L.punch, v + 2), 90), .85), t - 70, -4),
        (hp(pick(L.punch, v + 1), 400), t - 60, -6),
        (hp(pick(L.sword, v), 2000)[:int(.1 * SR)], t - 30, -10),
        (pick(L.sub, v)[:int(.7 * SR)], t, -9),
        (bp(speed(pick(L.bang, v + 2), .8), 300, 5000)[:int(.5 * SR)], t - 50, -7),
        (hp(pick(L.wood, v + 1), 250), t - 40, -8),
        (hp(pick(L.spear, v), 2000)[:int(.06 * SR)], t - 20, -11),
    ])


def r_down(L, b, v):
    t = peak_time(b, .4)
    return dict(len=1.3, verb=.1, layers=[
        (hp(b, 70), 0, 0),
        (lp(pick(L.wood, v), 2500), t - 40, -8),
        (pick(L.sub, v + 5)[:int(.6 * SR)], t, -10),
    ])


def r_grade(level):
    def r(L, b, v):
        layers = [(hp(b, 150 if level != 'miss' else 60), 0, 0)]
        t = peak_time(b, .6)
        if level == 'perfect':
            layers += [(hp(pick(L.bells, v), 2500), t, -11), (hp(pick(L.bells, v + 2), 3500), t + int(.09 * SR), -15),
                       (pick(L.sub, 4)[:int(.5 * SR)], t, -12)]
        elif level == 'great':
            layers += [(hp(pick(L.bells, v + 1), 2800), t, -14)]
        elif level == 'good':
            layers += [(hp(pick(L.bells, v + 3), 3000)[:int(.5 * SR)], t, -17)]
        return dict(len=SPEC['grade-' + level]['dur'][2], verb={'perfect': .16, 'great': .1}.get(level, .04),
                    layers=layers)
    return r


RECIPES = {'hit': r_hit, 'crit': r_crit, 'hurt': r_hurt, 'block': r_block, 'shield': r_shield, 'heal': r_heal,
           'ko': r_ko, 'strike-charm': r_strike('charm'), 'strike-demon': r_strike('demon'),
           'strike-mermaid': r_strike('mermaid'), 'strike-bloodelf': r_strike('bloodelf'),
           'ultimate': r_ultimate, 'boss-hit': r_boss_hit, 'down': r_down,
           'grade-perfect': r_grade('perfect'), 'grade-great': r_grade('great'),
           'grade-good': r_grade('good'), 'grade-miss': r_grade('miss')}


# ------------------------------------------------------------------ mastering
def momentary_max(x, fs):
    shelf, hpf = _biquads(fs)
    y = lfilter(*shelf, x)
    y = lfilter(*hpf, y)
    w = int(.4 * fs)
    y = np.pad(y, (0, max(0, w - len(y))))
    c = np.cumsum(np.concatenate([[0], y * y]))
    hop = int(.01 * fs)
    ms = [(c[i + w] - c[i]) / w for i in range(0, len(y) - w + 1, hop)]
    return float(-0.691 + 10 * np.log10(max(ms) + 1e-15))


def master(x, target, verb):
    board = [pb.Compressor(threshold_db=-18, ratio=2.5, attack_ms=4, release_ms=90)]
    if verb:
        board.append(pb.Reverb(room_size=.4, damping=.55, wet_level=verb, dry_level=1.0, width=0))
    x = pb.Pedalboard(board)(x.reshape(1, -1).astype(np.float32), SR)[0].astype(np.float64)
    g = gcd_resample(x)
    gain = target - momentary_max(g, OUT_SR)
    # Never squash the transient: at most GR_MAX dB of limiting above the -1 dBTP ceiling. A dense
    # sound that cannot reach its target stays a little quieter instead of turning into a brick.
    tp = 20 * np.log10(np.abs(resample_poly(g, 4, 1)).max() + 1e-12)
    gain = min(gain, -1.0 + GR_MAX - tp)
    y = g
    for _ in range(8):
        y = limit((g * 10 ** (gain / 20))[:, None], OUT_SR, -1.0)[:, 0]
        now = momentary_max(y, OUT_SR)
        if abs(now - target) < .15 or now < target:
            break
        gain += target - now
    # 0 ms latency: start at the first sample within 40 dB of the peak, 1 ms fade-in
    a = int(np.argmax(np.abs(y) > np.abs(y).max() * .01))
    y = y[max(0, a - 8):]
    y = degate(y)
    y = degate(y)
    # end: cut after the decay falls 60 dB under the peak, 40 ms fade
    env = np.convolve(np.abs(y), np.ones(441) / 441, 'same')
    tail = np.where(env > np.abs(y).max() * 10 ** (-60 / 20))[0]
    if len(tail):
        y = y[:min(len(y), tail[-1] + int(.02 * OUT_SR))]
    k = int(.001 * OUT_SR)
    y[:k] *= np.linspace(0, 1, k)
    k = min(len(y) // 4, int(.04 * OUT_SR))
    y[-k:] *= np.linspace(1, 0, k) ** 2
    return y, momentary_max(y, OUT_SR)


GR_MAX = 3.0


def degate(y, fs=OUT_SR, drop_db=15.0):
    """A generated gesture that stops dead (sustained for 100 ms, then >15 dB fall inside 30 ms while
    still audible) gets an 80 ms fade that ends at the stop, so it decays instead of switching off.
    Natural decays (already falling before the drop) are left alone."""
    w = int(.01 * fs)
    e = 20 * np.log10(np.sqrt(np.convolve(y * y, np.ones(w) / w, 'same')) + 1e-9)
    pk = e.max()
    d = int(.03 * fs)
    for i in range(int(.15 * fs), len(e) - d, int(.005 * fs)):
        held = e[i - int(.1 * fs)] - e[i] < 6
        if held and e[i] > pk - 35 and e[i] - e[i + d] > drop_db:
            end = i + d // 2
            f = min(int(.08 * fs), end)
            y = y[:end].copy()
            y[-f:] *= np.linspace(1, 0, f) ** 2
            return y
    return y


def degate(y, fs=OUT_SR, drop_db=15.0):
    """A generated gesture that stops dead (sustained for 100 ms, then >15 dB fall inside 30 ms while
    still audible) gets an 80 ms fade that ends at the stop, so it decays instead of switching off.
    Natural decays (already falling before the drop) are left alone."""
    w = int(.01 * fs)
    e = 20 * np.log10(np.sqrt(np.convolve(y * y, np.ones(w) / w, 'same')) + 1e-9)
    pk = e.max()
    d = int(.03 * fs)
    for i in range(int(.15 * fs), len(e) - d, int(.005 * fs)):
        held = e[i - int(.1 * fs)] - e[i] < 6
        if held and e[i] > pk - 35 and e[i] - e[i + d] > drop_db:
            end = i + d // 2
            f = min(int(.08 * fs), end)
            y = y[:end].copy()
            y[-f:] *= np.linspace(1, 0, f) ** 2
            return y
    return y


GR_MAX = 3.0


def degate(y, fs=OUT_SR, drop_db=15.0):
    """A generated gesture that stops dead (sustained for 100 ms, then >15 dB fall inside 30 ms while
    still audible) gets an 80 ms fade that ends at the stop, so it decays instead of switching off.
    Natural decays (already falling before the drop) are left alone."""
    w = int(.01 * fs)
    e = 20 * np.log10(np.sqrt(np.convolve(y * y, np.ones(w) / w, 'same')) + 1e-9)
    pk = e.max()
    d = int(.03 * fs)
    for i in range(int(.15 * fs), len(e) - d, int(.005 * fs)):
        held = e[i - int(.1 * fs)] - e[i] < 6
        if held and e[i] > pk - 35 and e[i] - e[i + d] > drop_db:
            end = i + d // 2
            f = min(int(.08 * fs), end)
            y = y[:end].copy()
            y[-f:] *= np.linspace(1, 0, f) ** 2
            return y
    return y


def degate(y, fs=OUT_SR, drop_db=15.0):
    """A generated gesture that stops dead (sustained for 100 ms, then >15 dB fall inside 30 ms while
    still audible) gets an 80 ms fade that ends at the stop, so it decays instead of switching off.
    Natural decays (already falling before the drop) are left alone."""
    w = int(.01 * fs)
    e = 20 * np.log10(np.sqrt(np.convolve(y * y, np.ones(w) / w, 'same')) + 1e-9)
    pk = e.max()
    d = int(.03 * fs)
    for i in range(int(.15 * fs), len(e) - d, int(.005 * fs)):
        held = e[i - int(.1 * fs)] - e[i] < 6
        if held and e[i] > pk - 35 and e[i] - e[i + d] > drop_db:
            end = i + d // 2
            f = min(int(.08 * fs), end)
            y = y[:end].copy()
            y[-f:] *= np.linspace(1, 0, f) ** 2
            return y
    return y


def gcd_resample(x):
    return resample_poly(x, 147, 160)  # 48000 -> 44100


# ------------------------------------------------------------------ selection
def body_pool(rows, event, n):
    """Best segments, at most two per Suno file so the takes differ."""
    seen, out = {}, []
    for r in sorted([r for r in rows if r['event'] == event], key=lambda r: r['score']):
        if seen.get(r['source'], 0) >= 2:
            continue
        seen[r['source']] = seen.get(r['source'], 0) + 1
        out.append(r)
        if len(out) >= n:
            break
    return out


def final_score(m, seg_score, spec, target=None):
    s = seg_score
    if target is not None and m.get('momentary_max', target) < target - 4.5:
        s += 3 + (target - 4.5 - m['momentary_max']) / 2     # too spiky/thin to sit at its level
    s += max(0, m['attack_ms'] - spec['attack']) / max(10, spec['attack'])
    s += m['clip_count'] * 2 + max(0, m['tp_db'] + .9) * 5
    s += max(0, m['lead_ms'] - 1) / 2
    return round(s, 3)


def sha(p):
    return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()


def main(dry=False):
    rows = json.loads((LOG / 'segments.json').read_text())
    L = Lib()
    report = {}
    for event, recipe in RECIPES.items():
        out_dir = CAND / event
        out_dir.mkdir(parents=True, exist_ok=True)
        for old in out_dir.glob('*.wav'):
            old.unlink()
        cands = []
        for i, r in enumerate(body_pool(rows, event, CANDIDATES_PER_EVENT)):
            body = load(str(REPO / r['path']))
            if SPEC[event]['kind'] == 'impact' or event in ('ko', 'down', 'boss-hit'):
                body = snap(body)
            plan = recipe(L, body, i)
            x = shape(place(plan['len'], *plan['layers']), event)
            y, lm = master(x, TARGET[event], plan['verb'])
            dst = out_dir / ('cand-%02d.wav' % (i + 1))
            sf.write(dst, y.astype(np.float32), OUT_SR, subtype='PCM_16')
            m = metrics(str(dst))
            m.update(momentary_max=round(lm, 2), body=r['path'], body_source=r['source'], body_start=r['start'],
                     seg_score=r['score'], sha256=sha(dst),
                     path=str(dst.relative_to(REPO)))
            m['score'] = final_score(m, r['score'], SPEC[event], TARGET[event])
            m['score'] = final_score(m, r['score'], SPEC[event], TARGET[event])
            cands.append(m)
        ranked = sorted(cands, key=lambda c: c['score'])
        chosen = ranked[:TAKES[event]]
        # takes of one event share one short-term loudness (only ever turned down, so peaks stay < -1 dBTP)
        common = min(c['momentary_max'] for c in chosen)
        for slot, c in enumerate(chosen):
            name = event + ('' if slot == 0 else '-%d' % (slot + 1)) + '.wav'
            c['chosen_as'] = name
            y, fs = sf.read(REPO / c['path'], dtype='float64')
            y = y * 10 ** ((common - c['momentary_max']) / 20)
            c['shipped_momentary_max'] = round(momentary_max(y, fs), 2)
            if not dry:
                sf.write(SFX_DIR / name, y.astype(np.float32), fs, subtype='PCM_16')
                c['shipped_sha256'] = sha(SFX_DIR / name)
        report[event] = {'target_lufs_m': TARGET[event], 'candidates': cands}
        print('%-16s %d cands -> %s' % (event, len(cands), ', '.join(
            '%s(%.2fs %.1fLUFS-M tp%.1f atk%.0fms)' % (c['path'][-11:-4], c['dur'], c['momentary_max'], c['tp_db'],
                                                       c['attack_ms']) for c in chosen)), flush=True)
    (LOG / 'sfx-candidates.json').write_text(json.dumps(report, indent=1))


if __name__ == '__main__':
    main('--dry' in sys.argv)

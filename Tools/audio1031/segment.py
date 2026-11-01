#!/usr/bin/env python3
"""Cut Suno v6 Sounds candidates into individual one-shots and score them objectively.

Suno often returns several hits inside one "one-shot" file. Each file is split at energy onsets;
every piece is trimmed to start exactly at its attack (0 ms latency), faded at a clean decay point
and measured (metrics.py). Output:
  Builds/qa-logs/zj-1031/audio/segments/<event>/<source>-s<k>.wav
  Builds/qa-logs/zj-1031/audio/segments.json   (all pieces with metrics and score; lower score = better)
"""
import json
import pathlib
import sys
import numpy as np
import soundfile as sf
from math import gcd
from scipy.signal import resample_poly

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from metrics import metrics  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
RAW = REPO / 'Builds/qa-logs/zj-1031/audio/raw/suno'
SEG = REPO / 'Builds/qa-logs/zj-1031/audio/segments'
SR = 44100

# kind: impact (cut each hit) / sustain (keep the first gesture whole)
# dur: (min, ideal, max) seconds; centroid: preferred brightness band (Hz); attack: max ok ms to peak
SPEC = {
    'hit':             dict(kind='impact',  dur=(.12, .3, .5),   cen=(1200, 5000), attack=12),
    'crit':            dict(kind='impact',  dur=(.25, .6, .95),  cen=(1800, 6500), attack=15),
    'hurt':            dict(kind='impact',  dur=(.15, .35, .55), cen=(500, 3000),  attack=15),
    'block':           dict(kind='impact',  dur=(.2, .45, .8),   cen=(1500, 6000), attack=15),
    'shield':          dict(kind='sustain', dur=(.4, .8, 1.2),   cen=(1500, 7000), attack=250),
    'heal':            dict(kind='sustain', dur=(.5, .9, 1.3),   cen=(1500, 7000), attack=300),
    'ko':              dict(kind='sustain', dur=(.5, 1.0, 1.5),  cen=(800, 5000),  attack=40),
    'strike-charm':    dict(kind='impact',  dur=(.25, .55, .85), cen=(1500, 7000), attack=30),
    'strike-demon':    dict(kind='impact',  dur=(.25, .55, .85), cen=(600, 4000),  attack=30),
    'strike-mermaid':  dict(kind='impact',  dur=(.25, .55, .85), cen=(800, 5000),  attack=30),
    'strike-bloodelf': dict(kind='impact',  dur=(.25, .55, .85), cen=(1500, 6500), attack=30),
    'ultimate':        dict(kind='sustain', dur=(.9, 1.6, 2.3),  cen=(700, 5000),  attack=60),
    'boss-hit':        dict(kind='impact',  dur=(.35, .7, 1.1),  cen=(300, 2500),  attack=20),
    'down':            dict(kind='sustain', dur=(.45, .9, 1.3),  cen=(300, 3000),  attack=60),
    'grade-perfect':   dict(kind='sustain', dur=(.8, 1.3, 1.8),  cen=(2500, 9000), attack=120),
    'grade-great':     dict(kind='sustain', dur=(.45, .8, 1.1),  cen=(2000, 8000), attack=120),
    'grade-good':      dict(kind='sustain', dur=(.25, .5, .8),   cen=(1500, 7000), attack=40),
    'grade-miss':      dict(kind='sustain', dur=(.2, .45, .7),   cen=(200, 2000),  attack=60),
}


def load(path):
    x, sr = sf.read(path, always_2d=True, dtype='float64')
    x = x.mean(1)
    g = gcd(SR, sr)
    return resample_poly(x, SR // g, sr // g) if sr != SR else x


def frame_db(x, win=.01, hop=.0025):
    w, h = int(win * SR), int(hop * SR)
    n = max(1, (len(x) - w) // h)
    e = np.array([np.mean(x[i * h:i * h + w] ** 2) for i in range(n)])
    return 10 * np.log10(e + 1e-14), h


def onsets(x, kind):
    db, h = frame_db(x)
    top = db.max()
    gap = .11 if kind == 'impact' else .5
    out, last = [], -1e9
    look = int(.04 / (h / SR))
    for i in range(1, len(db)):
        floor = db[max(0, i - look):i].min()
        if db[i] > top - 32 and db[i] - floor > 10 and db[i] >= db[i - 1]:
            t = i * h / SR
            if t - last >= gap:
                # walk back to where the rise begins (true attack start)
                j = i
                while j > 0 and db[j - 1] < db[j] - .5 and db[j - 1] > floor + 1:
                    j -= 1
                out.append(j * h / SR)
                last = t
    if not out:
        out = [float(np.argmax(db > top - 30) * h / SR)]
    return out


def cut(x, t0, t_next, spec):
    lo, ideal, hi = spec['dur']
    a = max(0, int((t0 - .002) * SR))
    end = min(len(x), int((t_next - .004) * SR)) if t_next else len(x)
    end = min(end, a + int(hi * SR))
    y = x[a:end].copy()
    if len(y) < int(.05 * SR):
        return None
    # cut where the decay reaches -42 dB under the piece's peak (keeps tails natural, removes dead air)
    db, h = frame_db(y, .01, .005)
    pk = int(np.argmax(db))
    below = np.where(db[pk:] < db[pk] - 42)[0]
    if len(below):
        y = y[:min(len(y), (pk + below[0]) * h + int(.01 * SR))]
    k = int(.0015 * SR)
    y[:k] *= np.linspace(0, 1, k)
    k = min(len(y) // 3, int(.06 * SR))
    pk_amp = np.abs(y).max() + 1e-12
    if spec['kind'] == 'sustain' or end < len(x):
        k = min(len(y) // 3, int(.15 * SR))   # a gesture cut by the length cap / next hit gets a longer fade
    if spec['kind'] == 'sustain' or end < len(x):
        k = min(len(y) // 3, int(.15 * SR))   # a gesture cut by the length cap / next hit gets a longer fade
    cut_db = 20 * np.log10(np.sqrt(np.mean(y[-k - int(.03 * SR):-k] ** 2)) / pk_amp + 1e-12) if len(y) > k + int(.03 * SR) else -60
    y[-k:] *= np.linspace(1, 0, k) ** 2
    return y, cut_db, 20 * np.log10(pk_amp)


def score(m, spec):
    lo, ideal, hi = spec['dur']
    s = 0.0
    d = m['dur']
    s += 0 if lo <= d <= hi else 3 + abs(d - (lo if d < lo else hi)) * 4
    s += abs(d - ideal) / ideal
    s += max(0, m['attack_ms'] - spec['attack']) / max(10, spec['attack'])
    c0, c1 = spec['cen']
    c = m['centroid']
    s += 0 if c0 <= c <= c1 else 2 * abs(np.log2(c / (c0 if c < c0 else c1)))
    s += max(0, .25 - m.get('mid_frac', .25)) * 12   # phone speakers: needs 0.8-6 kHz energy
    s += m['clip_count'] * .5
    s += max(0, m['tail_db'] + 40) / 10       # cut while still loud = truncated
    s += max(0, 14 - m['crest_db']) / 6 if spec['kind'] == 'impact' else max(0, 11 - m['crest_db']) / 4
    s += m.get('late_ratio', 0)
    s += m.get('gate', 0)                       # generated gesture that stops dead mid-sound
    s += m.get('gate', 0)                       # generated gesture that stops dead mid-sound                 # sustained sounds should bloom then decay, not swell to the cut
    return round(s, 3)


def main():
    rows = []
    for ev, spec in SPEC.items():
        (SEG / ev).mkdir(parents=True, exist_ok=True)
        for src in sorted(RAW.glob(ev + '-r*-[12].wav')):
            x = load(src)
            x = x / (np.abs(x).max() + 1e-9) * .89
            ts = onsets(x, spec['kind'])
            if spec['kind'] == 'sustain':
                ts = ts[:2]
            for k, t in enumerate(ts):
                res = cut(x, t, ts[k + 1] if k + 1 < len(ts) and spec['kind'] == 'impact' else None, spec)
                if res is None:
                    continue
                y, cut_db, peak_rel = res
                if peak_rel < 20 * np.log10(.89) - 8:   # residue, not one of the file's main sounds
                    continue
                dst = SEG / ev / ('%s-s%d.wav' % (src.stem, k + 1))
                sf.write(dst, y.astype(np.float32), SR, subtype='FLOAT')
                m = metrics(str(dst))
                m['cut_db'] = round(float(cut_db), 1)
                th = len(y) // 3
                early = np.sqrt(np.mean(y[:th] ** 2)) + 1e-9
                e = np.convolve(np.abs(y), np.ones(220) / 220, 'same')
                e_db = 20 * np.log10(e + 1e-9)
                drop = e_db[:-1323] - e_db[1323:] if len(e_db) > 1400 else np.array([0])   # 30 ms
                m['gate'] = round(float(max(0, drop[: max(1, len(drop) - int(.2 * SR))].max() - 30) / 10), 3)
                e = np.convolve(np.abs(y), np.ones(220) / 220, 'same')
                e_db = 20 * np.log10(e + 1e-9)
                drop = e_db[:-1323] - e_db[1323:] if len(e_db) > 1400 else np.array([0])   # 30 ms
                m['gate'] = round(float(max(0, drop[: max(1, len(drop) - int(.2 * SR))].max() - 30) / 10), 3)
                sp = np.abs(np.fft.rfft(y)) ** 2
                fq = np.fft.rfftfreq(len(y), 1 / SR)
                m['mid_frac'] = round(float(sp[(fq > 800) & (fq < 6000)].sum() / (sp.sum() + 1e-12)), 3)
                sp = np.abs(np.fft.rfft(y)) ** 2
                fq = np.fft.rfftfreq(len(y), 1 / SR)
                m['mid_frac'] = round(float(sp[(fq > 800) & (fq < 6000)].sum() / (sp.sum() + 1e-12)), 3)
                m['late_ratio'] = round(float(max(0, np.sqrt(np.mean(y[2 * th:] ** 2)) / early - .6)), 3)
                th = len(y) // 3
                early = np.sqrt(np.mean(y[:th] ** 2)) + 1e-9
                e = np.convolve(np.abs(y), np.ones(220) / 220, 'same')
                e_db = 20 * np.log10(e + 1e-9)
                drop = e_db[:-1323] - e_db[1323:] if len(e_db) > 1400 else np.array([0])   # 30 ms
                m['gate'] = round(float(max(0, drop[: max(1, len(drop) - int(.2 * SR))].max() - 30) / 10), 3)
                e = np.convolve(np.abs(y), np.ones(220) / 220, 'same')
                e_db = 20 * np.log10(e + 1e-9)
                drop = e_db[:-1323] - e_db[1323:] if len(e_db) > 1400 else np.array([0])   # 30 ms
                m['gate'] = round(float(max(0, drop[: max(1, len(drop) - int(.2 * SR))].max() - 30) / 10), 3)
                sp = np.abs(np.fft.rfft(y)) ** 2
                fq = np.fft.rfftfreq(len(y), 1 / SR)
                m['mid_frac'] = round(float(sp[(fq > 800) & (fq < 6000)].sum() / (sp.sum() + 1e-12)), 3)
                sp = np.abs(np.fft.rfft(y)) ** 2
                fq = np.fft.rfftfreq(len(y), 1 / SR)
                m['mid_frac'] = round(float(sp[(fq > 800) & (fq < 6000)].sum() / (sp.sum() + 1e-12)), 3)
                m['late_ratio'] = round(float(max(0, np.sqrt(np.mean(y[2 * th:] ** 2)) / early - .6)), 3)
                m['score'] = round(score(m, spec) + max(0, cut_db + 22) / 6, 3)
                m.update(event=ev, source=src.name, start=round(t, 3), path=str(dst.relative_to(REPO)))
                rows.append(m)
        best = sorted([r for r in rows if r['event'] == ev], key=lambda r: r['score'])[:4]
        print(ev, len([r for r in rows if r['event'] == ev]), 'pieces; best:',
              ['%s@%.2f d=%.2f sc=%.2f' % (r['source'][len(ev) + 1:-4], r['start'], r['dur'], r['score']) for r in best])
    (REPO / 'Builds/qa-logs/zj-1031/audio/segments.json').write_text(json.dumps(rows, indent=1))


if __name__ == '__main__':
    main()

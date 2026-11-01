#!/usr/bin/env python3
"""QA of every audio file added/replaced on 2026-10-31: decodes, format, duration, loudness, true peak, clipping,
leading silence. Also estimates the compressed (Vorbis) build size of the new audio.
  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/qa_audio.py
-> Builds/qa-logs/zj-1031/audio/qa-audio.json (+ printed summary; exit 1 on any failure)."""
import io
import sys
import json
import pathlib
import numpy as np
import soundfile as sf

REPO = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'Tools/audio1031'))
from metrics import metrics  # noqa: E402

A = REPO / 'Assets/Resources/Audio'
SFX = ['hit', 'crit', 'hurt', 'block', 'shield', 'heal', 'ko', 'strike-charm', 'strike-demon', 'strike-mermaid',
       'strike-bloodelf', 'ultimate', 'boss-hit', 'down']
GRADES = ['grade-perfect', 'grade-great', 'grade-good', 'grade-miss']


def files():
    out = []
    for n in SFX:
        out += [('sfx', A / 'Sfx' / (n + s + '.wav')) for s in ('', '-2', '-3')]
    out += [('sfx', A / 'Sfx' / (g + '.wav')) for g in GRADES]
    out += [('defeat', p) for p in sorted((A / 'Voice/Battle').glob('*-defeat-*.wav'))]
    out += [('enemy', p) for p in sorted((A / 'Voice/Enemy').glob('*.wav'))]
    out += [('announcer', p) for p in sorted((A / 'Voice/Announcer').glob('*.wav'))]
    return out


def vorbis_bytes(path, q):
    x, sr = sf.read(path, dtype='float32')
    buf = io.BytesIO()
    sf.write(buf, x, sr, format='OGG', subtype='VORBIS', compression_level=1 - q)
    return buf.tell()


def main():
    rows, fails, size_ogg, size_wav = [], [], 0, 0
    for kind, p in files():
        r = {'kind': kind, 'file': str(p.relative_to(REPO))}
        try:
            info = sf.info(str(p))
            m = metrics(str(p))
        except Exception as e:  # decode failure
            r['error'] = repr(e); fails.append(r); rows.append(r); continue
        r.update(sr=info.samplerate, ch=info.channels, subtype=info.subtype, dur=m['dur'], lufs=m['lufs'],
                 tp_db=m['tp_db'], clip=m['clip_count'], lead_ms=m['lead_ms'], attack_ms=m['attack_ms'])
        bad = []
        if info.samplerate != 44100: bad.append('sr')
        if info.channels != 1: bad.append('channels')
        if m['clip_count'] > 0: bad.append('clipping')
        if m['tp_db'] > -0.9: bad.append('true-peak')
        if kind == 'sfx' and m['lead_ms'] > 2: bad.append('head-silence')
        if kind == 'defeat' and not .8 <= m['dur'] <= 2.5: bad.append('defeat-length')
        if kind in ('enemy', 'announcer') and not .3 <= m['dur'] <= 4.5: bad.append('length')
        if bad: r['fail'] = bad; fails.append(r)
        size_wav += p.stat().st_size
        size_ogg += vorbis_bytes(str(p), .7 if kind == 'sfx' else .5)
        rows.append(r)
    summary = {'files': len(rows), 'failures': len(fails), 'wav_bytes': size_wav,
               'vorbis_estimate_bytes': size_ogg,
               'note': 'vorbis estimate = libsndfile Vorbis at the import quality; Unity WebGL output size is similar '
                       'but not identical. 14 SFX names x3 replace existing files of similar length.'}
    (REPO / 'Builds/qa-logs/zj-1031/audio/qa-audio.json').write_text(
        json.dumps({'summary': summary, 'failures': fails, 'files': rows}, ensure_ascii=False, indent=1))
    print(json.dumps(summary, indent=1))
    for f in fails:
        print('FAIL', f)
    by = {}
    for r in rows:
        by.setdefault(r['kind'], []).append(r)
    for k, v in by.items():
        print('%-9s n=%-3d dur %.2f-%.2fs  lufs %.1f..%.1f  tp max %.2f  clip %d' % (
            k, len(v), min(x['dur'] for x in v), max(x['dur'] for x in v), min(x['lufs'] for x in v),
            max(x['lufs'] for x in v), max(x['tp_db'] for x in v), sum(x['clip'] for x in v)))
    sys.exit(1 if fails else 0)


if __name__ == '__main__':
    main()

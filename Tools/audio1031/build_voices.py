#!/usr/bin/env python3
"""Build all 2026-10-31 voice assets from the raw MiniMax takes.

  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/build_voices.py process   # raw mp3 -> candidate wavs + metrics
  (run asr_check.py over the candidates -> Builds/qa-logs/zj-1031/audio/asr-*.json)
  ~/cho-siren-audio-src/venv/bin/python Tools/audio1031/build_voices.py select    # pick takes, copy to Assets, write json

Outputs
  Assets/Resources/Audio/Voice/Battle/<member>-defeat-0N.wav      (A, 4 members x 2)
  Assets/Resources/Audio/Voice/Enemy/<enemy>-<cat>-0N.wav + barks.json  (B)
  Assets/Resources/Audio/Voice/Announcer/ult-<grade>.wav           (C)
  Builds/qa-logs/zj-1031/audio/candidates/voice/**                  every processed take
  Docs/AUDIO-1031-voice-manifest.json                               text / voice / params / sha / chosen take
"""
import sys
import json
import glob
import shutil
import hashlib
import pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'Tools/audio1031'))
import voice_post as vp  # noqa: E402
from metrics import metrics  # noqa: E402

LOG = REPO / 'Builds/qa-logs/zj-1031/audio'
RAW = LOG / 'raw'
CAND = LOG / 'candidates/voice'
PLANS = REPO / 'Tools/audio1031/plans'
VOICE = REPO / 'Assets/Resources/Audio/Voice'

DIALOGUE_LUFS = -19.5
ANNOUNCER_LUFS = -16.5
ENEMY_FX = {'pulse-guard': 'drowned', 'noise-wraith': 'ghost', 'static-golem': 'berserk',
            'siren-queen': 'hall', 'echo-drone': 'hall', 'velvet-hexer': 'room', 'neon-scout': None}
# The announcer's "PERFECT" carries a little stadium air; good/great stay dry and quick.
ANNOUNCER_FX = {'good': None, 'great': 'room', 'perfect': 'hall'}

GROUPS = [
    ('defeat', 'player-defeat.json', 'voice-defeat'),
    ('enemy', 'enemy-barks.json', 'voice-enemy'),
    ('announcer', 'announcer.json', 'voice-announcer'),
]


def sha(p):
    return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()


def process():
    out = {}
    for group, plan, raw in GROUPS:
        (CAND / group).mkdir(parents=True, exist_ok=True)
        for line in json.loads((PLANS / plan).read_text())['lines']:
            src = RAW / raw / ('voice-%s-1.mp3' % line['id'])
            if not src.exists():
                continue
            dst = CAND / group / (line['id'] + '.wav')
            if group == 'enemy':
                vp.process(src, dst, DIALOGUE_LUFS, .24, .17, ENEMY_FX.get(line['enemy']))
            elif group == 'announcer':
                vp.process(src, dst, ANNOUNCER_LUFS, None, 0, ANNOUNCER_FX[line['grade']])
            else:
                # Defeat lines must stay within 0.8-2.5 s: a slow take gets its silences cut tighter.
                if vp.process(src, dst, DIALOGUE_LUFS, .2, .14) > 2.5:
                    vp.process(src, dst, DIALOGUE_LUFS, .12, .08)
            m = metrics(str(dst))
            out[line['id']] = {'group': group, 'raw': str(src.relative_to(REPO)), 'raw_sha256': sha(src),
                               'wav': str(dst.relative_to(REPO)), **{k: m[k] for k in
                               ('dur', 'lufs', 'tp_db', 'peak_db', 'clip_count', 'lead_ms', 'noise_db')}}
            print(line['id'], out[line['id']]['dur'], out[line['id']]['lufs'], out[line['id']]['tp_db'], flush=True)
    (LOG / 'voice-candidates.json').write_text(json.dumps(out, ensure_ascii=False, indent=1))


def asr_table():
    t = {}
    for f in glob.glob(str(LOG / 'asr-cand-*.json')):
        for r in json.loads(pathlib.Path(f).read_text()):
            t[pathlib.Path(r['file']).stem] = r
    return t


def install(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)


def select():
    cands = json.loads((LOG / 'voice-candidates.json').read_text())
    asr = asr_table()
    manifest = {'model': 'MiniMax speech-2.8-hd via RunningHub (rhart-audio/text-to-audio/speech-2.8-hd)',
                'post': 'trim silence, shorten pauses > .2 s (silence only), light FX for some enemies, '
                        'BS.1770 loudness match + true-peak limiter, 44.1 kHz mono PCM16',
                'defeat': [], 'enemy': [], 'announcer': []}

    # ---- A: player defeat (best take per member/variant: in 0.8-2.5 s, lowest ASR error) ----
    lines = json.loads((PLANS / 'player-defeat.json').read_text())['lines']
    by = {}
    for l in lines:
        by.setdefault((l['member'], l['variant']), []).append(l)
    for (member, var), takes in sorted(by.items()):
        def score(l):
            c = cands.get(l['id'])
            if not c:
                return 99
            d = c['dur']
            return (0 if .8 <= d <= 2.5 else 5 + abs(d - 2.5)) + asr.get(l['id'], {}).get('cer', .5) * 2 + \
                (0 if l['emotion'] in ('sad', 'angry') else .15)
        best = min(takes, key=score)
        dst = VOICE / 'Battle' / ('%s-defeat-%02d.wav' % (member, var))
        install(REPO / cands[best['id']]['wav'], dst)
        manifest['defeat'].append({'resource': 'Battle/%s-defeat-%02d' % (member, var), 'member': member,
                                   'text': best['text'], 'zh': best['zh'], 'voice_id': best['voice_id'],
                                   'emotion': best['emotion'], 'speed': best['speed'], 'pitch': best['pitch'],
                                   'chosen_take': best['id'], 'takes': [t['id'] for t in takes],
                                   'asr': asr.get(best['id'], {}).get('asr'), 'sha256': sha(dst),
                                   **{k: cands[best['id']][k] for k in ('dur', 'lufs', 'tp_db')}})

    # ---- B: enemy barks -> barks.json ----
    plan = json.loads((PLANS / 'enemy-barks.json').read_text())
    barks = {}
    for l in plan['lines']:
        c = cands.get(l['id'])
        if not c:
            continue
        name = '%s-%s-%02d' % (l['enemy'], l['cat'], l['variant'])
        dst = VOICE / 'Enemy' / (name + '.wav')
        install(REPO / c['wav'], dst)
        barks.setdefault(l['enemy'], {k: [] for k in ('enter', 'taunt', 'lowhp', 'kill', 'death')})
        barks[l['enemy']][l['cat']].append({'text': l['zh'], 'clip': 'Voice/Enemy/' + name})
        manifest['enemy'].append({'resource': 'Enemy/' + name, 'enemy': l['enemy'], 'cat': l['cat'],
                                  'text': l['text'], 'zh': l['zh'], 'voice_id': l['voice_id'],
                                  'emotion': l['emotion'], 'speed': l['speed'], 'pitch': l['pitch'],
                                  'fx': ENEMY_FX.get(l['enemy']), 'asr': asr.get(l['id'], {}).get('asr'),
                                  'sha256': sha(dst), **{k: c[k] for k in ('dur', 'lufs', 'tp_db')}})
    # The only boss in tactics.json is siren-queen; boss-default reuses her set. minion-default is a neutral
    # mix of short, generic lines (no character-specific wording).
    barks['boss-default'] = json.loads(json.dumps(barks['siren-queen']))
    pick = {'enter': [('neon-scout', 0), ('static-golem', 1)], 'taunt': [('neon-scout', 0), ('pulse-guard', 1)],
            'lowhp': [('echo-drone', 0)], 'kill': [('neon-scout', 0), ('echo-drone', 1)],
            'death': [('neon-scout', 0)]}
    barks['minion-default'] = {cat: [barks[e][cat][i] for e, i in refs] for cat, refs in pick.items()}
    order = ['neon-scout', 'noise-wraith', 'pulse-guard', 'velvet-hexer', 'echo-drone', 'static-golem',
             'siren-queen', 'boss-default', 'minion-default']
    barks = {k: barks[k] for k in order}
    (VOICE / 'Enemy' / 'barks.json').write_text(json.dumps(barks, ensure_ascii=False, indent=2) + '\n')

    # ---- C: announcer (best voice per grade by ASR, then loudness/peak sanity) ----
    lines = json.loads((PLANS / 'announcer.json').read_text())['lines']
    chosen_voice = json.loads((PLANS / 'announcer.json').read_text()).get('chosen_voice')
    for grade in ('good', 'great', 'perfect'):
        takes = [l for l in lines if l['grade'] == grade]
        if chosen_voice:
            best = next(l for l in takes if l['voice_id'] == chosen_voice)
        else:
            best = min(takes, key=lambda l: asr.get(l['id'], {}).get('cer', 1))
        dst = VOICE / 'Announcer' / ('ult-%s.wav' % grade)
        install(REPO / cands[best['id']]['wav'], dst)
        manifest['announcer'].append({'resource': 'Announcer/ult-' + grade, 'text': best['text'],
                                      'voice_id': best['voice_id'], 'emotion': best['emotion'],
                                      'chosen_take': best['id'], 'takes': [t['id'] for t in takes],
                                      'asr': asr.get(best['id'], {}).get('asr'), 'sha256': sha(dst),
                                      **{k: cands[best['id']][k] for k in ('dur', 'lufs', 'tp_db')}})
    (REPO / 'Docs/AUDIO-1031-voice-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=1))
    print('defeat', len(manifest['defeat']), 'enemy', len(manifest['enemy']), 'announcer', len(manifest['announcer']))


if __name__ == '__main__':
    {'process': process, 'select': select}[sys.argv[1]]()

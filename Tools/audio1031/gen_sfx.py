#!/usr/bin/env python3
"""Suno v6 Sounds (RunningHub international, USD) body layers for the 2026-10-31 battle SFX.

  python3 gen_sfx.py [--runs 2] [--only ev1,ev2]

Each request returns two candidates; RUNS requests per event. Output:
  Builds/qa-logs/zj-1031/audio/raw/suno/<event>-r<run>-<n>.wav   (+ receipts.json)
Already successful (event, run) pairs are skipped, so the command is safe to re-run.
"""
import argparse
import json
import pathlib
import concurrent.futures as cf
import rh1031 as rh

OUT = rh.REPO / 'Builds/qa-logs/zj-1031/audio/raw/suno'
MAX_USD = 0.05
TAIL = ', single one-shot sound effect, dry, no music, no voice'

PROMPTS = {
    'hit': 'anime fighting game punch impact, tight punchy body thump with crisp snap transient, short',
    'crit': 'critical hit impact, heavy punch with bright glass shatter crack and sparkle burst, anime action game',
    'hurt': 'muffled heavy body blow impact, dark dull thud with a short crunch, taking damage, game',
    'block': 'magic shield block, metallic clang deflect with ringing energy barrier ping, short',
    'shield': 'magic barrier activation, crystalline energy shield forming, shimmering glassy swell, bright',
    'heal': 'healing spell, soft rising sparkle bells with warm magical shimmer, gentle',
    'ko': 'enemy defeated, crystal monster shattering into glass shards over a deep boom, anime game',
    'strike-charm': 'pink heart magic burst hit, bright candy sparkle pop with airy flame whoosh, cute anime spell',
    'strike-demon': 'dark demon claw slash hit, vicious tearing swipe with purple void thump',
    'strike-mermaid': 'water magic splash hit, cyan wave crash with bubbly pop',
    'strike-bloodelf': 'crimson blade slash hit, sharp metallic sword swing with dark blood magic shimmer',
    'ultimate': 'massive anime ultimate finisher impact, deep sub boom, sharp crack and glittering magic tail',
    'boss-hit': 'heavy hit on a giant armored boss, deep thick impact with armor crunch and sub boom',
    'down': 'character collapses, soft heavy fall thud with fading magical shimmer, sad',
    'grade-perfect': 'rhythm game PERFECT judgement sound, sparkling synth riser into bright cymbal crash and glittering chimes, triumphant',
    'grade-great': 'rhythm game GREAT judgement sound, bright synth chime stab with sparkle',
    'grade-good': 'rhythm game GOOD judgement sound, clean bell ding, short',
    'grade-miss': 'rhythm game MISS sound, soft muted dull thud falling downward, short',
}


# Round 3: tighter wording for the events whose first two rounds were weak (see segments.json scores).
PROMPTS_R3 = {
    'ultimate': 'one huge cinematic impact: short reverse cymbal swell then a single massive boom with sharp crack, '
                'long shimmering magic tail, anime ultimate attack',
    'strike-charm': 'one short magical hit: bright sparkle burst with a soft pink fire whoosh and glassy chime, anime',
    'hit': 'one single short punch impact, meaty thump and sharp slap transient, close mic',
    'heal': 'one short healing chime: soft harp glissando up with warm sparkles, gentle',
    'grade-great': 'one short bright success stinger: rising synth arpeggio ending in sparkle chime',
    'ko': 'one enemy shatter: glass and crystal burst into shards with low thump, quick decay',
}


def run(event, r):
    label = '%s-r%d' % (event, r)
    prompt = PROMPTS_R3[event] if r == 3 and event in PROMPTS_R3 else PROMPTS[event]
    payload = {'prompt': prompt + TAIL, 'soundType': 'one-shot', 'audioFormat': 'wav'}
    e = rh.submit('intl', rh.SUNO_SOUNDS, payload, label, MAX_USD)
    if e.get('task_id'):
        e = rh.wait(e, OUT, timeout=900, poll=8)
    return label, e


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runs', type=int, default=2)
    ap.add_argument('--only', default='')
    ap.add_argument('--workers', type=int, default=4)
    a = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    rp = OUT / 'receipts.json'
    rec = json.loads(rp.read_text()) if rp.exists() else {}
    only = set(filter(None, a.only.split(',')))
    jobs = [(ev, r) for ev in PROMPTS if not only or ev in only for r in range(1, a.runs + 1)
            if r < 3 or ev in PROMPTS_R3
            if rec.get('%s-r%d' % (ev, r), {}).get('status') != 'SUCCESS']
    print('jobs', len(jobs), flush=True)
    with cf.ThreadPoolExecutor(a.workers) as ex:
        for label, e in ex.map(lambda j: run(*j), jobs):
            rec[label] = {k: e.get(k) for k in ('task_id', 'status', 'quote', 'actual', 'currency', 'error')}
            rec[label]['prompt'] = e.get('payload', {}).get('prompt')
            rec[label]['outputs'] = [o for o in e.get('outputs') or [] if o['path'].endswith('.wav')]
            rp.write_text(json.dumps(rec, ensure_ascii=False, indent=1))
            print(label, e.get('status'), e.get('actual'), len(rec[label]['outputs']), flush=True)
    print('spent_cny_total', rh.spent_cny())


if __name__ == '__main__':
    main()

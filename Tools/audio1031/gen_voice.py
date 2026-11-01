#!/usr/bin/env python3
"""Generate MiniMax speech-2.8-hd lines from a plan JSON (RunningHub, domestic CNY wallet).

  python3 gen_voice.py PLAN.json OUT_DIR [--only id1,id2] [--workers 4]

PLAN = {"lines": [{"id", "text", "voice_id", "emotion", "speed", "pitch", ...}]}
Skips ids already present in OUT_DIR/receipts.json with SUCCESS (no duplicate paid calls).
"""
import argparse
import json
import pathlib
import concurrent.futures as cf
import rh1031 as rh

MAX_PER_LINE_CNY = 0.03


def run_one(line, out_dir):
    payload = {k: line[k] for k in ('text', 'voice_id', 'emotion', 'speed', 'pitch')}
    payload.update(volume=line.get('volume', 1), enable_base64_output=False, english_normalization=False)
    e = rh.submit(line.get('wallet', 'cn'), rh.MINIMAX, payload, 'voice-' + line['id'], MAX_PER_LINE_CNY)
    if not e.get('task_id'):
        return line['id'], e
    e = rh.wait(e, out_dir, timeout=300, poll=3)
    return line['id'], e


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('plan'); ap.add_argument('out')
    ap.add_argument('--only', default='')
    ap.add_argument('--workers', type=int, default=4)
    a = ap.parse_args()
    out = pathlib.Path(a.out); out.mkdir(parents=True, exist_ok=True)
    rec_path = out / 'receipts.json'
    receipts = json.loads(rec_path.read_text()) if rec_path.exists() else {}
    lines = json.loads(pathlib.Path(a.plan).read_text())['lines']
    only = set(filter(None, a.only.split(',')))
    todo = [l for l in lines if (not only or l['id'] in only) and receipts.get(l['id'], {}).get('status') != 'SUCCESS']
    print('todo', len(todo), flush=True)
    with cf.ThreadPoolExecutor(a.workers) as ex:
        for lid, e in ex.map(lambda l: run_one(l, out), todo):
            receipts[lid] = {k: e.get(k) for k in ('task_id', 'status', 'quote', 'actual', 'currency', 'outputs', 'error')}
            rec_path.write_text(json.dumps(receipts, ensure_ascii=False, indent=1))
            print(lid, e.get('status'), e.get('actual'), [o['path'].split('/')[-1] for o in e.get('outputs') or []], flush=True)
    print('spent_cny_total', rh.spent_cny())


if __name__ == '__main__':
    main()

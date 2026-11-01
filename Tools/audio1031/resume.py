#!/usr/bin/env python3
"""Finish ledger entries that were submitted but not collected (query known taskIds only, never resubmit),
then rebuild the per-folder receipts.json files from the ledger."""
import json
import rh1031 as rh

FOLDERS = {'suno-v6/sounds': 'Builds/qa-logs/zj-1031/audio/raw/suno'}


def main():
    led = rh.load_ledger()
    for c in led['calls']:
        if c.get('task_id') and c.get('status') not in ('SUCCESS', 'FAILED', 'SUPERSEDED'):
            folder = FOLDERS.get(c['model'].split('rhart-audio/')[-1])
            if folder:
                c = rh.wait(c, rh.REPO / folder, timeout=900, poll=8)
                print('resumed', c['label'], c['status'], c.get('actual'))
    led = rh.load_ledger()
    rec = {}
    for c in led['calls']:
        if 'suno-v6/sounds' in c['endpoint'] and not c['label'].startswith('pilot'):
            rec[c['label']] = {k: c.get(k) for k in ('task_id', 'status', 'quote', 'actual', 'currency', 'error')}
            rec[c['label']]['prompt'] = c['payload']['prompt']
            rec[c['label']]['outputs'] = [o for o in c.get('outputs') or [] if o['path'].endswith('.wav')]
    (rh.REPO / FOLDERS['suno-v6/sounds'] / 'receipts.json').write_text(json.dumps(rec, ensure_ascii=False, indent=1))
    print('receipts', len(rec), 'totals', led.get('totals'))


if __name__ == '__main__':
    main()

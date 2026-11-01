#!/usr/bin/env python3
"""RunningHub client for the 2026-10-31 audio batch (CHO-SIREN).

Credentials are read at runtime from the user's existing private handoff files and only live in
this process. They are never printed, written to the repo, logs or the ledger. Every paid call is
price-previewed first and recorded in Docs/AUDIO-1031-ledger.json (model, taskId, cost, output sha).

  intl  = RunningHub international (USD wallet)  -> Suno v6 Sounds, MiniMax speech
  cn    = RunningHub domestic config (CNY wallet) -> MiniMax speech-2.8-hd, Seed-Audio
"""
import datetime as dt
import hashlib
import importlib.util
import json
import pathlib
import re
import threading
import time
import urllib.error
import urllib.request

REPO = pathlib.Path(__file__).resolve().parents[2]
LEDGER = REPO / 'Docs' / 'AUDIO-1031-ledger.json'
HOST = 'https://www.runninghub.ai'
BUDGET_CNY = 60.0
USD_TO_CNY = 7.3          # conservative conversion used only for the combined batch cap

INTL_README = pathlib.Path('/Users/bojuejun/Documents/Codex/2026-10-15/new-chat/outputs/'
                           'music-handoff-20261017/README_做歌交接.md')
CN_PROBE = pathlib.Path('/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/bgm-20261014/rh_readonly_probe.py')

SUNO_SOUNDS = '/openapi/v2/rhart-audio/suno-v6/sounds'
MINIMAX = '/openapi/v2/rhart-audio/text-to-audio/speech-2.8-hd'
SEED = '/openapi/v2/bytedance/doubao-seed-audio-1.0'
QUERY = '/openapi/v2/query'
ACCOUNT = '/uc/openapi/accountStatus'
ALLOWED = {SUNO_SOUNDS, MINIMAX, SEED, QUERY, ACCOUNT}
ALLOWED |= {p.replace('/openapi/v2/', '/openapi/v2/price-preview/') for p in (SUNO_SOUNDS, MINIMAX, SEED)}

_keys = {}
LOCK = threading.RLock()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def key(wallet):
    if wallet in _keys:
        return _keys[wallet]
    if wallet == 'intl':
        text = INTL_README.read_text()
        sec = text.split('## 3.', 1)[1].split('## 4.', 1)[0]
        m = re.search(r'```text\s*\n(.*?)\n```', sec, re.S)
        if not m:
            raise RuntimeError('international credential reference not found')
        _keys[wallet] = m.group(1).strip()
    elif wallet == 'cn':
        spec = importlib.util.spec_from_file_location('rhprobe', CN_PROBE)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _keys[wallet], _ = mod.credentials()
    else:
        raise ValueError(wallet)
    return _keys[wallet]


def redact(s):
    s = str(s)
    for v in _keys.values():
        s = s.replace(v, '[REDACTED]')
    return s


def post(wallet, path, payload, timeout=60):
    if path not in ALLOWED:
        raise RuntimeError('route not allowlisted')
    k = key(wallet)
    body = dict(payload)
    if path == ACCOUNT:
        body['apikey'] = k
    req = urllib.request.Request(HOST + path, data=json.dumps(body, ensure_ascii=False).encode(), method='POST',
                                 headers={'Content-Type': 'application/json', 'Authorization': 'Bearer ' + k,
                                          'User-Agent': 'CHO-SIREN-audio-1031/1'})
    try:
        with urllib.request.build_opener(NoRedirect).open(req, timeout=timeout) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        try:
            detail = e.read().decode('utf-8', 'replace')[:300]
        except Exception:
            detail = ''
        return {'errorCode': 'HTTP_%d' % e.code, 'errorMessage': redact(detail)}


def account(wallet):
    r = post(wallet, ACCOUNT, {})
    d = r.get('data') or {}
    return {k: d.get(k) for k in ('remainMoney', 'currency', 'currentTaskCounts', 'remainCoins') if k in d} | \
        {'code': r.get('code')}


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def load_ledger():
    if LEDGER.exists():
        return json.loads(LEDGER.read_text())
    return {'batch': 'zj-1031 audio', 'budget_cny': BUDGET_CNY, 'usd_to_cny_for_cap': USD_TO_CNY,
            'note': 'Credentials are never stored here. cost = actual settled amount when known, else quote.',
            'calls': []}


def save_ledger(led):
    tot_cny = tot_usd = 0.0
    for c in led['calls']:
        v = c.get('actual') if c.get('actual') is not None else c.get('quote') or 0
        if c.get('status') in ('REJECTED',):
            continue
        if c.get('currency') == 'USD':
            tot_usd += float(v)
        else:
            tot_cny += float(v)
    led['totals'] = {'cny': round(tot_cny, 5), 'usd': round(tot_usd, 5),
                     'combined_cny_for_cap': round(tot_cny + tot_usd * USD_TO_CNY, 4)}
    tmp = LEDGER.with_suffix('.tmp')
    tmp.write_text(json.dumps(led, ensure_ascii=False, indent=1))
    tmp.replace(LEDGER)


def spent_cny():
    led = load_ledger()
    save_ledger(led)
    return led['totals']['combined_cny_for_cap']


def submit(wallet, path, payload, label, max_cost):
    """price-preview -> cap check -> one submit (no automatic retry). Returns the ledger entry."""
    q = post(wallet, path.replace('/openapi/v2/', '/openapi/v2/price-preview/'), payload)
    if q.get('errorCode') or q.get('estimatedPrice') is None:
        raise RuntimeError('no quote for %s: %s' % (label, redact(q)[:200]))
    price, cur = float(q['estimatedPrice']), q.get('currency')
    if price > max_cost:
        raise RuntimeError('quote %.4f %s above per-call cap %.4f for %s' % (price, cur, max_cost, label))
    add = price * (USD_TO_CNY if cur == 'USD' else 1)
    LOCK.acquire()
    if spent_cny() + add > BUDGET_CNY:
        LOCK.release()
        raise RuntimeError('batch budget cap would be exceeded')
    led = load_ledger()
    entry = {'label': label, 'wallet': wallet, 'endpoint': path, 'model': path.split('/openapi/v2/')[1],
             'payload': payload, 'quote': price, 'currency': cur, 'submitted_at': now(),
             'status': 'SUBMITTING'}
    led['calls'].append(entry)
    save_ledger(led)
    LOCK.release()
    r = post(wallet, path, payload)
    entry.update(task_id=r.get('taskId'), status=r.get('status') or ('REJECTED' if r.get('errorCode') else 'QUEUED'),
                 error=redact(r.get('errorMessage') or r.get('errorCode') or '')[:200] or None)
    with LOCK:
        led = load_ledger()
        _replace(led, entry)
        save_ledger(led)
    return entry


def _replace(led, entry):
    for i, c in enumerate(led['calls']):
        if c.get('label') == entry['label'] and c.get('submitted_at') == entry['submitted_at']:
            led['calls'][i] = entry


def wait(entry, out_dir, timeout=900, poll=6):
    """Polls only the known taskId, downloads results to out_dir, records sha/cost."""
    out_dir = pathlib.Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    tid = entry.get('task_id')
    if not tid:
        return entry
    end = time.time() + timeout
    r = {}
    while time.time() < end:
        r = post(entry['wallet'], QUERY, {'taskId': tid})
        st = r.get('status')
        if st in ('SUCCESS', 'FAILED'):
            break
        time.sleep(poll)
    entry['status'] = r.get('status') or 'UNKNOWN'
    usage = r.get('usage') or {}
    for f in ('thirdPartyConsumeMoney', 'consumeMoney'):
        if usage.get(f) not in (None, '', 0, '0'):
            try:
                entry['actual'] = float(usage[f])
                break
            except ValueError:
                pass
    files = []
    for i, res in enumerate(r.get('results') or []):
        url = res.get('url') or res.get('fileUrl')
        if not url:
            continue
        ext = (res.get('outputType') or url.rsplit('.', 1)[-1].split('?')[0] or 'bin').lower()[:4]
        dst = out_dir / ('%s-%d.%s' % (entry['label'], i + 1, ext))
        with urllib.request.urlopen(url, timeout=120) as resp:
            data = resp.read()
        dst.write_bytes(data)
        files.append({'path': str(dst.relative_to(REPO)) if str(dst).startswith(str(REPO)) else str(dst),
                      'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)})
    entry['outputs'] = files
    if r.get('errorMessage'):
        entry['error'] = redact(r['errorMessage'])[:200]
    entry['finished_at'] = now()
    with LOCK:
        led = load_ledger()
        _replace(led, entry)
        save_ledger(led)
    return entry

"""Offline Whisper-small sanity check (no downloads). Run with the existing local venv:
  /Users/bojuejun/Documents/Codex/2026-10-15/new-chat/work/jay-voice-pilot-20261016/local-venv/bin/python \
      asr_check.py LANG OUT.json file:expected_text [...]
Reports the transcription and a character error rate against the target (punctuation/tags removed).
ASR only shows intelligibility / native-like pronunciation; it does not grade acting.
"""
import os
import sys
import json
import re
import unicodedata
from fractions import Fraction
from pathlib import Path

ROOT = Path('/Users/bojuejun/Documents/Codex/2026-10-15/new-chat/work/jay-voice-pilot-20261016')
REV = '973afd24965f72e36ca33b3055d56a652f456b4d'
MODEL = ROOT / 'seed-vc-local/checkpoints/hf_cache/models--openai--whisper-small/snapshots' / REV
TOK = ROOT / 'v2-isolated-cache/hub/models--openai--whisper-small/snapshots' / REV
for k in ['HF_HUB_OFFLINE', 'TRANSFORMERS_OFFLINE']:
    os.environ[k] = '1'
import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402
from scipy.signal import resample_poly  # noqa: E402
from transformers import WhisperFeatureExtractor, WhisperForConditionalGeneration, WhisperTokenizer  # noqa: E402


def norm(s):
    s = re.sub(r'\([^)]*\)', '', s)
    s = unicodedata.normalize('NFKC', s).lower()
    return ''.join(ch for ch in s if ch.isalnum())


def cer(ref, hyp):
    r, h = norm(ref), norm(hyp)
    if not r:
        return 0.0
    d = list(range(len(h) + 1))
    for i in range(1, len(r) + 1):
        prev, d[0] = d[0], i
        for j in range(1, len(h) + 1):
            cur = d[j]
            d[j] = min(d[j] + 1, d[j - 1] + 1, prev + (r[i - 1] != h[j - 1]))
            prev = cur
    return d[len(h)] / len(r)


def main():
    lang, out = sys.argv[1], sys.argv[2]
    torch.set_num_threads(4)
    fe = WhisperFeatureExtractor.from_pretrained(MODEL, local_files_only=True)
    tok = WhisperTokenizer.from_pretrained(TOK, local_files_only=True, language=lang, task='transcribe')
    model = WhisperForConditionalGeneration.from_pretrained(MODEL, local_files_only=True, use_safetensors=True,
                                                            torch_dtype=torch.float32).eval()
    model.generation_config.forced_decoder_ids = tok.get_decoder_prompt_ids(language=lang, task='transcribe',
                                                                            no_timestamps=True)
    rows = []
    for arg in sys.argv[3:]:
        path, _, target = arg.partition('::')
        a, sr = sf.read(path, dtype='float32', always_2d=True)
        a = a.mean(1)
        fr = Fraction(16000, sr)
        a = resample_poly(a, fr.numerator, fr.denominator)
        inp = fe(a, sampling_rate=16000, return_attention_mask=True, return_tensors='pt')
        with torch.inference_mode():
            t = model.generate(input_features=inp.input_features, attention_mask=inp.attention_mask, do_sample=False,
                               num_beams=3, max_new_tokens=80)
        text = tok.batch_decode(t, skip_special_tokens=True)[0].strip()
        row = {'file': path, 'target': target, 'asr': text, 'cer': round(cer(target, text), 3)}
        rows.append(row)
        print(json.dumps(row, ensure_ascii=False), flush=True)
    Path(out).write_text(json.dumps(rows, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()

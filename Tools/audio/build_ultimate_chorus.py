#!/usr/bin/env python3
"""Build short wordless ultimate responses from the project's existing licensed vocal sample.

Run with ~/cho-siren-audio-src/venv/bin/python. No model, download or paid service is used.
The underlying recording is a nonverbal human performance; these are processed harmonies,
not newly recorded character songs. The original source and approved BGM remain unchanged.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
import pedalboard as pb
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path.home() / "cho-siren-audio-src/sonniss-gdc2026/part1/344 Audio - Anime Fight Voices Vol. 2/VOXFem_Anime, Warrior Elf Princess Aggressive Yell_344 Audio_Anime Fight Voices Vol 2_14.wav"
DEST = ROOT / "Assets/Resources/Audio/Chorus"
SR = 48000


def main():
    audio, rate = sf.read(str(SOURCE), always_2d=True)
    dry = audio.mean(axis=1)
    dry = resample_poly(dry, SR, rate).astype(np.float32)
    # The sustained nonverbal portion keeps a real breath/attack; no spoken words are spliced.
    dry = dry[int(.20 * SR):int(1.28 * SR)]
    dry = sosfilt(butter(2, [220, 6100], btype="bandpass", fs=SR, output="sos"), dry).astype(np.float32)
    dry[:2400] *= np.linspace(0, 1, 2400)
    dry[-7200:] *= np.linspace(1, 0, 7200) ** 2
    # Each response has its own timbre; variant 2 changes timing/intervals, not random pitch.
    profiles = {"xingli": (1, .30), "feiyin": (-2, .16), "wubai": (3, .38), "yeying": (-4, .24)}
    DEST.mkdir(parents=True, exist_ok=True)
    rows = []
    for member, (shift, wet) in profiles.items():
        for take in (1, 2):
            out = np.zeros((2, int(SR * 1.60)), dtype=np.float32)
            for semitones, delay, gain, pan in [
                (shift, 0, .65, 0),
                (shift + (7 if take == 1 else 5), .035 if take == 1 else .065, .22, -.5),
                (shift - 12, .065 if take == 1 else .025, .13, .5),
            ]:
                signal = pb.PitchShift(semitones)(dry[np.newaxis, :], SR)[0]
                at = int(delay * SR)
                out[0, at:at + len(signal)] += signal * gain * np.sqrt((1 - pan) * .5)
                out[1, at:at + len(signal)] += signal * gain * np.sqrt((1 + pan) * .5)
            out = pb.Pedalboard([
                pb.Compressor(threshold_db=-24, ratio=2.5, attack_ms=12, release_ms=120),
                pb.Reverb(room_size=.35, damping=.7, wet_level=wet, dry_level=.85, width=.75),
            ])(out, SR)
            peak = float(np.abs(out).max())
            if peak: out *= 10 ** (-6 / 20) / peak
            out[:, -int(.28 * SR):] *= np.linspace(1, 0, int(.28 * SR)) ** 2
            path = DEST / (member + "-" + str(take) + ".wav")
            sf.write(str(path), out.T, SR, subtype="PCM_16")
            rows.append({"resource": "Audio/Chorus/" + path.stem, "seconds": len(out[0]) / SR,
                         "peak_dbfs": round(20 * np.log10(float(np.abs(out).max())), 3),
                         "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    manifest = {"source": str(SOURCE), "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                "license": str(SOURCE.parents[1] / "License - GDC Game Audio.pdf"),
                "method": "Existing human nonverbal recording, local pitch-layering and reverb; no lyrics, no BGM copied, no paid generation.",
                "clips": rows}
    (ROOT / "Docs/AUDIO-COMPLETION-2026-10-28-chants.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({"clips": len(rows), "duration": 1.60, "peak_dbfs": -6, "total_bytes": sum(p.stat().st_size for p in DEST.glob('*.wav'))}))


if __name__ == "__main__": main()

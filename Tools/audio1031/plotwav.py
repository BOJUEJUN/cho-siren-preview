#!/usr/bin/env python3
"""Waveform + spectrogram strip for a list of audio files -> one PNG (visual QA of candidates)."""
import sys
import numpy as np
import soundfile as sf
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt  # noqa: E402


def main(out, paths):
    n = len(paths)
    fig, axes = plt.subplots(n, 2, figsize=(14, 1.6 * n + .4), squeeze=False)
    for i, p in enumerate(paths):
        x, sr = sf.read(p, always_2d=True)
        x = x.mean(1)
        t = np.arange(len(x)) / sr
        axes[i][0].plot(t, x, lw=.4)
        axes[i][0].set_ylim(-1, 1)
        axes[i][0].set_title(p.split('/')[-1], fontsize=7, loc='left')
        axes[i][0].tick_params(labelsize=6)
        axes[i][1].specgram(x + 1e-9, NFFT=512, Fs=sr, noverlap=384, cmap='magma', vmin=-120)
        axes[i][1].set_ylim(0, 16000)
        axes[i][1].tick_params(labelsize=6)
    fig.tight_layout()
    fig.savefig(out, dpi=80)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2:])

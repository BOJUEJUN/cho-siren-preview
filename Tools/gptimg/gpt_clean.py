#!/usr/bin/env python3
"""Clean the speckle/grain that GPT Image leaves in anime art (worse with reference images).

Usage: gpt_clean.py IN.png OUT.png [--strength 1.0] [--scale 2]
Pipeline: isolated-speckle removal -> edge-preserving non-local-means -> optional 2x upscale.
"""
import argparse
import cv2
import numpy as np


def remove_specks(img, thresh):
    # Pixels that jump far from their 3x3 median on luminance are specks, unless they sit on a line.
    lab = cv2.cvtColor(img, cv2.COLOR_BGR2LAB)
    L = lab[:, :, 0]
    med = cv2.medianBlur(img, 3)
    diff = cv2.absdiff(L, cv2.cvtColor(med, cv2.COLOR_BGR2LAB)[:, :, 0])
    edges = cv2.Canny(cv2.GaussianBlur(L, (5, 5), 0), 60, 140)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8))
    mask = (diff > thresh) & (edges == 0)
    out = img.copy()
    out[mask] = med[mask]
    return out, int(mask.sum())


def clean(img, strength=1.0, scale=1):
    alpha = None
    if img.ndim == 3 and img.shape[2] == 4:
        alpha = img[:, :, 3]
        img = img[:, :, :3]
    img, n = remove_specks(img, thresh=int(26 / max(strength, .1)))
    h = 4.0 * strength
    img = cv2.fastNlMeansDenoisingColored(img, None, h, h, 5, 15)
    if scale > 1:
        img = cv2.resize(img, None, fx=scale, fy=scale, interpolation=cv2.INTER_LANCZOS4)
        img = cv2.bilateralFilter(img, 5, 18 * strength, 3)
        if alpha is not None:
            alpha = cv2.resize(alpha, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
    if alpha is not None:
        img = np.dstack([img, alpha])
    return img, n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--strength", type=float, default=1.0)
    ap.add_argument("--scale", type=int, default=1)
    a = ap.parse_args()
    img = cv2.imread(a.src, cv2.IMREAD_UNCHANGED)
    out, n = clean(img, a.strength, a.scale)
    cv2.imwrite(a.dst, out)
    print(f"{a.dst} {out.shape[1]}x{out.shape[0]} specks={n}")


if __name__ == "__main__":
    main()

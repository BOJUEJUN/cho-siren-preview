# GPT image noise — research and pipeline (2026-10-28)

少爷 paused RunningHub image generation (cost). Images now come from his ChatGPT through the bundled
Codex CLI (built-in `image_gen`, no API key). This note records why GPT output looks grainy and what we do.

## What others found
- GPT Image 2 has a known speckle/"sparkle" grain bug; an OpenAI researcher said they are working on it
  (OpenAI community thread "Collection of GPT-image-generator 2.0 issues").
- It is strongest with reference images / image-to-image and grows over a long conversation
  (earlier images leak texture). Fix: one fresh session per image, as few references as possible.
- "In the style of X" requests may pull hidden references → avoid; describe the look instead.
- Busy micro-detail triggers it (particles, dust, rain, glitter, lace, fishnet, halftone). Ask for less detail
  and describe concrete clean surfaces; vague words like "HD, no noise" alone do little (yingtu.ai guide).
- Post-processing: Larryvrh/gpt-image-2-artifact-cleaner (FLUX.2-VAE, trained on GPT Image 2) works best
  but is PolyForm **Noncommercial** and wants CUDA — not used. Generic denoise as the last step.
- ChatGPT / Codex built-in path has no quality or size switch (API-only `quality: high`).

## Our pipeline
- `Tools/gptimg/gpt_gen.sh OUT.png "prompt" [--ref IMG] [--transparent] [--raw] [--strength S]`
  fresh `codex exec` per image + anti-noise rendering block, saves `OUT.raw.png`, then
- `Tools/gptimg/gpt_clean.py` (OpenCV only): isolated-speck removal off edges → light non-local-means
  (h = 4 × strength) → optional `--scale 2` Lanczos + bilateral. Strength 1.0 keeps lines and lace.

## Measured (same 9:16 group CG, flat-area high-frequency σ, lower = cleaner)
| variant | raw | cleaned |
|---|---|---|
| A old prompt, 5 references | 3.55 | 2.91 |
| B new prompt, 2 references | 2.83 | 2.35 |
| C new prompt, no references | 2.37 | 1.78 |

Trade-off: fewer references = cleaner but character designs drift (C changed outfits). Use references
only when identity matters, and then a single combined lineup sheet. Side by side: `GPT-IMAGE-NOISE-2026-10-28/compare-abc.jpg`.
GPT also blocks revealing outfits (`moderation_blocked`) — describe stage outfits as fully covered.

# Female enemy character assets — 2026-09-07

## Superseding decision: use the user's existing art pack

The user rejected the three AI designs below and explicitly requested the supplied character pack instead. The generation records remain below for provenance, but those images are no longer shipping resources. Only these three generated PNGs and their own `.meta` files were moved from `Assets/Resources/Art/Enemies/` into `ArtArchive/EnemyConcepts-20260907/`. The archive is outside `Assets`, so Unity will not include them in the build. No user-provided original was moved, edited, or removed.

The provided package was located at `/Users/nikizhao/Downloads/3695 棕色尘埃2【202605】/01.立绘.zip`. Existing runtime derivatives are already imported in `Assets/Resources/Art/Members/`; use their existing resource keys directly, avoiding duplicate textures and a redundant extraction of the large ZIP.

Selected mappings, visually checked against all four starting party portraits:

- `neon-scout` → `Art/Members/hero-0205/portrait`: green short-haired, dual-dagger streetwear female scout. Workspace: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Members/hero-0205/portrait.png`. Source package filename: `char020501.png`.
- `pulse-guard` → `Art/Members/hero-0035/portrait`: blonde white/blue armored, winged sword-bearing female guardian. Workspace: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Members/hero-0035/portrait.png`. Source package filename: `char003501.png`. This source depicts a large sword, not the rejected AI design's shield; do not retain shield-specific visual claims.
- `velvet-hexer` → `Art/Members/hero-0003/portrait`: blue-hatted female spellcaster with a long staff and book. Workspace: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Members/hero-0003/portrait.png`. Source package filename: `char000303.png`. This source is blue-themed, not the rejected AI design's red hair.
- Additional replacement: `noise-wraith` → `Art/Members/hero-0007/portrait`: adult-presenting dark-haired violet-clad winged female specter, with a floating pose and a legible hair/wing silhouette. Workspace: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Members/hero-0007/portrait.png`. Source package filename: `Char000701.png` (capital C). Visually inspected full-body and distinct from the four starting party images and the three selections above. Reuse the existing sprite directly; no copy or pixel edits. The existing runtime manifest records a 512 × 704 RGBA image with alpha extrema 0–255; its file hash was checked against that manifest in this subtask. The earlier AI `noise-wraith` image's physical removal/archiving is not performed by this read-only selection subtask.

Source filenames and historical original paths are traced by `Tools/member-runtime-art-sources.json` and `Docs/member-runtime-art-manifest.json`. The ZIP inventory confirms the filenames remain present in the supplied Mac package. Character ages were not independently verified from canonical franchise lore; selection was based on adult-presenting visual design and existing supplied art, not invented age metadata. The three chosen portraits visibly differ from `member-xingli`, `member-feiyin`, `member-wubai`, and `member-yeying`; `hero-0002` was explicitly excluded because it matches the starting character 星璃.

Fresh read-only PNG decoding confirmed each selected runtime file is 512 × 704 RGBA with alpha extrema 0–255:

- `hero-0205`: 296,905 fully transparent and 9,485 partially transparent pixels; SHA-256 `8182d5dc72f3166907d1c169dbb1bf370142d1b003bff96112049f3b4d11f7af`.
- `hero-0035`: 276,786 fully transparent and 20,195 partially transparent pixels; SHA-256 `630e62c374fad4b142cba36e7dee714cac577236205f3e0ad9381cba742ba0fb`.
- `hero-0003`: 267,323 fully transparent and 15,665 partially transparent pixels; SHA-256 `ba267cffd111c4f672c247bf9783e3b571fa97bf14342bda726ceb77ab20dfaa`.

All three have whole-body art and existing transparent safety margins. No additional pixel edits were performed for the enemy mapping. Existing pack possession and prior in-project use do not establish third-party commercial redistribution rights; these are user-selected prototype assets, and the original rights caveat in the project catalog documentation remains applicable.

This replacement subtask does not edit `EnemySprite`, battle balance, or scene layout and did not start Unity. Runtime mappings are integrated by the main task.

## Delivery and provenance

Generated with the built-in image_gen tool, not fallback CLI. The imagegen skill was read and followed. Three independent new-image calls ran concurrently, one per requested enemy. No input reference images, third-party game assets, or existing IP character designs were used. Full final prompts are reproduced below.

The full-body results were visually inspected for distinct roles, complete silhouettes and weapons, consistent original anime mobile-game style, and absence of text/UI/watermarks. The guard's spear is close to the top edge but remains inside the image; preserveAspect is important when displaying these tall sprites. This is original single-pose raster artwork, not a skeletal-animation model.

Selected source PNGs were copied byte-for-byte into the workspace with versioned names. No pixel editing, cutout, recoloring, or downsampling of the originals was performed. Built-in generation output remains preserved in its original directory. All generated previews were displayed via generatedImage.

All three source PNGs are 1024 × 1536, 8-bit RGBA, noninterlaced, containing 1,572,864 pixels each. Actual alpha was verified with read-only IDAT decompression and PNG row-filter reversal, not inferred from preview backgrounds. Every file has alpha range 0–254, with genuine fully transparent pixels and feathered artwork edges. The dark appearance behind the preview is not proof of an opaque PNG background.

The existing EnemyArtImportProcessor covers these files and will import single sprites at maximum 1024 texture size with no mipmaps/no CPU readback and configured compressed Standalone/WebGL output. This subtask did not start Unity, edit battle data, or edit the panel. Unity import metadata, runtime assignments, browser scale/compositing, and final build checks are the main task's integration responsibility.

## neon-scout

- Source: `/Users/nikizhao/.codex/generated_images/01a07ada-4529-7723-a4a1-32a42743cbf5/exec-69a4824e-f8d4-4931-9126-7bc87e68cc0f.png`
- Workspace target: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Enemies/neon-scout-ai-v1.png`
- Runtime resource key: `Art/Enemies/neon-scout-ai-v1`
- Alpha pixel counts: 1,056,782 fully transparent; 516,082 partially transparent.

Final prompt:

```text
Use case: stylized-concept
Asset type: production-ready transparent full-body 2D enemy character sprite for CHO-SIREN, a dark neon sci-fi music fantasy mobile game.
Scene/backdrop: genuinely transparent background with real alpha, isolated character only; no floor, backdrop, ground shadow, checkerboard pattern, text, logos, watermark, UI, or other characters.
Style/medium: polished original Japanese mobile RPG anime illustration, crisp expressive face, fine clean linework, rich cel-shading and soft painted material highlights, readable silhouette at small game size. Original design, not a character from another IP. Tasteful non-sexual costume, clearly adult woman.
Composition: vertical portrait, entire body including head, hands, weapons and boots fully visible, generous transparent padding on all sides; front three-quarter battle-ready stance, eye-level; no cropped limbs or weapon tips. Controlled purple/cyan rim lighting suitable for a dark stage. One character, no contact sheet.
Subject: NEON SCOUT, a nimble adult female street-idol reconnaissance fighter. Short cyan bob hair, alert confident expression, black-and-white lightweight tactical street-performance jacket and fitted practical trousers with enclosed boots, two compact glowing light blades held in her hands. Athletic poised scout stance ready to dash; accent colors cyan and white. Keep blade light compact, do not obscure body with effects.
```

## pulse-guard

- Source: `/Users/nikizhao/.codex/generated_images/01a07ada-4529-7723-a4a1-32a42743cbf5/exec-5be75db9-b84b-4b14-9c06-a3dc8fe335cf.png`
- Workspace target: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Enemies/pulse-guard-ai-v1.png`
- Runtime resource key: `Art/Enemies/pulse-guard-ai-v1`
- Alpha pixel counts: 734,257 fully transparent; 838,607 partially transparent.

Final prompt:

```text
Use case: stylized-concept
Asset type: production-ready transparent full-body 2D enemy character sprite for CHO-SIREN, a dark neon sci-fi music fantasy mobile game.
Scene/backdrop: genuinely transparent background with real alpha, isolated character only; no floor, backdrop, ground shadow, checkerboard pattern, text, logos, watermark, UI, or other characters.
Style/medium: polished original Japanese mobile RPG anime illustration, crisp expressive face, fine clean linework, rich cel-shading and soft painted material highlights, readable silhouette at small game size. Original design, not a character from another IP. Tasteful non-sexual costume, clearly adult woman.
Composition: vertical portrait, entire body including head, hands, weapons and boots fully visible, generous transparent padding on all sides; front three-quarter battle-ready stance, eye-level; no cropped limbs or weapon tips. Controlled purple/cyan rim lighting suitable for a dark stage. One character, no contact sheet.
Subject: PULSE GUARD, an adult female defensive sentinel. Long silver hair, composed steadfast expression, enclosed black and deep-purple segmented protective armor covering torso and legs, sturdy armored boots. Holds one large translucent cyan-purple hexagonal energy shield and one slender spear. Clearly defensive planted stance. Accent colors violet and blue. Shield may be semitransparent but character body remains readable; no giant surrounding aura.
```

## velvet-hexer

- Source: `/Users/nikizhao/.codex/generated_images/01a07ada-4529-7723-a4a1-32a42743cbf5/exec-e7228a94-bd6c-48f4-a7ea-c6246bbdc45f.png`
- Workspace target: `/Users/nikizhao/cho-siren-unity/Assets/Resources/Art/Enemies/velvet-hexer-ai-v1.png`
- Runtime resource key: `Art/Enemies/velvet-hexer-ai-v1`
- Alpha pixel counts: 641,803 fully transparent; 931,061 partially transparent.

Final prompt:

```text
Use case: stylized-concept
Asset type: production-ready transparent full-body 2D enemy character sprite for CHO-SIREN, a dark neon sci-fi music fantasy mobile game.
Scene/backdrop: genuinely transparent background with real alpha, isolated character only; no floor, backdrop, ground shadow, checkerboard pattern, text, logos, watermark, UI, or other characters.
Style/medium: polished original Japanese mobile RPG anime illustration, crisp expressive face, fine clean linework, rich cel-shading and soft painted material highlights, readable silhouette at small game size. Original design, not a character from another IP. Tasteful non-sexual costume, clearly adult woman.
Composition: vertical portrait, entire body including head, hands, weapons and boots fully visible, generous transparent padding on all sides; front three-quarter battle-ready stance, eye-level; no cropped limbs or weapon tips. Controlled purple/cyan rim lighting suitable for a dark stage. One character, no contact sheet.
Subject: VELVET HEXER, an adult female ranged curse singer and sonic disruptor. Long flowing deep-red curly hair, focused mysterious expression, elegant black and purple long stage gown with tasteful covered bodice, long boots, gold fine trim. One hand controls a compact floating magical musical-note conductor ring, a precise rose-magenta gold arc with several abstract luminous musical notes around the hand. Confident front three-quarter casting pose. Keep effects localized and silhouette distinct from an armored fighter.
```

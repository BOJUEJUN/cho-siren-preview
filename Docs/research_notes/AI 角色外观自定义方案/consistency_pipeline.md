# Character Consistency and 2D Animation/Asset Pipelines for AI-Customized Outfits (anime 2D, late 2026)

Scope: a Unity idol-raising game with 64 anime characters. Each has a full-body portrait, face-anchored bust/avatar crops, battle skill close-up cut-ins, and pre-rendered ultimate-skill videos. Goal: let players change outfit/hair/makeup (mostly AI-generated, plus official designs) and show the new look in all of those places.

Research date: 2026-11-03. Fifteen web searches and fetches were made. Many sources are third-party blogs or aggregators, flagged where relevant. Dates are given so older information can be spotted.

---

## Q1. Identity/style consistency methods: which combination best keeps a fixed pose so the new image drops into existing layouts?

### Takeaway
In late 2026 the practical default is a multi-reference instruction-editing model (Qwen-Image-Edit-2511 / Qwen-Image 2.1, FLUX Kontext / FLUX.2, Nano Banana Pro/2, Seedream). Give it the existing portrait as the base image and say outright to keep pose, framing and canvas. This keeps the pose well enough for most layouts, but about 30% of edits needed rerolls in one hands-on test. For pixel-exact silhouettes, add a lineart/pose ControlNet or a mask-restricted inpaint, or a per-character LoRA on an open model. A per-character LoRA is cheap in data (roughly 15–50 images) but multiplies operations work across 64 characters.

### Cited Findings
**Multi-reference editing models**
- Qwen-Image 2.1 (open-weight, released 2026-09-20 under Qwen's research license; commercial use needs a separate license). Its ComfyUI "Image Edit" template accepts up to 10 reference images. — [ai-muninn blog](https://ai-muninn.com/en/blog/qwen-image-21-same-face-8-outfits-rtx-5090)
- That blog's same-face, 8-outfit test gives a concrete pose-lock recipe:
  - List every invariant in the prompt (face, mole, hairstyle/hairpin, pose, background, lighting), then name the single change.
  - Add "Keep exactly the same framing, crop and camera distance as <image1>". Without it, coat and suit edits became full-body shots.
  - Keep the latent canvas size equal to the reference. A mismatched canvas raised eye-distance variation to ±18.9%; after the fix it was ±3.9%.
  - Leave shoes out of outfit prompts, because they widen the framing. — [ai-muninn](https://ai-muninn.com/en/blog/qwen-image-21-same-face-8-outfits-rtx-5090)
- Speed and failure rate on an RTX 5090 (32 GB), 40 steps:
  - Speed: about 5.5 s per 768×1024 edit and about 22.5 s at 1536×2048.
  - Failures: about 7 of 23 edits needed a reroll, and the model made slight pose changes to fit clothing.
  - Transparency: an RGBA instruction in the prompt gave a clean alpha PNG. — [ai-muninn](https://ai-muninn.com/en/blog/qwen-image-21-same-face-8-outfits-rtx-5090)
- Qwen-Image-Edit-2511 has an official native ComfyUI workflow. It improves character consistency and multi-person editing and integrates LoRA. — [ComfyUI docs (zh)](https://docs.comfy.org/zh/tutorials/image/qwen/qwen-image-edit-2511.md)
- Community Qwen-based try-on and outfit-change workflows claim to preserve garment shape, colour and fabric while keeping the subject consistent. — [RunComfy Qwen Image 2.1 换装](https://www.runcomfy.com/zh-CN/comfyui-workflows/qwen-image-2-1-outfit-change-in-comfyui-reference-try-on); [RunComfy Consistent Character Creator 3.0](https://www.runcomfy.com/comfyui-workflows/consistent-character-creator-3-0)
- Qwen-Image-Edit versus FLUX.1 Kontext, from one blogger's tests (not a benchmark):
  - Kontext tends to change only what you ask for.
  - Qwen Image Edit edits more freely unless you forbid changes explicitly.
  - Qwen's default anime style is "very classic anime", and other looks need detailed prompts. — [diffusiondoodles](https://diffusiondoodles.substack.com/p/qwen-image-edit-vs-flux1-kontext)
- Kontext prompting guides recommend the following:
  - Start with an explicit statement that identity, pose and framing are preserved.
  - Split compound edits into separate passes, one variable per edit.
  - When the pose must be exact, more prose in the prompt is "usually the wrong move", which points to ControlNet instead. — [eastondev Kontext guide 2026-08](https://eastondev.com/blog/en/posts/ai/20260821-comfyui-flux-kontext-character-consistency/); [selfielabstudio](https://selfielabstudio.com/blog/flux1-kontext-edit-characters-contextually-20260216)
- Black Forest Labs publishes an official character-consistency guide covering multi-reference editing with FLUX.2. — [BFL docs](https://docs.bfl.ai/guides/usecases_editing_character_consistency.md)
- Nano Banana reference limits:
  - Nano Banana Pro, Nano Banana 2 and 2 Lite accept up to 14 reference images, and Pro preserves up to 5 distinct subjects. — [Higgsfield help center](https://higgsfield.ai/creator-hub/help-center/ai-models/how-do-i-use-nano-banana); [Atlas Cloud](https://www.atlascloud.ai/blog/guides/nanobanana-14-reference-images-consistency)
  - Some blogs say 8. That is likely the older original Nano Banana. — [Higgsfield](https://higgsfield.ai/creator-hub/help-center/ai-models/how-do-i-use-nano-banana)
  - One blog's tests found consistency plateaus around 6 good references and can degrade beyond 10. This is a single source. — [selfielabstudio multi-ref guide 2026-03](https://selfielabstudio.com/blog/nano-banana-pro-multi-ref-character-workflow-guide-20260313)
- Google's guidance (as reported second-hand) recommends clear references and a distinct name for each character. — [coverr blog](https://coverr.co/blog/nano-banana-pro-ai-image-character-consistency-2026)

**Per-character LoRA**
- Typical anime character LoRA datasets on Illustrious/SDXL are small: Kaguya 42 images, Chika Fujiwara 42 images across outfit tokens (waiIllustriousSDXL v17), Hayasaka Ai 35 images. — [HF Kaguya dataset](https://huggingface.co/datasets/abbuibuibui/kaguya-Dataset); [HF Chika dataset](https://huggingface.co/datasets/abbuibuibui/ChikaFujiwara-Dataset); [HF Hayasaka dataset](https://huggingface.co/datasets/abbuibuibui/HayasakaAi-Dataset)
- FLUX LoRA guides suggest the following:
  - 15–30 images is usual for a character.
  - Fewer than 10 may underfit, and more than 50 redundant views can overfit.
  - FluxGym is an open-source web UI for training on limited hardware. — [ThinkDiffusion FluxGym guide](https://learn.thinkdiffusion.com/make-your-character-style-lora-stand-out-easy-lora-training-with-fluxgym/)
- Community reports say many working SDXL character LoRAs use fewer than 40 images, and caption quality matters more than count. — [Reddit snapshot 2026-03](https://reddit.sentinel-team.org/posts/1ry3uct/snapshots/2026-03-20T17%3A36%3A49.07391Z)
- There is a FLUX Kontext workflow that builds a LoRA training set from one character image. This matters when a character has only a few official images. — [Floyo workflow](https://www.floyo.ai/workflows/flux-kontext-single-image-to-charact-sw6872x86u3k)
- A 2026-08 guide covers training a LoRA inside ComfyUI. — [eastondev](https://eastondev.com/blog/en/posts/ai/20260828-comfyui-train-your-own-lora/)

### Inferences
- **Recommended combination for drop-in replacement:** official portrait as image1, plus an outfit reference (official design sheet or player-chosen garment) as image2.
  - Use a multi-reference edit model with explicit invariants and a framing-lock sentence, at exactly the source canvas size.
  - Then composite only the changed region back over the original using a garment/body mask, so the face, hands and outline stay pixel-identical. This keeps face-anchored crops valid.
  - Use ControlNet lineart/openpose or a depth lock (open models only: Qwen/FLUX/SDXL in ComfyUI) as a second line of defence when the edit model drifts the pose.
- **Closed APIs versus open models:** closed APIs (Nano Banana, Seedream, FLUX Kontext Pro) are easier to run but give no ControlNet hooks. The most deterministic pose lock is an open model in ComfyUI with inpaint masks and ControlNet.
- **Local tooling:** this repo's environment already has LightAI (Nano Banana Pro/2, Kling, Jimeng) and Volcengine Ark (Seedream/Seedance) skills installed. That lowers the cost of prototyping with the closed-API route. This comes from the local environment, not a web source.
- **LoRA trade-off for 64 characters:** at roughly 15–50 curated images each, LoRAs mainly help keep the art style and face stable under big changes such as new hairstyles. The cost is 64 models to train, version and serve, so they suit a self-hosted pipeline more than an API one.
- **IP-Adapter, InstantID, PuLID** are 2023–2024-era adapters (older; from training knowledge, not re-verified). They were built mostly for photoreal faces. For anime in 2026 they appear to have been largely superseded by native multi-reference edit models.
- **Validation:** an automated check is useful. Compare face landmarks, or the eye-distance metric the ai-muninn post used, against the original, and reroll anything outside tolerance.

### Gaps
- No official or controlled benchmark compares pose and silhouette retention across Nano Banana Pro, FLUX Kontext/FLUX.2, Qwen-Image-Edit-2511/2.1 and Seedream 4.x/5 for anime full-body portraits.
- No 2026 source verified ControlNet lineart support for Qwen-Image-Edit or FLUX.2 specifically.
- No 2026 figures were found for LoRA training cost (GPU-hours or $) or for PuLID/InstantID performance on anime faces.
- Seedream's multi-reference limits and anime quality were not researched in this pass.

---

## Q2. Layered "paper-doll" approaches: generating outfits as layers and AI layer decomposition

### Takeaway
Two research-grade open tools now turn a single illustration into editable RGBA layers:
- **Qwen-Image-Layered** (Apache-2.0, variable layer count, ComfyUI support).
- **See-through** (arXiv 2602.03749, Feb 2026). It is anime-specific, outputs 19 semantic body-part classes as an inpainted PSD of up to 23 layers, and its labels come from Live2D.

Both make a "base body + garment layers" pipeline feasible as a draft step, but neither yields production-clean layers without artist cleanup.

### Cited Findings
- **Qwen-Image-Layered**
  - Decomposes an image into multiple RGBA layers, with variable layer counts (demos show 3 and 8). Decomposition can be recursive, splitting any layer again.
  - Licence: Apache 2.0. — [GitHub QwenLM/Qwen-Image-Layered](https://github.com/QwenLM/Qwen-Image-Layered)
  - Paper: arXiv 2512.15603 (Dec 2025). Architecture: an RGBA-VAE shared with RGB, a VLD-MMDiT for variable layer count, and multi-stage training from a pretrained generator. — [paper mirror](https://piefed.jeena.net/c/stable_diffusion/p/294757/qwen-image-layered-towards-inherent-editability-via-layer-decomposition)
  - Works best with clear, well-defined objects. Each layer can be edited, moved or recoloured independently. — [Replicate](https://replicate.com/qwen/qwen-image-layered)
  - Has a ComfyUI workflow template and vLLM-Omni support, plus GGUF quantizations. — [ComfyUI workflow](https://comfy.org/fr/workflows/image_qwen_image_layered-9ff401a65647/); [GitHub](https://github.com/QwenLM/Qwen-Image-Layered)
- **See-through: Single-image Layer Decomposition for Anime Characters** (arXiv 2602.03749, 2026-02-03)
  - Input and output: takes one anime illustration and outputs inpainted RGBA body-part layers (occluded regions completed) with a stratified draw order. It is built on SDXL, with a Body Part Consistency Module and Marigold-based pseudo-depth for ordering.
  - Training data: about 9,102 commercial Live2D models (7,404 train / 851 val / 847 test). Labels are snapped to Live2D ArtMesh masks and refined with SAM.
  - Output: 19 classes, exported as a layered PSD. Hair, Handwear, Topwear and Bottomwear are further split by depth (K-Means, K=2).
  - Speed: about 74 s per 1024² decomposition plus about 10 s depth on one RTX 4090.
  - Limitations: minor overlaps; unstable sublayer boundaries; very fine hatching gets softened; footwear is not stratified; hair tips need checking; artists noticed "AI-like" textures and wanted finer splits such as separate left and right arms; backgrounds are not reconstructed. — [arXiv HTML](https://arxiv.org/html/2602.03749v1)
- See-through's code is at github.com/shitagaki-lab/see-through. It uses LayerDiff 3D and fine-tuned Marigold and exports up to 23 PSD layers. — [search summary of arXiv/GitHub](https://arxiv.org/abs/2602.03749v1); [GitHub mirror](https://github.com/mitmul/see-through)
- Third-party testers describe See-through as an "automatic PSD draft generator", not a one-click Live2D rig. Integrations exist for ComfyUI-See-through, PachiPakuGen and StretchyStudio. — [lilting.ch test](https://lilting.ch/en/articles/see-through-anime-layer-decomposition); [gamedev.net](https://gamedev.net/news/1463-see-through-single-image-layer-decomposition-for-anime-characters/)
- There is a public Hugging Face Space demo (NF4-quantized). — [HF Space](https://yuyuanan-see-through-nf4.hf.space/)
- Qwen-Image 2.1 can emit an RGBA PNG with alpha when prompted. This helps generate isolated garment layers directly. — [ai-muninn](https://ai-muninn.com/en/blog/qwen-image-21-same-face-8-outfits-rtx-5090)

### Inferences
- **Viable paper-doll pipeline for this game:**
  1. Run See-through once per official portrait to get a base body PSD (skin, face, hair and garments as separate layers). An artist cleans it up.
  2. For a new outfit, generate the full-figure edit (Q1), then decompose it with See-through or Qwen-Image-Layered, or mask out just the Topwear/Bottomwear region.
  3. Composite the new garment layers over the fixed body and face layers.
  4. Hair and makeup become separate swappable layers on the same base.
- **Why this helps:** the face stays the identical original pixels. That keeps bust and avatar crops and expression variants valid, and it limits AI artefacts to the garment.
- **Garment–body fit:** a pure "generate garment in isolation, then overlay" approach (LayerDiffuse-style, 2024, older; not re-verified) is risky because of fit. A garment generated without the body tends to misalign at the neckline, sleeves and hands. Generating in context and then decomposing fits better.
- **Hair is the hardest layer:** hair overlaps the face both in front and behind, which is exactly the case See-through targets with its stratified order. Hairstyle changes will still need the most cleanup.

### Gaps
- No quantitative quality comparison between Qwen-Image-Layered and See-through on anime full-body art was found.
- LayerDiffuse's 2025–2026 status, and any newer "garment-only RGBA generation conditioned on body" model, were not researched in this pass.
- No developer postmortem was found from a shipped gacha or idol game that uses AI decomposition in production.

---

## Q3. Live2D Cubism and Spine: outfit swapping, AI auto-split/rig, and effort per character

### Takeaway
- **Spine** has a mature runtime skin and attachment API made for mix-and-match outfits.
- **Live2D** swaps outfits through part visibility or texture replacement on a rig authored with those parts in place.
- **AI help:** Live2D ships AI auto-deformer and auto-sway generation (Cubism 5.1). AI part separation exists as research (See-through, Bunraku 2026-07) and open-source non-Live2D rigs (img2rig). No mainstream tool turns one image into a finished Live2D .moc3.
- **Practical consequence:** an AI-generated outfit can only drop into a Live2D or Spine rig if it is decomposed to match the rig's existing part and mesh layout.

### Cited Findings
**Spine (Esoteric Software)**
- Skins control which attachments a skeleton uses. A skeleton has one active skin at a time plus the "default" skin as a fallback. — [Spine Runtime Skins](https://esotericsoftware.com/spine-runtime-skins)
- Outfits can be built from item skins: create a skin in code and add attachments from other skins. Spine 3.8+ has an improved Skin API for this. — [Spine Runtime Skins](https://esotericsoftware.com/spine-runtime-skins); [spine-unity Mix and Match](https://en.esotericsoftware.com/spine-unity-mix-and-match)
- The spine-unity workflow is to prepare a skin per appearance or equipment item, combine them into a new skin at runtime, and animate with a template (dummy) skin. The docs include a "Mix and Match Skins" sample scene. — [spine-unity Mix and Match](https://en.esotericsoftware.com/spine-unity-mix-and-match)
- Changing skins or attachments on shared SkeletonData affects every instance. The runtime also supports building attachments from sprites (AttachmentTools). — [spine-unity attachments & AttachmentTools](https://esotericsoftware.com/spine-unity-attachments-and-attachmenttools)
- Esoteric staff advise creating or copying a new skin at runtime rather than modifying the default skin. — [Esoteric forum 11258](https://esotericsoftware.com/forum/d/11258)
- Note: some spine-unity pages date from the 3.6/3.7 era (older). Check them against the current runtime guide.

**Live2D Cubism**
- Cubism 5.1.00 (2024-07, older but still current) shipped AI that reads the PSD and auto-generates a full-body deformer setup for humanoid models, plus AI auto-generated hair and accessory sway. — [AUTOMATON 2024-07-30](https://automaton-media.com/articles/newsjp/20240730-303793/); [Live2D official](https://www.live2d.com/en/?p=4424)
- A 2024 Live2D staff interview says AI face-motion generation gives a base motion after parts are separated, which creators then refine. — [AUTOMATON interview 2024-08](https://automaton-media.com/articles/reportjp/live2d-20240805-304478/)
- An AI "Material Separation" Photoshop plugin (R1 alpha1, March 2024) cut out and filled parts. It is not confirmed to have become a standard feature. — [search summary; Live2D/MoguraVR](https://www.moguravr.com/?p=248431)
- Community posts mention a Cubism Editor/SDK 5.4 alpha (July 2026) and third-party layer-prep tools that split hair and outfits. These are not official release notes. — [Live2D community 2379](https://community.live2d.com/discussion/2379/seeking-feedback-on-layer-prep-tool-for-live2d-setup); [community 2381](https://community.live2d.com/discussion/2381/quick-question-on-layer-prep-workflows)
- Freelance Live2D rigging listings say standard and premium models work best with 30–60+ separated layers. — [Fiverr listing](https://fiverr.com/collins_aguns/rig-your-anime-vtuber-model-for-vtube-studio-live2d-commission)
- **Bunraku** (arXiv 2607.27348, July 2026) is a research prototype. From one illustration it generates ordered RGBA layers, a deformation mesh per layer, and parameter-driven keypose vertex offsets. — [papers.cool](https://papers.cool/arxiv/2607.27348)
- **img2rig** (Apache-2.0) turns one AI illustration into a layered rig with procedural motion. It explicitly produces no Live2D-format assets. — [GitHub SSnemo/img2rig](https://github.com/SSnemo/img2rig)
- See-through's authors call it a starting point for Live2D artists, not an image-to-Live2D pipeline. — [arXiv 2602.03749](https://arxiv.org/html/2602.03749v1)

### Inferences
- **Fit with this game's art:** the existing assets are static portraits plus pre-rendered videos, not rigs. Introducing Live2D or Spine would add a per-character rigging cost before any outfit system exists. From the 30–60+ layer figure, a Live2D model is clearly a multi-day professional job per character. No source gave a time or cost figure, so this is not quantified.
- **Live2D outfit swaps:** these only work if the new outfit is split into the same parts as the rig's ArtMeshes (sleeve L/R, torso, skirt front/back). A garment that changes silhouette, such as a long coat versus a short skirt, needs new meshes and deformers, so player-driven unlimited AI outfits cannot simply be "skinned" onto a fixed rig.
- **Spine is the friendlier runtime for mix-and-match**, and AttachmentTools can build attachments from runtime sprites. In principle, an AI-generated garment PNG cut to a known slot template could be attached at runtime. Silhouette-changing garments still need new mesh weights.
- **A realistic hybrid:** keep a small set of rig-compatible "template garments" (official designs) for animated contexts. AI recolours, retextures or restyles inside those garment masks, which keeps the silhouette, while static portraits allow free-form AI outfits.

### Gaps
- No verified 2026 official Live2D release note was found confirming AI auto part-splitting.
- No sourced per-character hour or cost estimate exists for Live2D or Spine rigging, and no evaluation of Bunraku's quality or code availability.
- Live3D and similar commercial auto-rig startups, and CharacterGen (2024, image-to-3D A-pose), were not verified in this pass.

---

## Q4. Propagating a new outfit to derived assets: busts/avatars, close-ups, and image-to-video

### Takeaway
- **Static derivatives are cheap.** If the face pixels and canvas are kept identical (Q1/Q2 compositing), face-anchored bust and avatar crops can be re-cut automatically with the existing crop rectangles. Skill close-ups can be regenerated by the same edit model using the new full-body as reference.
- **Video is the expensive part.** Image-to-video with the new portrait or a key frame as the start frame costs roughly $0.03–0.57 per second of output via APIs (Kling 3/4, Seedance 2/2.5), depending on model, resolution and audio. Self-hosted Wan 2.2 takes about 2–9 minutes per 5 s clip on a 4090-class GPU.
- **Video consistency limit:** identity and outfit consistency across a multi-shot ultimate video is still the weakest link.

### Cited Findings
- Kling 4.0 and Seedance 2.5 pricing (aggregator, dated 2026-10-29; not confirmed against official pages):
  - Kling 4.0: about $0.157/s at 720p and $0.235/s at 1080p.
  - Seedance 2.5 on BytePlus: about $0.231/s and $0.569/s. — [toolcolumn Kling 4 vs Seedance 2.5](https://www.toolcolumn.com/learn/kling-4-vs-seedance-2-5)
- Seedance 2.5 image-to-video pricing conflicts across providers: about $0.134/s on Atlas Cloud versus about $0.473/s at 720p with audio on fal.ai. Some platforms bill by tokens and do not bill input images. — [Atlas Cloud](https://ask.atlascloud.ai/seedance-2-5-image-to-video-api-provider-pricing); [fal.ai Seedance 2.5 i2v](https://fal.ai/models/bytedance/seedance-2.5/image-to-video); [Segmind](https://www.segmind.com/models/seedance-2.5/pricing)
- Seedance 2.5 reportedly leads the Artificial Analysis image-to-video leaderboard. Kling 4.0 is not yet listed there. — [aggregator search summary](https://www.krea.ai/blog/seedance-2-5-vs-kling-3-0-which-ai-video-model-should-you-choose)
- Older (early 2026) pricing:
  - Kling 3.0 Pro with audio: about $0.168/s on fal.ai.
  - Seedance 2.0 720p: about $0.30/s on fal.ai standard, with a Fast 480p tier at about $0.15/s. — [fal.ai Seedance 2.0 vs Kling 3.0](https://fal.ai/learn/tools/seedance-2-0-vs-kling-3-0); [devtk pricing 2026](https://devtk.ai/en/blog/ai-video-generation-pricing-2026/)
  - A conflicting figure of about $0.029/s for Kling 3.0 is likely a different tier. — [atlascloud cheapest providers](https://www.atlascloud.ai/blog/guides/cheapest-api-provider-seedance-2-kling-wan)
- Wan 2.2 (open source, self-hosted) timings:
  - TI2V-5B on an RTX 4090: about 9 min per 5 s 720p clip, or about 4 min at 480p.
  - 14B: about 4 min for 81 frames at 480p unquantized (RunPod). — [evetech](https://evezone.evetech.co.za/daily-drop/wan-2-2-hits-local-rigs-720p-video-on-24gb-of-vram); [RunPod guide](https://runpod.io/articles/guides/comfyui-wan-2-2)
  - With the 4-step Lightning LoRA: about 524 s down to about 138 s (RTX 4090D, 640×640, 81 frames, ComfyUI doc), and 50 min down to 5.6 min on an RTX 3060 12GB. — [ComfyUI Wan2.2 Fun Inp doc](https://docs.comfy.org/tutorials/video/wan/wan2-2-fun-inp); [danielrusnok](https://danielrusnok.substack.com/p/0-ai-reels-on-a-12gb-gpu)
- Wan 2.2 supports style and character LoRAs. — [MindStudio](https://www.mindstudio.ai/blog/what-is-wan-2-2-video-open-source)
- Most of these prices come from secondary aggregators and conflict with each other. Treat them as ranges, not quotes.

### Inferences
- **Bust/avatar crops:** the project already uses face-anchored crops. If outfit edits keep the original canvas and the face is composited back unchanged, the same crop rectangles stay valid with no re-detection. If the face or hair changes (hairstyle or makeup), re-run face detection and anchoring, and recheck the hair silhouette at the crop edges.
- **Skill close-ups:** regenerate each close-up by editing the existing close-up, with the new full-body as image2 ("apply the outfit from image2, keep everything else"). This is a per-asset edit call at image-model cost and latency (seconds each). Expect reroll rates similar to Q1 (about 30%).
- **Ultimate videos, worked example:** a 10 s clip at about $0.15–0.57/s is roughly $1.5–5.7 per character-outfit at API list prices, before rerolls.
  - Multiplied across 64 characters, every player's custom outfit, and rerolls, per-player on-demand video becomes a live-ops cost and moderation problem.
  - API latency was not sourced. Self-hosted Wan runs about 2–9 min per 5 s clip, so the video cannot be generated in real time.
- **Recommended pattern for videos:**
  - (a) For player AI outfits, keep the ultimate video costume-agnostic (effects, silhouettes, close-ups of the face, which is unchanged), or overlay the static new portrait in a 2.5D parallax shot.
  - (b) Reserve full video regeneration for official outfits, which are produced once and QA'd by the team.
  - (c) Use start-frame (and end-frame where supported) image-to-video so the first frame matches the new portrait exactly.
- **Content safety:** every AI-generated outfit needs moderation (filters on prompts and outputs). This applies to images and even more to video.

### Gaps
- No sourced latency (time-to-result) figures for Kling, Seedance or Veo API calls.
- No 2026 data on Veo 3.x or HunyuanVideo i2v pricing and anime quality.
- No measured outfit and identity consistency figures for multi-shot anime video.
- Official Kling and BytePlus price pages were not fetched, and aggregator prices conflict.

---

## Q5. The 3D alternative: VRoid/VRM toon models with modular clothing, versus 2D

### Takeaway
3D (VRoid/VRM, MToon or lilToon toon shading) makes modular clothing structurally natural. One model renders every pose, crop and close-up, and animated ultimates come free with the outfit. The costs are a style shift away from hand-painted 2D portraits, clipping and fitting problems, and shader and bone-standard lock-in. Converting 64 existing 2D characters to 3D is a full re-production, and AI-generated outfits in 3D are much less mature than 2D image editing.

### Cited Findings
- VRoid Studio added an outfit-swap feature in beta, around November 2025 according to the article. Swaps use XAvatar/XWear files made with the Unity XWear Packager, which needs VCC and Unity familiarity. It supports per-bone size adjustment, for example the hips only. — [MoguraVR](https://www.moguravr.com/?p=259783)
- vear Dresser (free, MIT, Unity editor extension) layers VRoid outfits. It cannot resize clothing, so outer layers clip, and heavy layering inflates VRM file size. — [BOOTH vear Dresser](https://booth.pm/ko/items/2667352)
- Outfits are often tied to specific toon shaders: one is built against lilToon 1.7.2, another uses a UnityChanToonShader-based shader. — [BOOTH outfit listing](https://booth.pm/en/items/5104726); [BOOTH](https://booth.pm/ko/items/4770852)
- MToon UV scrolling can animate clothing patterns. — [scrapbox tutorial](https://scrapbox.io/ke456memo/%2321_MToon%E3%81%AEUV%E3%82%B9%E3%82%AF%E3%83%AD%E3%83%BC%E3%83%AB%E3%81%A7%E6%9C%8D%E3%81%AE%E6%A8%A1%E6%A7%98%E3%81%8C%E5%8B%95%E3%81%8FVRM%E3%82%92%E4%BD%9C%E3%82%8B)
- Modular Avatar is used for armature-following clothing setups. — [zenn.dev](https://zenn.dev/hsclair/articles/4e4a96d9883cd9)
- LightAI tooling offers Tripo image-to-3D and multiview-to-3D. This is from the local environment's skill list, not a web source. Generic image-to-3D does not output rigged, modular, toon-ready anime avatars.

### Inferences
- **Pros of 3D for unlimited customization:**
  - A garment is a mesh plus texture bound to a skeleton, so it appears automatically in every portrait, bust, cut-in and real-time ultimate.
  - Recolours and texture variants are trivial, and AI texture generation onto a fixed garment UV is a tractable middle ground.
- **Cons of 3D:**
  - It loses the hand-painted 2D look that the AGENTS.md contract protects ("reproduce reference 1:1", "never replace homepage characters").
  - New silhouettes still need 3D modelling and weight painting, which AI does not yet do reliably for anime garments.
  - Clipping and shader lock-in.
  - All 64 characters would need re-authoring, plus a new render pipeline in Unity.
- **Middle path:** keep 2D, and for animated ultimates use pre-rendered, outfit-agnostic or official-outfit-only videos. 3D only pays off if the product pivots to real-time 3D performance scenes (idol stage shows). VRM's modular clothing ecosystem is strongest there.

### Gaps
- No source was found for runtime (in-build) UniVRM outfit swapping in a shipped game, or for AI-generated rigged anime garment meshes in 2026.
- CharacterGen and other 2024-era image-to-3D anime research (older) was not re-verified.
- No developer postmortem compares 2D versus 3D customization cost for gacha or idol games.

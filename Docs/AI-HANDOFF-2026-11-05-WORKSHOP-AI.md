# AI handoff — 2026-11-05: 创意工坊 AI 换装 (in game) + 星钻 cost

Read `AGENTS.md` first (reply style: Chinese, call the user 少爷, very short, plain words). This continues
`Docs/AI-HANDOFF-2026-11-03-CONTINUE.md` and `Docs/FULL-RELEASE-2026-11-04-0935.md`.

## State in one paragraph

Released game is still **0.9.35**. Everything below is committed on branch `release/music-0.3.5-20260909` but
**not released**: 经纪人 account level (d386c5b), 创意工坊 replacing 专辑制作 (cc5778b … d654325), in-game AI 换装
through Tencent Cloud (d499f3a), 星钻 cost (f6e5ca8), and the uncommitted Banana 2.1 switch described below. The Mac
app `/Applications/幻域魅声.app` has everything up to f6e5ca8 (Seedream era); it needs one more update after the
last fix. The user asked to try on Mac first, then release 0.9.36 to all channels when they approve.

## How AI 换装 works

1. Game (`Assets/Scripts/WorkshopAi1104.cs`) PNG-encodes the member still (≤1024 px tall), POSTs
   `{device, member, prompt, image}` to `WorkshopAi1104.Endpoint`
   = `https://1318245308-d7n1q0c0wj.ap-guangzhou.tencentscf.com` `/dress`, then polls `GET /dress/<task>` every 2.5 s
   (180 s max), downloads the result, `KeyGreen` removes the green backdrop (border-connected flood fill + enclosed
   gaps + soft edge + despill), `FitToStill` scales/places the figure like the still so face close-ups still land.
2. Tencent Cloud SCF function `cho-siren-ai-dress` (ap-guangzhou, Python 3.9, source `Tools/ai-dress-fn/index.py`,
   stdlib only) holds the keys in env vars, enforces **5 per device per day and 300 per day for everyone** (counts in
   COS `ai-quota/<date>.json`), and calls RunningHub.
3. Model: **Nano Banana 2.1 economy** on the global site `https://www.runninghub.ai/openapi/v2/rhart-image-n-nb21/image-to-image`
   (body `prompt, imageUrls[dataURI], aspectRatio "9:16", resolution "2k"`; ~$0.03, ~40 s, 1536×2752, pure green
   backdrop). If the global site refuses at submit time it falls back to **Seedream 4.7** on the China site
   (`runninghub.cn/openapi/v2/seedream-v4.7/image-to-image`, ¥0.15, ~16 s). Task ids are prefixed `b:` / `s:` so the
   query goes to the right site. Banana 2.1 is NOT available on the China site (compliance, error 40310).
4. Player cost: **10 星钻 per finished outfit** (`GameModel.AiDressDiamondCost1105` in `GameModelWorkshop1104.cs`),
   charged only when the outfit arrives; not enough 星钻 → toast, nothing sent.
5. Style chips map to full outfit descriptions (`WorkshopAi1104.StylePrompts` / `PromptFor`) — short tags like
   「偶像演出服」 alone made the model keep the old outfit.

## Secrets (never print, commit or paste them)

- China-site RunningHub key: `~/XiaomiMiMoProjects/krea-studio-models/.env.local` (`RUNNINGHUB_API_KEY`).
- Global-site RunningHub key: `~/.cho-siren-rh-ai.key` (the user pasted it in chat; suggest they rotate it later).
- Tencent credentials: `~/.cho-siren-cos.json` (also used for SCF; user granted `QcloudSCFFullAccess`).
- Deploy script: `Tools/ai-dress-fn/deploy_fn.py` (run with a venv that has `tencentcloud-sdk-python`) — zips `index.py`, Create/UpdateFunctionCode, then
  UpdateFunctionConfiguration with env `RUNNINGHUB_AI_KEY, RUNNINGHUB_API_KEY, COS_BUCKET, COS_REGION,
  COS_SECRET_ID, COS_SECRET_KEY`.
  The HTTP trigger (function URL, AuthType NONE) already exists; redeploying code keeps it.

## Last step in progress (do this next)

1. Uncommitted: `Tools/ai-dress-fn/index.py` (Banana 2.1 + fallback, already deployed and verified end to end:
   `b:` task done in 45 s) and `Assets/Scripts/WorkshopAi1104.cs` (HTTP refusals now show the service's message,
   e.g. 「今天的 AI 换装次数用完了」, instead of 「网络连接失败」; 429 sets `RemainingToday = 0`).
2. The last live in-game test (`play4`) failed with 「网络连接失败」 after 1 s. Most likely cause: the Unity test
   device already used its 5 tries today (earlier run reported 「今天还能换 0 次」), and the old client hid the 429.
   Verify after the fix: rerun the capture (it now shows the real message); to really test generation, wait for the
   next day (Beijing time) or temporarily raise `AI_DRESS_DAILY_LIMIT` env on the function, then restore 5.
3. Run tests (one Unity process at a time):
   - `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter WorkshopKey1104Tests ...`
     (set `WORKSHOP_KEY_SAMPLE=<png>` to also key a real sample; 1536×2752 keys in ~220 ms)
   - `HOMECENTER_PHASE=ai1105 WORKSHOP_AI_LIVE=1 Unity ... -testPlatform PlayMode -testFilter TopUp1104CaptureTests`
     (live step costs one model call; screenshots land in `Builds/qa-logs/topup-1104/workshop-ai-result*.png`)
4. Quit the game (`osascript -e 'tell application "幻域魅声" to quit'`), run
   `Builds/qa-logs/local-preview/update-desktop.sh`, commit only these files.
5. When the user approves: release **0.9.36** (never 0.3.x) to web / PC / macOS / Android / update manifest, replace
   the Mac app, install on 伯爵君's iPhone keeping saves (no auto-launch; 五条悟 on hold; iPhone signing profile
   expired 11-06 19:34 — the user must re-login in Xcode). Follow `Docs/FULL-RELEASE-2026-11-04-0935.md` and
   `Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md`. WebGL: `WorkshopRecorder1104` mic is native only; AI works over HTTPS.

## Costs the user knows

Seedream ¥0.15/张, Banana 2.1 economy ~$0.03 (~¥0.2)/张, official $0.04 but ~100 s. Global balance was ~$7.7.
Worst case per day = 300 × $0.03 ≈ $9. Players pay 10 星钻 (=1 元 at the planned rate); payment channel is still
closed (`PaymentOpen1104 = false`).

## Other open items

- Domain enzoban.com / enzoban.cn: purchase waits for the real-name template review; confirm item and price with the
  user before ordering; the user pays by scanning.
- Run `Tools/cos_prune.py --apply` after releases.
- The user's Claude-in-Apple purchase failed; they were told to cancel in Apple settings and request a refund.

# CHO-SIREN handoff · 2026-10-20 (live v0.3.87 / R78)

Read this first, then `AGENTS.md` and `Docs/AI-HANDOFF-2026-10-19-R73-STOP.md` (publish safety).

## How to work with the owner

- Talk to the owner in short, simple Chinese and call them **少爷**.
- Only read and generate on the user's machines. No installs, no system or settings changes, no deletes, no passwords.
- Design taste:
  - UI in the Persona 5 / ATLUS language: inspired by it, never copied.
  - NIKKE-like layouts.
  - Keep the original art.
- For any new visual design, show options first (AI concept images or an Artifact preview) and let the owner pick.
- Every release ships both targets: web and the iOS simulator build.
- **Image generation:** the local ChatGPT Codex CLI generates images, including true transparent backgrounds.
  - Binary: `/Applications/ChatGPT.app/Contents/Resources/codex-cli/bin/codex`.
  - Command:
    ```
    codex exec --skip-git-repo-check -s read-only -c model_reasoning_effort='"low"' "<prompt; say: call image_gen with transparent_background: true; reply only the saved path>" [-i ref.png] < /dev/null
    ```
  - Put `-i` after the prompt. Output lands in `~/.codex/generated_images/`.
  - Transparency is a real tool parameter; check `transparentBackground: True` in `~/.codex/sessions/.../rollout-*.jsonl`.

## Build / test / release (copy-only, never release.py / push.sh)

1. **EditMode tests:** expect 577/577.
   ```
   Unity -batchmode -projectPath . -buildTarget WebGL -runTests -testPlatform EditMode -testResults x.xml -logFile x.log
   ```
   - Grep the log for `error CS`.
   - `TurnBattlePacing1020Tests` prints `TURN-PACING` lines with rounds, seconds, rerolls and ultimates per stage.
2. **Bump the version:** `bundleVersion` in `ProjectSettings/ProjectSettings.asset`. Next is 0.3.88 / R79.
3. **WebGL build:**
   ```
   Unity -batchmode -nographics -quit -projectPath . -buildTarget WebGL -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWebGL -buildOutput Builds/WebGL-rNN
   ```
4. **Stage the web repo:**
   - Diff the build against `~/cho-siren-web` and copy only new or changed files. Normally that is the 4 Build files and `index.html`.
   - Archive the old record: `release.json` → `releases/R<prev>.json`.
   - Rotate `build-versions.json`: current → previous → retained. Keep every historical Build.
   - Update `package.json`, `release.json` and `RELEASES.md`.
   - `scripts/verify-webgl.mjs` pins the battle manifest at **84 entries** plus its SHA. Update both if you add layers.
5. **Check:** `npm run check` must show 110/110.
6. **Commit and push:**
   - `git add` explicit paths only; never `-A`.
   - `git push --atomic origin C:refs/heads/main C:refs/heads/backup/v<ver>`.
   - Wait until Pages `package.json` shows the new version.
7. **iOS simulator build:** `BuildIOS -iosSimulator`, then `xcodebuild` to `Builds/iOS-DerivedData`.
   - Simulator UDID: `C75AD7D8-8C13-477C-A291-E3DE282D46A1`.
   - Read app logs with `xcrun simctl launch --stdout=~/cho-siren-unity/Builds/qa-logs/app.out`.

## What changed 2026-10-20 (v0.3.82 → v0.3.87)

### Loading and the title screen

- **Resumable first download:** 4 MB byte ranges kept in Cache Storage, in the WebGL template's `resumableData()`.
- **Startup watchdog:** counts visible time only.
- **Asset gate:** the 「正在准备界面」 gate now has a ✕ that offers 后台准备 or 继续等待 (`ChoSirenApp.ShowReferenceArtGate`).
- **Image quality:**
  - 流畅 keeps full-resolution textures and caps at 30 fps.
  - The web render density never drops below 2×.
- **Title screen:** smaller side buttons, and the CADPA 16+ badge sits alone in the top-left.
- **Lobby top bar:** sits closer to the Dynamic Island (`SafeAreaFitter` uses 72% of a tall top inset).

### Turn-based battle

`Systems/Tactics/TurnBattle1020.cs` and `Panels/TacticsBattlePanel.Turn1020.cs`:

- **Turn flow:**
  - Turns follow speed order; each member chooses 普攻 / 技能 / 大招.
  - Enemies telegraph specials one turn ahead.
  - 20 rounds per fight; bosses get 30.
- **Defaults:** battles start on 自动, with no "XX 的回合" caption.
- **Balance:**
  - Damage multiplier ×13 for members, ×8 for enemies; heals and poison ×4.
  - +6% reroll charge per member action.
  - Fights last about 45–72 s at the recommended level.
- **Ultimate limits:**
  - Energy +10 per own action.
  - The whole team may cast one ultimate per round, and each member at most 3 per battle.
  - Ultimate damage ×1.6.
- **Auto mode:** tapping a full-energy member queues her ultimate for her turn without switching to manual.

### Ultimate show and battle visuals

- **Ultimate show:** `UI/BattleUltimateShow1020.cs`.
  - Plays each member's clip from `StreamingAssets/UltimateVideos/{wubai,xingli,yeying,feiyin}-ult.mp4`, listed in `Resources/UltimateVideos/manifest.json`.
  - A tap ring scores PERFECT +10% or GOOD +5%.
  - In auto mode the game taps late in the GOOD window, so the player can still beat it to PERFECT.
  - After the cast, `UltimateImpact` slams the skill name onto the stage and shakes it.
- **Dice:**
  - A die selected for reroll lifts, glows and grows (`UI/DiceRerollMark1020.cs`); there is no 待重投 text.
  - Scoring dice show only +N.
- **Other battle visuals:**
  - The energy bar keeps its white rim, and the fill rises smoothly.
  - The reroll button has a soft feathered glow (`reroll-halo`).
  - The yellow pillar is hidden.
  - The basic-attack burst portrait is scaled to 60%.
  - The guard ring sits on the portrait.

### Menus, economy and transitions

- **Red dots:** all badges use one round red dot (`DotSprite`) with no count.
- **成员 page:** shows owned members only by default (范围：已拥有 / 全部图鉴), with no pager when there is one page.
- **Diamonds** are now a scarce, paid currency:
  - Free diamonds come to about 300–400 per week.
  - Daily, idle and first-clear rewards were cut, and sign-in moved mostly to gold.
  - Values live in `economy.json`, `GameModelMail.cs` (SignInRewards, welcome mail 150), `GameModel.cs` constants and `tactics.json` DiamondFirstClear.
- **Transitions:** returning to the lobby from menu pages has no transition; only the stage flow plays one.

## Open requests from the owner (next work, in order)

1. **Ultimate hit effects per member:** after the show, a distinct, polished attack or impact effect, such as a projectile or a slash for damage. Use GPT transparent art and animate it.
2. **Grade stamp art:** replace the black-bar PERFECT / GOOD / MISS stamp with designed transparent text art from GPT `transparent_background`.
3. **Reroll glow:** make it flow like the lobby ink (`PsdHomeAlbumInkMotion` / R02 sheets) while still hugging the bar outline softly.
4. **Volume:** add sliders for music, SFX and voice instead of on/off. Default to full volume so the game follows the system volume.
5. **Sound:** research and replace the synthesized SFX with high-quality material. Kenney CC0 needs the owner's OK to download; an AI SFX service is another option. Get a list approved first.
6. **Per-member basic attacks:** a distinct, polished effect for each member, for example lightning.
7. **Dice roll animation and FX:** make several versions for the owner to choose from.
8. **Level map page:** the city background is too busy and the UI too cluttered; simplify. Redesign the 章节奖励 and 章节任务 popups in the P5 style.
9. **Story:**
   - Characters look pasted into the scene; generate real in-scene CGs.
   - The dialogue box is too tall; slim it down.
   - Give the 经纪人 (producer) a generated avatar.
   - Several P5-style dialogue-frame versions are still owed to the owner.
10. **Transition:** storyboards T1–T4 (calling card / spotlight eyes / equalizer / shattered mirror) were sent; wait for the owner's pick, then build it.
11. **Later:** the result screen choice and voice auditions (see the R73 handoff).

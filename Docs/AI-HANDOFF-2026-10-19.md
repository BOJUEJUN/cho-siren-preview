# 停工交接：本地 v0.3.82 / R73c 已构建，未发布

用户要求本AI停工，由其他AI继续。先读 [R73c停工交接](AI-HANDOFF-2026-10-19-R73-STOP.md)：双端新包、首幕3张自然整绘CG、110项Web检查与模拟器四页通过；新增12句语音仅试听；其余20场景与新版结算仍待继续。线上仍为v0.3.81/R72。本AI不再自动继续开发或消费。

---

# 已发布状态：v0.3.81 / R72

本轮执行与真实验收见 [BATTLE-UPGRADE-2026-10-19.md](BATTLE-UPGRADE-2026-10-19.md)。81个PSD条目；556 Edit / 70 Play / 110 Web通过；模拟器三页实际PNG/结构通过，非实体触摸。四角色大招视频未到；03仅候选，原首页两首保留。不使用Devin，不覆盖其他AI脏改动，不再反复要求用户确认常规已授权操作。

下方是v0.3.80/R71的历史交接，旧测试预期与图标常驻规则以本轮记录及人类最新指示为准。

---

# CHO-SIREN handoff · 2026-10-19 (v0.3.80 / R71)

Written for the next AI picking up this project. Read this file first, then `AGENTS.md`.

## 1. The user and how to work with them

- The owner (班海庄) is a beginner. Reply in **short, simple Chinese** and address them as **少爷**. You choose the version numbers (0.3.x).
- **Safety rule (verbatim):** 「你的操作尽量安全，不要让我的Pc发出安全警告」. On the user's machines you only read and generate. No installs, no system or settings changes, no deletes, never type passwords or credentials. Ask before downloading anything (give the file name, source and size).
- **Visual direction:**
  - UI follows 胜利女神 NIKKE.
  - Keep the user's original art. Change only what was asked.
  - When the user supplies a PSD, reproduce it 1:1 from its separate layers. The user stressed that the PSDs ship real layers.
- **Design changes:** for transitions and other big visual changes, publish a preview first (an Artifact page) and wait for approval before putting it in the game.
- **Every release ships both targets:** the web build (GitHub Pages) **and** the iOS simulator build.
- **Large Unity worktree:** `~/cho-siren-unity` is a git repo with ~1,170 uncommitted changes, many made by other AIs. Do not reset, clean or mass-commit. A tar backup from before today's work is at `~/cho-siren-backups/unity-before-psd-20261019.tgz`.

## 2. Where things are

| What | Path |
|---|---|
| Unity project (6000.6.0f1) | `~/cho-siren-unity` |
| Unity binary | `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity` |
| Web repo (GitHub Pages) | `~/cho-siren-web` → https://bojuejun.github.io/cho-siren-preview/ |
| Release scripts | `~/cho-siren-backups/release-tools/release.py`, `push.sh` |
| Source PSDs | `~/Downloads/未标题3453634576568-2.psd` (battle), `~/Downloads/CHO-SIREN-Home-Layered-v3(1)(1).psd` (home) |
| Transition reference video | `~/Downloads/fdd2ddc3673258f9098f5e0c79cc0587.mov` |
| Node for `npm` | `export PATH=$(ls -d ~/.workbuddy-ai/binaries/node/versions/*/bin \| tail -1):$PATH` |
| iOS simulator | iPhone 18 Pro, UDID `C75AD7D8-8C13-477C-A291-E3DE282D46A1` |

The Mac has no ffmpeg, PIL or brew. For video trims and frame grabs, use small Swift AVFoundation / CoreImage tools; this session compiled them with `swiftc`.

## 3. Build, test, release

1. **EditMode tests** (about 2 min):
   ```
   Unity -batchmode -projectPath . -buildTarget WebGL -runTests -testPlatform EditMode -testResults edit.xml -logFile edit.log
   ```
   - Always run `grep "error CS" edit.log`. A compile error still reports the previous pass count.
   - Expected result: **502/503**. The single failure, `StarterMemberAvatarTests` (xingli art size), is pre-existing.
2. **Bump the version** in `ProjectSettings/ProjectSettings.asset` (`bundleVersion`). Current: 0.3.80. Next: 0.3.81 / R72.
3. **WebGL build:**
   ```
   Unity -batchmode -projectPath . -buildTarget WebGL -quit -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWebGL
   ```
   Look for `CHO_SIREN_BUILD_OK` in the log. Output goes to `Builds/WebGL`.
4. **Stage the release:** `python3 ~/cho-siren-backups/release-tools/release.py spec.json`.
   - The spec has: version, releaseId, name, notice, unityBuild, entry, validation, knownLimits.
   - The R71 spec is reproduced in `~/cho-siren-web/release.json`.
5. **Check the web repo:** `cd ~/cho-siren-web && npm run check` must show **106 pass**.
   - `scripts/verify-webgl.mjs` pins the battle manifest to **72 entries**.
   - It also requires the new PSD keys and `StreamingAssets/Transitions/challenge-cut.mp4`.
   - Update the script whenever you add battle layers.
6. **Push:** `bash ~/cho-siren-backups/release-tools/push.sh "message" <version>`. It pushes `main` alone plus `backup/v<ver>`, then waits until Pages serves the new version.
7. **iOS simulator:**
   - Build with `ChoSirenBuild.BuildIOS` plus `-iosSimulator`. Then run `xcodebuild` on `Builds/iOS/Unity-iPhone.xcodeproj`, scheme `Unity-iPhone`, Release, `-sdk iphonesimulator`, `ARCHS=arm64`, `CODE_SIGNING_ALLOWED=NO`, `-derivedDataPath Builds/iOS-DerivedData`.
   - Install `Builds/iOS-DerivedData/Build/Products/Release-iphonesimulator/CHO-SIREN.app`.
   - If the Burst compiler fails once with "Method not found … ConvertToTag", run the build again. It is flaky.
8. **Tapping through the simulator** (402×874 points):
   - Title screen: (200,600).
   - Lobby 开始演出: (320,580).
   - Map 开始挑战: (322,660).
   - Record with `xcrun simctl io <udid> recordVideo --codec=h264 --force file.mp4 &`, stop with `kill -INT`.
   - Short effects only show up in recordings, not screenshots.

**WebGL template gotcha:** the web check runs `index.html` in a minimal fake DOM, so any new JavaScript in `Assets/WebGLTemplates/ChoSirenPortrait/index.html` must guard optional APIs. The neon cursor trail checks `appendChild` and `getContext` for this reason.

## 4. What shipped in v0.3.80 (R71) today

### Battle page: replicates the 2026-10-19 battle PSD

- **Layer export:** 28 new layers exported at half size to `Assets/StreamingAssets/BattlePsd928/<sha16>-<key>.png`. They are listed in both `Assets/Resources/BattlePsd928Manifest.json` and `StreamingAssets/BattlePsd928/manifest.json` (72 entries in total; x/y/width/height are PSD coordinates, 2946×6144).
- **New keys:**
  - Reroll: `reroll-smoke`, `reroll-bar`, `reroll-dice`, `reroll-arrows`.
  - Guard, ultimate, captain, enemies: `guard-badge`, `ult-pillar` (L013 cropped to the top 60%), `captain-plate`, `enemy-tag-l`, `enemy-tag-r`.
  - Bars: `bar-white` (desaturated L037, tinted live), `bar-energy`, `dice-bar` (L054).
  - Icons: `icon-*` (16 skill icons, L085–L100).
- **Code:** `Assets/Scripts/Panels/TacticsBattlePanel.Psd928.cs`.
  - Member cards span the PSD bounds: `PsdMemberTop = 3670`, height 1381. Frames and sleeves are placed at their manifest coordinates.
  - HP and energy bars sit on the PSD bar rectangles (`PsdHpBars` / `PsdEnergyBars`).
  - Skill column per card (`PsdSkillColumnX`, `PsdSkillRowY`):
    - Row 1 is the ultimate; it is bright when ready.
    - Row 2 is the small skill.
    - Row 3 is the race's dice passive; it is bright on the captain.
    - Race → icons mapping is in `RefreshPsdCell`.
  - The ultimate pillar pulses above a card when its ultimate is ready.
  - 队长 plate sits under the captain card.
  - Enemy name plates go to the top corners when one or two foes are on stage (`PositionPsdEnemyCell`).
  - Reroll control on the left (`RerollX/Y`). `BattleRerollSlide929` now dims instead of hiding.
  - 应急守护 badge on the right. Its live subtitle reads 「全队减伤50%」 (the real mechanic is a 3 s, 50 % team guard).
  - The dice bar's sample words are covered by live text.
  - 「怎么玩」 was removed from the stage; it is still in the pause menu.
- **Album disc:** idle and ready offsets in `UI/BattleMemberAlbum929.cs` match the new card top.

### Transitions

- `UI/ChallengeVideoTransition.cs` plays the user's reference clip, cut to seconds 0.92–2.3 and stored at `StreamingAssets/Transitions/challenge-cut.mp4`.
- Sequence: a diagonal slash opens the clip, the scene switches underneath, then the frozen last frame splits apart along the slash.
- The clip is warmed (prepared) while the map or lobby is shown.
- Used for 开始挑战 (`LevelMapPanel.StartChallenge`) and 开始演出 (`ChoSirenAppPsdHome.OpenPsdHomeAfterMotion`).
- Known limit: the clip shows the name **KUMI**, not the current captain.
- `UI/VinylStageTransition.cs` (rejected design) and `AlbumSceneTransition929` remain in the code but are unused for these two entries.
- The approved preview page is https://claude.ai/artifact/R2qmiecKSdwdVfiBKjKTJN.

### Hit feel (meeting item 1)

- **Pace:** `BattlePresentationOptions.Pace = .7`. It scales the simulation clock, `BattleAnimationDelta`, the boss presentation, attack trajectories and the ultimate cut-in.
- **Flash shader:** `Assets/Resources/Shaders/UIFlash.shader`, used by `UI/EnemyUnitPresentation.cs` for both enemies and member portraits.
  - The hit target flashes white.
  - The attacker tints red during wind-up.
  - The existing hit shake is kept.
- **Critical hits** shake the whole stage (`ShakeStage` in `TacticsBattlePanel.cs`).
- **Popups:** damage numbers land at 1.9× and settle to 1×; heals (text starting with `+`) grow from 0.45×.

### Sound

- `GameAudio.PlaySfx(name, volume)` loads `Resources/Audio/Sfx/<name>.wav`. It skips repeats of the same sound within 70 ms and respects `SfxEnabled`.
- **Hooks in `PresentEvent`:**

  | Sound | When |
  |---|---|
  | `hit` / `crit` | Hits on enemies |
  | `hurt` / `block` | Hits on the team |
  | `heal`, `shield`, `debuff` | Matching effects |
  | `ko` | An enemy is defeated |
  | `ultimate` | An ultimate is cast |
  | `dice` | Reroll |
  | `victory` | Battle won |
  | `cut`, `glitch`, `stamp` | Transition |

- The current WAVs were synthesized with a Python script. **The user called them bad.** See the todo list below.

### Home and other changes

- **Home record player:**
  - `HomeMusicDiscSpin` (in `Assets/Scripts/HomeMusicSwitcher.cs`) always spins, and notes rise from the disc while music plays.
  - `HomeMusicMarquee` shows the title in a slanted (8°) masked window and scrolls it when it is too long.
  - ◀ ▶ buttons call `GameAudio.PreviousHomeMusic` / `NextHomeMusic`.
- **Level map drop panel** simplified to icon + rate (`LevelMapPanel.cs`).
- **Mouse trail:** the WebGL template has a neon-shard mouse trail (option F from the user's picks).

## 5. Open work, in the agreed order

From the 2026-10-19 meeting with 赵金 (full notes: user memory `meeting-20261019-battle-todo`). The battle system is the core, so get rough effects in cheaply first and polish later.

1. **Real sound effects.** Recommended: Kenney CC0 packs (Impact Sounds, Casino Audio, Sci-fi Sounds, Interface Sounds from kenney.nl; no account needed). The user has **not yet approved the download**, so ask first. The alternative is the user generating them in ElevenLabs; write the prompts for them. Drop the files into `Resources/Audio/Sfx/` under the same names; no code change is needed.
2. **Two forms of normal attack:**
   - A fireball projectile from the member icon. `AttackTrajectoryPresentation` exists; restyle it.
   - The full portrait bursting out from behind the card. PSD layer L012 「攻击冒出的角色立绘」 at (1021,2104)-(3207,5831) shows the pose.
3. **Active-skill cut-in:** a Persona-style close-up, with UI and SFX.
   - `BattleAlbumUltimate929` (`psdUltimate`) already does a cut-in for ultimates. Extend it to active skills.
   - The PSD has per-race cut-in layers L101 (demon), L109 (charm), L113/L116 (mermaid) and L118 (blood elf), plus star sparkles. They are hidden in the PSD and not yet exported.
4. **Ongoing-effect icons on heads** (poison, heal over time). `StatusIcon-*` is now placed over the head (`RefreshPsdCell`). Restyle it with the PSD icons L079 (持续恢复) and L081–L083 (中毒) when doing effects.
5. **Ultimate zoom interaction:**
   - Tap the disc, then the disc grows into a zoom transition with a soul effect. Alternatively, drag the disc along an arrow.
   - During the zoom, a QTE bar: tapping in the window adds attack and shows Good/Perfect.
   - Camera zoom, screen burst and feathers.
   - Ultimate is currently cast via auto mode or by tapping the card (see `IsUltimateReady`).
6. **Result screen:** victory logo, 1–3 stars, MVP. The user's motion reference is a P4R 天城雪子 total-attack finisher (xiaohongshu link in the earlier chat). Current result flow: `ShowResult()` in `TacticsBattlePanel.cs`.
7. **Attack effects in the PSD style:** P5-inspired but not a copy. Layers L077 (slash "81"), L078 (gold lightning), L079 (heal), L080 (damage number style), L081–L083 (poison). The user asked for less clutter than now.
8. **Wave transition:** the crystal floor cracks and reforms (a 1–2 s video) instead of an instant respawn.
9. **Monster design:**
   - Roles (AOE, single target, healer, shield) and combos.
   - Counters, for example high attack with low HP, or high defence as support.
   - The user suggested an AI-made PPT for races and numbers.
10. **Battle BGM** with a strong rhythm.
11. **Ultimate videos for the four characters** will come from 赵金; integrate them when they arrive.

Older items still open:

- 「经纪人」 text overlaps the header decoration.
- The title-screen 16+ badge should sit at the top-left.
- Enemy skill-name labels float under the enemies.
- The pre-existing `StarterMemberAvatarTests` failure.

## 6. Scratch tools used today (recreate if needed)

The scratchpad is wiped between sessions. Recreate these:

- **`extract`** (Swift, `AVAssetImageGenerator`): `extract in.mov outDir stepSeconds` grabs frames.
- **`sheet`** (Swift, AppKit): builds a contact sheet, `sheet out.jpg cols cellWidth files…`.
- **`trim`** (Swift, `AVMutableComposition` + `AVAssetExportSession`): writes a video-only MP4 for a time range.
- **`prep`** (Swift, CoreImage): crop top fraction, optional desaturate, scale; writes RGBA PNG. Used for the PSD layer export. The battle PSD layers were first dumped to PNG with `layers.json` (bbox, visibility, blend).

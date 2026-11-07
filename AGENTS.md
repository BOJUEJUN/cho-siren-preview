# CHO-SIREN agent guide

Keep this file short. Load only the document that matches the work:

- **Start here:** `Docs/AI-HANDOFF.md` — the only current handoff (game 0.9.39 public / 0.9.38 private on both iPhones, AI 换装, enzoban.com site, secrets, release flow). Earlier handoff docs were deleted 2026-11-06; use git history if needed.
- Release records: `Docs/FULL-RELEASE-2026-11-07-0939.md` (latest), `Docs/BALANCE-1104.md` for level/balance rules
- WebGL build or publishing: `Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md`
- Battle UI or feedback: `Docs/BATTLE-UI-2026-09-05.md` and `Docs/BATTLE-FEEDBACK-20260907.md`

## Current product contract

- Versions are `0.9.x` and only move forward (0.9.33 was a web-only dress-lab page; 0.9.38 is installed privately on both iPhones, 0.9.39 is the current public release, so the next one is 0.9.40 or higher). Never fall back to `0.3.x`.
- Solid teal fill means "selected" only; main actions use the dark face with a teal frame and teal caption (2026-11-03).
- Bosses, enemies and chapter-one lineups follow the user's planning deck (`CHO-SIREN幻域魅声 游戏企划书`): 1-5 碎音·琉娜, 1-10 幻蚀歌枢·梅洛蒂 (`StageBoss1103.cs`); keep its enemy names and designs.
- Ultimate charge (2026-11-03 deck): captain 17s, then 20s / 25s / 30s in formation order (`ChargeOrder1101.cs`).
- 星钻 is the top-up currency (2026-11-04): free recurring rewards give 签约券 / 星光币; the shop grants nothing until a real payment channel exists (`GameModelTopUp1104.cs`).
- Levels (2026-11-04, `Docs/BALANCE-1104.md`): training cap starts at Lv12, +5 per fifth stage cleared in order; Lv17/27/37… need a 突破 paid in 声纹碎晶 / 声核 / gold. Tune stages to the recommended-level pass rates there (normal ~70%, elite/boss ~60%).
- Use the latest user supplied reference for visual acceptance and reproduce it 1:1 except for explicit user changes.
- Homepage characters keep the two separately authored homepage designs. The revised member PSD updates member portraits/avatars only; never replace or add homepage characters unless the user supplies new homepage art explicitly (2026-10-15).
- Preserve source aspect ratios and native RGBA. Reject checkerboards or solid backgrounds presented as transparency.
- Bottom navigation labels are Chinese only. Its five measured visual and hit regions are intentionally nonuniform.
- Every interactive homepage hotspot needs visible hover, press, release, and exit feedback in a real browser; buttons show the green ink edge on hover (not at rest, not only on click). Exceptions by the user (2026-11-03): the name plate and the music bar stay unhighlighted.

## Working method

- The user explicitly forbids Devin (2026-10-15) and is not renewing it. Do not invoke, check, repair, or manage Devin; this overrides older skill/delegation guidance.
- Only one AI should change this project at a time; check `git log` and the other repo (`/Users/bojuejun/cho-siren-web`, remote main) before releasing.
- Codex or Claude continue implementation and verification directly. Do not use DeepSeek Harness unless the user enables it again.
- Preserve unrelated local changes. Stage and commit only files belonging to the current task.
- You may run safe local reads, Unity status checks, focused tests, builds, screenshots, and browser inspection without asking.
- Use focused tests for the changed behavior. Broaden testing only when a failure or shared subsystem warrants it.

## Definition of done

A change is complete when its behavior is implemented, affected regression tests pass, and the result is inspected in its real runtime. A publishing task also requires a clean WebGL build, deployed browser verification, and a working public URL. Continue through fixes and rechecks until all applicable conditions pass.

## Release delivery flow (2026-11-02)

- 用户要求发布时一并更新现有网页、PC/macOS/Android 下载和增量更新清单。
- 同步替换本机 `/Applications/幻域魅声.app`，保留现有 Dock 入口并验证其指向最新版本。
- 安装到用户指定 iPhone，保留存档，查询已装版本；不要自动打开手机游戏。两台都可装（五条悟专用机 2026-11-06 起用户自测），以用户当次指定为准。
- 分别报告线上、桌面、手机完成情况；未完成的渠道不能宣称已上线。

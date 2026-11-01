# CHO-SIREN agent guide

Keep this file short. Load only the document that matches the work:

- Release continuity or handoff: `Docs/AI-HANDOFF-2026-09-08.md`
- WebGL build or publishing: `Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md`
- Battle UI or feedback: `Docs/BATTLE-UI-2026-09-05.md` and `Docs/BATTLE-FEEDBACK-20260907.md`

## Current product contract

- Keep the existing `0.3.x` naming; the current homepage work is `0.3.8`.
- Use the latest user supplied reference for visual acceptance and reproduce it 1:1 except for explicit user changes.
- Preserve source aspect ratios and native RGBA. Reject checkerboards or solid backgrounds presented as transparency.
- Bottom navigation labels are Chinese only. Its five measured visual and hit regions are intentionally nonuniform.
- Every interactive homepage hotspot needs visible hover, press, release, and exit feedback in a real browser.

## Working method

- Prefer parallel agents for independent implementation, tests, and review. Give each writer an exclusive file scope.
- Devin work must use `swe-2-max`. Do not use DeepSeek Harness unless the user enables it again.
- Preserve unrelated local changes. Stage and commit only files belonging to the current task.
- You may run safe local reads, Unity status checks, focused tests, builds, screenshots, and browser inspection without asking.
- Use focused tests for the changed behavior. Broaden testing only when a failure or shared subsystem warrants it.

## Definition of done

A change is complete when its behavior is implemented, affected regression tests pass, and the result is inspected in its real runtime. A publishing task also requires a clean WebGL build, deployed browser verification, and a working public URL. Continue through fixes and rechecks until all applicable conditions pass.

## Release delivery flow (2026-11-02)

- 用户要求发布时一并更新现有网页、PC/macOS/Android 下载和增量更新清单。
- 同步替换本机 `/Applications/幻域魅声.app`，保留现有 Dock 入口并验证其指向最新版本。
- 安装到用户指定 iPhone，保留存档，查询已装版本；不要自动打开手机游戏。当前五条悟暂缓，仅伯爵君。
- 分别报告线上、桌面、手机完成情况；未完成的渠道不能宣称已上线。

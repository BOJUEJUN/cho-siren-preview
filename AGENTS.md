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

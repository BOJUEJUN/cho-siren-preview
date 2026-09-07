# Battle feedback and recovery — 2026-09-07

## Scope and source baseline

Built from the current `ui/battle-layout-20260905` source, baseline `49ff5ed`, preserving the fixed-stage minute-long combat release and accepted UI. Previous Pages version: `d060af7`. Unrelated Live2D, local agent configuration and art experiments are not included.

## Shipped behavior

- Every actor emits its own action event. Independent idle, attack, hit and defeat presentation is bound to the correct unit ID. These are procedural 2D sprite animations, not skeletal animation or AI video.
- Small skills appear on their caster's portrait; major skills use a bounded side cut-in queue. Ordinary damage no longer overwrites the shared status line continuously.
- Dice flip through existing face sprites, stagger their landing and highlight the authoritative result. Held dice stay still. Pause freezes presentation, and the visual animation never rerolls the model or changes combat outcomes.
- The charge bar means reroll energy, not an unimplemented ultimate attack. Boss HP excludes guards; other enemy HP is anchored to each figure. Team HP is a summary of individual HP, not a shared damage pool.
- Fallen captains hand command to the first surviving teammate in formation order. This changes captain rules without resetting dice, charges, cooldowns or the saved formation.
- Chapter enemy compositions differ and use mostly female characters from the user-provided Brown Dust pack. New rejected AI female concepts are archived outside Assets, not built. See `FEMALE-ENEMIES-20260907.md` for source mappings and provenance.
- Defeat gives read-only, state-based advice and routes to training, equipment, formation or the chapter map. No upgrade, purchase or recruitment is performed automatically. Current recruitment fills role/captain needs; it is not guaranteed to produce a stronger unit. Equipment is the existing three shared loadouts, not a new inventory system.
- Chapter victories count toward daily/weekly performance tasks and the existing third-performance daily reward. Failure and repeated settlement cannot count or pay twice.

## Automated evidence

- EditMode: `TestResults/editmode-feedback-final2-20260907.xml`, 304/304 passed.
- PlayMode: `TestResults/playmode-feedback-final-verified-20260907.xml`, 76/76 passed after the browser-discovered clipping correction.
- WebGL build: `Builds/WebGL-Feedback-20260907`; initial build `Logs/build-feedback-20260907.log` succeeded; final rebuild tracked in `Logs/build-feedback-final-20260907.log`.
- No real user save was cleared. Browser acceptance uses an isolated temporary profile.

## Isolated browser acceptance

Initial candidate: `Artifacts/qa-minute-1788772438186/` (screenshots and recorded WebM).

- Fresh 1-1: three waves, 67.6 seconds, three stars, zero casualties; 120→112 stamina and only 1-1 added to clear records.
- User-pack scout and wraith figures render fully; individual enemy HP follows its own figure. Dice roll/landing and caster side cut-in observed.
- Deliberately underleveled 1-10 fixture: Boss-only top HP, four separately tracked guards, successive captain replacement and eventual defeat at 57.4 seconds.
- Defeat → training routes to the lowest-level teammate. Explicit training click costs 50 gold, raises level 1→2, attack 40→42 and HP 280→303. No real browser save was changed.
- Browser `pageerror` list stayed empty.
- This pass caught clipped player names and a two-line dice caption crossing its charge bar. The release candidate was subsequently corrected to a fitting name label and one-line hand caption, with a regression test. Final visual/release evidence follows after rebuilding.

Final candidate: `Artifacts/qa-minute-1788773114056/fixed-battle.png` confirms all four player names, the single-line dice hand and its separated charge bar render correctly. Final WebGL build exited 0; Pages staging and `npm run check` passed, including all 12 loader tests. The immediately previous d060af7 asset set is retained; only the older grandparent's four hashed build files are removed by the validated staging helper and remain recoverable from Git.

# Equipment and battle interaction update

## Equipment and roster

- Seven category slots per member: ears, necklace, wrist, ring, hair, charm, stage footwear. Same-category replacements preserve other slots. One physical collectible still has one wearer; duplicate drops remain upgrade fragments.
- Existing MemberAccessories bindings are retained. Normalization now deduplicates by member plus category, not member alone. No save reset or currency reset.
- Equipment bonuses add across slots and enter the same PlayerStats calculation used by roster power and battle setup. Transfer preview subtracts the selected item from its former wearer. Removal affects only that item.
- Equipment page uses an owned-member portrait picker instead of previous/next arrows. Selecting a member does not equip an item or change formation.
- Worn slots and inventory are separate; selecting a slot filters inventory. Equipped item comparison shows its positive applied contribution, retaining bonuses from other slots in both columns.
- Team occupied portraits open training/equipment profiles, with an explicit change-member action. Empty slots still open selection directly.
- Member cards and pickers show captain, deployed, or standby labels sourced from the actual formation.

## Combat

- Enemy special attacks telegraph for two seconds. Manual interrupt cancels a pending cast, with a 12-second cooldown consumed only on success.
- Stun stops actions; bosses cap stun at 0.6 seconds and targets have recovery protection. Slow lengthens basic attack intervals by 35%; armor break reduces defense 30%; cleanse removes one negative condition; control ward prevents stun.
- Enemy identities select distinct disruption/heal patterns. Enemy heal tuned to 4% maximum HP to keep the chapter pacing audit within its existing bounds.
- Feedback palette: outgoing cyan, incoming red, critical orange, healing green, shield blue, control gold, poison/armor break purple. Incoming captions no longer overwrite the performer's action caption; poison ticks do not spawn a new firing trajectory.

## Verification

- EditMode: 361/361 passed (`TestResults/editmode-multislot-20260907.xml`). Includes multi-slot persistence, transfer/replacement/removal preview and battle stat agreement, tactical conditions, 10 stages across all captains and 16 seeds.
- PlayMode: 112/112 passed (`TestResults/playmode-multislot-final-20260907.xml`). Includes picker navigation, team profile flow, deployment badges, short/standard layout containment and text overlap checks.
- Previous production: Pages `cffde5a`, source `a0f8911`. Retain as rollback; do not remove user saves.
- WebGL build succeeded: `Builds/WebGL-multislot-tactics-20260907`. Hashed staging dry-run passed, retaining all four assets from `cffde5a` as previous version.
- Isolated real Chrome, 480×1024: `Artifacts/qa-minute-1788790210376/`. `two-equipped.png` shows two simultaneous slots, additive stats and two persisted bindings. `hero-picker.png` / `selected-wubai.png` verify direct selection. `team-trained.png` and `deployment-badges.png` verify upgrade from team portrait and updated roster labels. `battle-interrupt.png` shows a successful interrupt, 1.1s stun and 11.9s cooldown. Browser reported no page errors.
- Source implementation commit: `124d53a`. GitHub push was denied by permission review, asking for explicit authorization to send source/documentation to this repository and branch. No workaround or alternate push was attempted; Pages remains `cffde5a`. Remote branch readback before push was `a0f8911`.

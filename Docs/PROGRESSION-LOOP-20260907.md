# Progression loop, 2026-09-07

## Player flow

- Interview signing persists ownership, then opens the signed member's profile. Owned members sort before unowned members without changing stable catalog IDs.
- Team positions open an owned-member picker. Full teams replace in one action; choosing another deployed member swaps positions. Position zero is captain. One to four members and repeated careers are valid; automatic formation prioritizes career diversity then fills vacancies.
- Member profile links to that member's accessory page. Each member has one accessory slot; each unique owned item has at most one wearer. The transfer button names its current wearer. Unequip, transfer, training and upgrades persist and affect the next battle snapshot only.
- The accessory screen has a character selector, real before/after stats, explicit source and equip/upgrade actions, six inventory cards, and a masked vertical scroll area for short screens. Supplied character and accessory art is retained; overlapping decorative panel art is not used.

## Drops and first-clear distinction

- Stages 1-1 through 1-4 drop microphone charms; 1-5 through 1-9 drop star bracelets; 1-10 drops crowns. Each first clear guarantees its stage's item, in addition to first-clear diamonds and normal rewards.
- Repeat victories give base gold and weighted random drops, not another first-clear payment. Preview percentages are computed from the actual drop table and represent the probability of at least one item across all rolls (1-1: 19%). Defeat gives no victory rewards; repeat settlement cannot pay again.
- A new item is added to accessory inventory. Each duplicate becomes three upgrade fragments. Upgrade levels 1–3 cost 3/6/9 fragments and 100/200/300 gold. Each level adds two percentage points to the item's existing nonzero stat bonuses.
- Result panels show named equipment/fragment gains and an equipment shortcut. Stage selection offers a detailed first-clear/repeat drop preview.

## Currency

- Diamonds: recruitment and optional gold/stamina exchanges. Gold: interview contracts, member training and accessory upgrades. Stamina: entry cost, with existing natural regeneration.
- Top-bar resource groups open their use/source panel. Gold exchange: 100 diamonds for 2,000 gold. Stamina: at most 60 points, one diamond per actual point, never beyond the cap. Both require a second confirmation; stale stamina quotes are rejected without payment.
- No real-money payment service is connected. The diamond panel explicitly says so; no fake purchase success, payment collection, or crediting is implemented.

## Save compatibility

- Schema 5 retains stable member IDs, currencies, levels, clears and collection data. Old global accessories migrate once to the existing captain; later captain changes do not move equipment.
- Older equipment IDs incorrectly stored as cosmetics are recovered into the inventory without deleting original data. Old cleared stages receive a one-time guaranteed-equipment compensation tracked by individual stage claim markers, never another diamond payment.
- No automatic save reset. Tests use isolated fixtures; browser QA must not touch the user's save.

## Validation

- EditMode: 340/340 passed in `TestResults/editmode-loot-loop-20260907.xml`.
- PlayMode: 88/88 passed in `TestResults/playmode-loot-loop-final-20260907.xml`, including short-screen containment, current wearer artwork, signing navigation, transfer/reload, and atomic exchange confirmation.
- WebGL build succeeded: `Builds/WebGL-LootLoop-20260907`, log `Logs/build-loot-loop-20260907.log`. Binary source `d1299d3`; Pages `be7675d` (fast-forward from `b7607fc`). `npm run check`: bundle/hash checks and 12/12 loader tests passed.
- Local Chrome acceptance, isolated test save: `Artifacts/qa-minute-1788776771928/`. Equipped heart necklace to Xingli, switched to Feiyin and transferred it; only Feiyin retained its binding and bonus. Cancelled gold exchange without spending; confirmed exchange changed diamonds 10,695→10,595 and gold 17,267→19,267. Signed Qingge for 700 gold, opened her owned profile, replaced slot two in a full team; team `[1,44,2,3]` persisted.
- That team cleared 1-1 in 74.9 seconds, three stars, no deaths. Stamina 120→112, first-clear diamonds +20, gold +416, microphone charm appeared as a named new item. The result shortcut opened the owned charm in the equipment screen. Refill charged exactly 8 diamonds for 8 stamina. Reload retained ownership, bindings, team, currencies and clear. No browser page errors throughout.
- Live asset probes: all four new assets HTTP 200. Data 78,578,649 bytes, framework 78,094, WASM 9,036,627 match local. Loader is gzip-transferred (14,793 bytes); decoded 41,128 bytes and SHA256 match local. GitHub warned the data exceeds its 50 MB recommendation but accepted the 74.94 MiB file (below its hard file limit).
- Live Chrome acceptance, separate fresh test save: `Artifacts/qa-minute-1788777345141/`. New personal equipment, resource modal and stage drop details verified after load. The default level-one party cleared 1-1 in 68.0 seconds, three stars, zero deaths; stamina 120→112, gold +300, diamonds +20, shards +4, owned accessories `[0,1,2]→[0,1,2,3]`. The settlement shortcut opened the new charm in personal equipment. Browser page errors: none. `live-victory.png`, `live-owned-drop.png` and the session WebM retain visual evidence.
- The previous `b7607fc` bundle remains in `build-versions.json.previous`. Four older `d060af7` build files were pruned by the guarded staging script and remain recoverable in Git. User save was not touched.

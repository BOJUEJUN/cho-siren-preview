# CHO-SIREN 0.3.8 page release verification

- Eight pages implemented against the latest supplied references with real save data, member selection, equipment, recruitment, stage locks and battle state.
- Character stage attributes use the recruitment model: 声波、说唱、舞蹈、名气、颜值. Signing channel remains consistent after leveling and save reload.
- Battle removes the manual-actor command, tactical interrupt and bottom combined-health hints. Emergency guard remains. The existing once-per-battle mermaid reroll entitlement and formation restrictions remain covered.
- 92 targeted PlayMode regressions passed. Actual final WebGL navigated all eight pages with zero page errors and zero failed requests. Hover, pressed and exit states were visually inspected.
- The nine authored PNGs are streamed byte-for-byte; content hashes validate the copies. The main data archive is approximately 84.6 MiB. An intentional HTTP 503 on the lobby artwork showed an actionable retry and recovered to the lobby with all nine images loaded.
- The loader's 12 cache/error regressions and Pages current/previous asset verification passed.

Evidence: `Artifacts/qa-20260915-full-ui/`. Public verification is recorded separately after deployment.

## Remaining visual differences

This is a playable page overhaul, not a claim of pixel-identical reconstruction. Non-lobby navigation art, the original member portrait resolution, several live numerical/name fonts, and the battle dice silhouettes still differ from the supplied mockups. Dynamic members and values intentionally follow the save rather than copying the mockup. These differences remain visible in the image progress board for further refinement. No iPhone Safari device verification is claimed.

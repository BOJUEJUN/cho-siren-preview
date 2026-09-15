# Runtime reference art for 0.3.8

Generated from the supplied 2026-09-15 reference images. These are deliberately opaque full-page backgrounds/backplates, not transparent sprites and not completed pages. UI must still draw live values, real member art, and working controls in the blank slots. Do not present these files as implementation screenshots or replace live data with mock reference values.

| Resource path | Use |
| --- | --- |
| Art/Reference038/team-stage-038 | Clean crystal orbit environment without UI or members |
| Art/Reference038/battle-stage-038 | Clean battle platform environment without UI or combatants |
| Art/Reference038/team-board-038 | Team environment plus empty UI frames, static Chinese title/actions, live member slots |
| Art/Reference038/members-board-038 | Member-gallery frame art with five columns and three rows; actual cards/search/filter values drawn on top |
| Art/Reference038/audition-board-038 | Empty candidate dossier, mode tabs, contract action art, right-side live candidate space |

| Art/Reference038/map-board-038 | City map with ten empty route plaques and static challenge/reward frames |
| Art/Reference038/profile-board-038 | Empty full-page dossier with static skill, stage-attribute, training headings and actions |
| Art/Reference038/accessory-board-038 | Empty equipment comparison and twelve collection slots; real equipment and numbers overlaid |
| Art/Reference038/unknown-member-038 | Opaque dark scanline portrait for unidentified members; no real identity revealed |
| Art/Reference038/lobby-board-038 | Latest homepage reference with live HUD value slots and Chinese-only navigation |

`Assets/Editor/ReferenceArtImport.cs` imports these assets as full-rectangle, non-mipmapped sprites with source dimensions preserved. UI transforms must retain the source aspect ratio; an opaque backplate does not need an alpha channel. No background-removal process was used.

WebGL publishes the nine used PNGs byte-for-byte in `StreamingAssets/Reference038/` with content-hashed filenames. `ReferenceArtWebBuild` temporarily excludes their expanded texture imports from the main data archive and restores all source assets in `finally`. The runtime preloads them three at a time as sRGB color textures before opening the lobby; download errors provide a retry action. No RGB16 quantization, resizing, or alpha extraction is applied. The unused clean team-stage source remains in the project without being downloaded.

Visual acceptance still requires current Unity/WebGL screenshots against the original reference and all explicit user corrections. Generated details may deviate and must be assessed rather than assumed exact.

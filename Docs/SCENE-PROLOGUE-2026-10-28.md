# 序章场景修复

- 用户要求以角色为参考生成完整场景，禁止把中景立绘贴到背景上。
- 使用内置 image_gen，参考 `Assets/Resources/Art/Members/member-xingli.png`。
- 新图：`Assets/Resources/Art/Story/Scenes1019/cg-prologue-l24.png`，原生 859×1831，未插值放大。原始输出保留在 Codex generated_images。
- 根据对白「窗外的霓虹猛地闪了一下。耳返里，传来刺耳的杂音。」绘制排练室窗边听见杂音的星璃。修正该段错误切到后巷背景；后三句沿用新完整场景，不再叠加悬浮立绘。
- 当前仅修复用户截图所示序章片段，未声称全剧情素材已全部重制。
- PlayMode 已验证 24–26 三句均使用同一 CG、独立角色层关闭。真实 Unity 渲染预览：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/story-layout-20261019/runtime/prologue-alarm-integrated-1028.png`。

## 生成提示词

Use case: illustration-story.
Create one finished, naturally integrated story CG for the portrait mobile game CHO-SIREN. This is a complete new painting of character AND environment together, not a pasted cutout on a background.
Input image 1 is the adult character Xingli identity/costume reference only. Preserve her recognizable blue eyes, long golden hair, fox ears, blue hair clips, headset mic, white sleeveless stage top, black shorts, necklace and iridescent blue/purple fox tails. Change her pose and expression to fit the scene.
Narrative moment: late at night in the idol group's rehearsal studio, the neon outside the window abruptly flickers, and sharp interference sounds in her in-ear monitor. Xingli has just stopped rehearsal, turns toward the rain-streaked window, and touches her earpiece with one hand, alert and slightly concerned. Natural candid three-quarter body pose, weight grounded, no posing for a catalog.
Composition: tall portrait approximately 9:19.2, full bleed. Eye-level cinematic medium-long shot, character in the midground occupying about half the image height, face near the upper-middle; the studio and night city are an essential part of the composition. Rehearsal studio wooden floor, mirror and a small speaker, tall windows overlooking a wet violet/blue neon alley. Modest empty floor in bottom 18% so the game's separate dialogue UI can overlay it. No UI, text, logos, borders, extra portraits, inset panels, name labels, title, or watermark.
Art: polished Japanese anime game narrative CG, clean elegant drawn linework and painterly environmental detail, sharp native detail, cohesive purple/cyan/pink night lighting, readable face without being brighter than the entire environment. Match character perspective, room lighting, ambient occlusion, plausible window reflections and contact shadows. Pink exterior rim light and cool window light should illuminate both clothing and surrounding objects consistently. No all-around neon outline, no sticker edge, no white silhouette, no floating character, no copy/pasted neutral standing pose. Single coherent authored illustration.

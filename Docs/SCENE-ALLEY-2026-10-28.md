# 首战后巷完整场景 CG

## 用途与资产

- 用户要求角色与场景一起生成，避免将原立绘硬贴到背景上。本图用于 `stage-1-1` 首战前与 `stage-1-1-end` 战后对白。
- 使用内置 `image_gen`，参考图重绘模式；参考 `Assets/Resources/Art/Story/Scenes1019/cg-stage-1-1-l3.jpg` 中两名角色的身份与服装，重做自然行动和场景。不是透明贴片，`transparent_background=false`。
- 项目输出：`Assets/Resources/Art/Story/Scenes1019/cg-stage-1-1-scene-v2.png`。
- 工具原始输出：`/Users/bojuejun/.codex/generated_images/01a11f6e-6a02-7fb2-b043-2a611929a904/exec-2da5a859-e1fa-4eab-b9cf-eec09e46e475.png`。
- 原始PNG尺寸 **941×1672**，未插值为4K。工具输出SHA-256：`9d56b5c388ff28d0792987e1455675f0c703787872378ea950c47f4a133aa0ca`。
- 构图：成年星璃与绯音走在雨后霓虹后巷，关注异常闪烁的招牌；统一地面、透视、照明、接触阴影和倒影，底部留出对白覆盖空间，不绘制UI。
- 路由由 `StorySceneDirector1028` 负责；两段脚本持续使用完整CG，UI独立对话头像仍属于对白界面，不作为场景内悬浮角色层。

## 实际生成提示词

以下提示词来自本轮主线程的生成记录，未事后改写：

```text
Use case: illustration-story. Produce one complete, naturally staged anime game story CG, portrait 9:16 composition, high resolution clean detailed linework. The reference identifies two ADULT fictional music idols: golden blonde fox-eared Xingli with blue eyes, white and black stage outfit and cyan/lavender spirit tails; pink-haired horned Feiyin with tiny gold crown, pink jacket, black stage outfit and gold microphone. Preserve their identity, facial features, colors and costume design. New composition and poses: both adults walk together through a rain-wet neon city alley at night, inspecting the flickering pink sign outside their rehearsal building before their first battle. Eye-level medium-wide composition, knees or full bodies naturally in the street, one character pointing at the flickering sign, the other listening on her earpiece. Clear scene geometry, both figures grounded on the same street with convincing contact shadows and reflections. Unified blue ambient light and warm sign illumination falling on faces and clothes. Continuous single painted image, not collage, no cutout silhouettes, no outline glow, no detached busts, no floating people, no horizontal seam or pasted image rectangle, no table or road hiding their bodies at chest height. Calm clean shapes, minimal particles or grain, no text, no interface, no border. Reserve bottom fifth for a dialogue overlay without making it a separate blank panel.
```

## 验证状态

- EditMode路由与资源存在检查已通过，报告：`Builds/qa-logs/completion-099/editmode.xml`。
- 发布资产字节一致性、游戏运行截图与公开版本验证由主集成人在 `RELEASE-2026-10-28-0.9.9.md` 补齐；本素材记录不单独代表上线或最终视觉验收。

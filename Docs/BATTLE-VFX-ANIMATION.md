# 战斗角色动画与特效规范

## 当前版本的选择

当前 Boss 原画是单张透明 PNG，而不是分层 PSD。为了不破坏角色脸部、服装和王座细节，当前版本采用：

- 原画整体的低幅待机呼吸、漂浮和轻微倾斜；
- 真实战斗事件触发的受击后仰、震动、闪白、品红残影、斩击和心形碎裂；
- 技能释放前的舞台能量环和聚光脉冲；
- 低血量时的心电警告框与灯光节奏变化；
- 胜利/失败时的定格、淡出和舞台收光；
- 所有效果只依赖 Unity 原生 UGUI、协程和材质，Windows 与 WebGL 共用同一套行为。

这不是最终骨骼方案，而是对现有单层资产最稳妥、失真最小的高质量实现。

## AI 视觉素材

战斗视觉统一使用 `Assets/Resources/Art/BattleAI`：

- `battle-stage-hud-v1.png`：主舞台与 HUD 构图；
- `dice-frame-v1.png`：骰子槽；
- `member-skill-frame-v1.png`：成员战斗卡；
- `reroll-ring-v1.png`：重投入口；
- `skill-button-frame-v1.png`：技能按钮；
- `battle-hit-slash-ai-v1.png`：受击斩击；
- `battle-heart-impact-ai-v1.png`：受击爆裂；
- `battle-charge-aura-ai-v1.png`：技能蓄力；
- `battle-low-health-frame-ai-v1.png`：低血量警告。

后四张由 2x2 AI 图集生成，并通过 `Tools/Process-AIBattleVfxAtlas.ps1` 在本地转成透明 PNG。原始图集保存在 `Docs/DesignReferences/AI/battle-vfx-atlas-generated-v1.png`，不会成为运行时依赖，但新电脑可以用它重新生成素材。

## 为什么暂不接入第三方骨骼运行时

调研过 Unity 2D Animation、Inochi2D、Live2D 类运行时、image2live2d、LeanTween/PrimeTween 和 UGUI 粒子方案。骨骼与网格变形工具都更适合眼睛、嘴、头发、身体等已分层素材；直接自动扭曲一张完整 Boss 图，很容易让面部、手指、麦克风和王座产生明显拉扯。

当前动画规模无需额外 Tween 依赖，原生实现能减少 WebGL 构建和新电脑恢复时的风险。若以后提供分层 PSD，优先升级到 Unity 官方 2D Animation 的 Sprite Skin 工作流，并保留当前命中、蓄力和低血量特效作为上层反馈。

调研链接（供后续分层升级时继续评估）：

- Unity 2D Animation / Sprite Skin：<https://docs.unity3d.com/Packages/com.unity.2d.animation@10.0/manual/SpriteSkin.html>
- image2live2d（Apache-2.0，分层图转可驱动模型）：<https://github.com/Wzhang3912/image2live2d>
- Inochi2D（BSD-2-Clause，开放 2D Puppet 标准）：<https://github.com/Inochi2D/inochi2d>
- Particle Effect For UGUI（MIT，若未来需要复杂粒子排序）：<https://github.com/mob-sakai/ParticleEffectForUGUI>
- LeanTween（轻量 Unity Tween，当前版本未引入）：<https://github.com/dentedpixel/LeanTween>

## 后续分层源稿建议

建议至少提供这些独立图层：

1. 头部、前发、后发、左右眼、嘴；
2. 胸腔/上身、左右上臂、前臂和手；
3. 大腿、小腿、鞋；
4. 披风/羽毛装饰；
5. 王座前景、王座主体、人物后方装饰；
6. 麦克风与手分离。

有上述图层后可增加眨眼、口型、头部视差、头发物理、手持麦克风动作和更明显的技能演唱动画。

# Boss 动画技术决策（Windows / WebGL）

## 当前版本采用的方案

当前 Boss 是一张已经合成完成的透明 PNG，并没有头发、手臂、躯干、王座等独立图层。
因此本版本使用 Unity 原生 UGUI + Coroutine 做“2.5D 演出层”，不破坏用户提供的原画：

- 待机：呼吸缩放、上下漂浮、微倾、舞台光与地面阴影脉冲；
- 敌方出手：蓄力收束、AI 生成的音律光环、状态字幕；
- 受击：后仰、衰减抖动、品红残影、AI 斩光、碎心冲击、冲击环、伤害数字；
- 低血：30% 阈值触发心电警告框、红色呼吸光和“终曲暴走”；
- 阶段切换：二阶段/三阶段独立爆发演出；
- 战斗结束：胜利时 Boss 退场淡出，失败时 Boss 进入安可定格。

这些动画全部响应 `BattleEvent`，不是按固定时间播放的装饰视频；暂停和 1/2 倍速也会同步。
实现不依赖原生 DLL、外部进程或网络，因此 Windows 与 WebGL 共用一套逻辑。

## 调研过的开源方案

### Unity 2D Animation（官方）

- 官方示例：https://github.com/Unity-Technologies/2d-animation-samples
- 优点：Sprite Skin、骨骼和网格变形都在 Unity 编辑器内完成，运行时稳定。
- 限制：需要分层 PSD/PSB 或重新切割并补画被遮挡部位；无法从当前合成 PNG 自动得到可靠骨骼动画。
- 结论：拿到分层角色源文件后，这是下一阶段最稳的角色动画升级路线。

### Rive Unity Runtime（MIT）

- 项目：https://github.com/rive-app/rive-unity
- 示例：https://github.com/rive-app/rive-unity-examples
- 优点：状态机、交互动画、Unity 6 和 WebGL；非常适合按钮、血条、章节节点等矢量 UI。
- 限制：需要在 Rive 编辑器中制作 `.riv` 源资产，不能直接让当前 PNG 获得高质量骨骼；Unity runtime 仍快速演进，WebGL/LTO 组合曾有公开兼容问题。
- 结论：未来优先用于可交互 UI 动效，不在本预览版替换 Boss 渲染链。

### LeanTween

- 项目：https://github.com/dentedpixel/LeanTween
- 优点：轻量、成熟，适合常规位移/缩放/淡入淡出。
- 限制：它只简化补间 API，并不能解决角色图层、骨骼或受击特效素材问题。
- 结论：当前原生 Coroutine 已能完整覆盖需求，不额外增加依赖。

### LivePortrait

- 项目：https://github.com/KlingAIResearch/LivePortrait
- 优点：适合离线把人像表情或头部动作生成视频。
- 限制：PyTorch/模型权重/推理环境不适合随 Unity WebGL 发布；全身二次元王座构图也不是它的主要目标。
- 结论：可用于以后离线制作剧情特写或宣传视频，不用于实时战斗。

## 后续高质量升级路径

1. 美术交付分层 PSD/PSB（身体、头发前后层、双臂、麦克风、披风、王座、光效）。
2. 用 Unity 2D Animation 做网格与骨骼，保留本版本的事件接口：待机、受击、蓄力、低血、胜/负。
3. 原生演出层继续负责镜头抖动、伤害数字、斩光、冲击波和 HUD，使换骨骼资产时无需重写战斗逻辑。
4. 若要更强的终结技，可离线生成 1–2 秒短演出并作为可选层；核心反馈仍保留实时版本，避免 WebGL 视频解码或透明通道成为阻塞点。

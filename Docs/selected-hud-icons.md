# 已选顶部 HUD 图标资源

本目录记录玩家已选定的顶部 HUD 图标套装。资源位于：

`Assets/Resources/Art/UI/HudIcons/`

## 资源清单

- `Mail.svg` / `Mail.png`：邮件
- `Music.svg` / `Music.png`：音乐
- `Settings.svg` / `Settings.png`：设置

三枚图标统一使用 64×64 的矢量坐标系，造型为白色立体线条，左下带青色微光、右上带粉色微光，背景完全透明。PNG 运行时版本为 256×256 RGBA，可以避免不同 Unity/平台上的 SVG 导入差异；SVG 保留为无损源文件。

## Unity 使用建议

- 默认使用同名 PNG 作为 UGUI `Image` 的 Sprite。
- 建议视觉显示尺寸：28–32 px。
- 建议每枚按钮的点击热区：40×48 px；透明按钮容器可以大于图标本身。
- `Image.preserveAspect` 应为 `true`。
- 图标需要放在顶部 HUD Canvas 的可交互层，按钮容器负责接收射线；图标子对象可以关闭 `Raycast Target`。
- 不要在图标后添加不透明方底，也不要对 PNG 再叠加棋盘格或黑色背景。

## 生成与验证

PNG 可通过以下脚本重新生成：

`Tools/GenerateHudIcons.py`

验收要求：三枚 SVG 可被 XML 解析；三枚 PNG 为 256×256 RGBA；四角 Alpha 为 0；图形非空并保留透明边距。

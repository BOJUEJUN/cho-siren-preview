# 55 名成员运行时立绘管线

## 用途

`Tools/Prepare-MemberRuntimeArt.ps1` 从 `Docs/hero-asset-inventory-50.md` 的 55 张权威候选中生成 Unity 运行时副本。原始素材始终只读，不会删除或覆盖。

每名成员生成两张保留透明通道的 PNG：

- `Assets/Resources/Art/Members/hero-<角色组>/thumb.png`：256×352
- `Assets/Resources/Art/Members/hero-<角色组>/portrait.png`：512×704

运行时路径分别是 `Art/Members/hero-<角色组>/thumb` 与 `Art/Members/hero-<角色组>/portrait`，兼容 `Resources.Load` 和 `MemberCatalog.IsMemberResourcePath`。

## 执行

先跑普通、宽构图、超大原图三个样本：

```powershell
& .\Tools\Prepare-MemberRuntimeArt.ps1 -Sample
```

批量处理全部 55 名成员：

```powershell
& .\Tools\Prepare-MemberRuntimeArt.ps1
```

处理指定角色组：

```powershell
& .\Tools\Prepare-MemberRuntimeArt.ps1 -HeroIds 0002,0675
```

## 处理规则

- 只移除完全透明的外围画布，不裁掉任何可见像素。
- 等比缩放并完整放入目标透明画布。
- 四边预留约 5% 透明安全边，人物可见内容底部对齐。
- 使用 Lanczos 高质量缩放和 PNG 无损压缩。
- 每次运行验证 RGBA、目标尺寸、透明像素、安全边和文件哈希。
- 检测到重复源图哈希时立即失败，禁止复制同一立绘充数。

## 可追溯清单

- `Tools/member-runtime-art-sources.json`：55 张审计源图的稳定 ID、绝对源路径、风险和原始尺寸。
- `Docs/member-runtime-art-manifest.json`：源 SHA-256、输出 SHA-256、输出路径、Resources 路径、尺寸、Alpha 范围、可见边界、缩放和摆放参数。

当前批量结果为 55 个不同源 SHA-256、110 个不同运行时资源路径，PNG 合计约 17.66 MiB。

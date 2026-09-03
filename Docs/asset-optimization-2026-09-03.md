# 资源与构建层优化记录（2026-09-03）

静态修改，全程未启动 Unity Editor（执行时 `Get-Process Unity*` 未见 Editor；收尾时只看到 `Builds\Windows\UnityCrashHandler64.exe`，那是有人在运行已构建的 Windows 预览版，不是 Editor）。依据 `Docs/AI-COLLAB-HANDOFF.md` 第 10 节「项目体检结论」执行，未触碰 `Assets/Scripts/**`、`Assets/Tests/**`、`Assets/Resources/Data/**`、`Docs/AI-COLLAB-HANDOFF.md`，未删除任何文件。

每一项彼此独立，可单独回退。所有改动都会改变构建产物：**改完必须在 Unity 中重新导入 / 重新构建，并重跑 EditMode + PlayMode、重新截图后才能验收。**

## 总览

| # | 项目 | 状态 | 预期收益 | 需要 Unity 才能验证 |
|---|------|------|----------|---------------------|
| 1 | `bundleVersion` 0.1.0 → 0.3.0 | 完成 | `Application.version` 与发布包标签一致，打包脚本不再报警 | 重新构建后在游戏内可见 |
| 2 | 移除 5 个未使用的 package | 完成 | 玩家构建中不再带 `Unity.AI.MCP.Runtime.dll` 等预览期程序集；编译/域重载更快 | 打开项目时 Package Manager 会重新 resolve |
| 3 | 序列帧 Crunch（Standalone / WebGL） | 完成 | `resources.assets.resS` 138.6 MiB 预计降到约 1/3–1/4；显存占用不变 | 需重导入 238 帧 |
| 4 | 字体子集化 | 完成（未接线） | 15.7 MiB → 1.5 MiB（-90.5%） | 接线者改一行 `Resources.Load` 后重新构建 |
| 5 | 打包脚本去重 + 清单字段 | 完成 | 发布包不再包含重复的 `预览说明.txt`；`版本信息.json` 可追溯排除规则 | 下次运行 `Package-WindowsPreview.ps1` |

## 1. 版本号

- 文件：`ProjectSettings/ProjectSettings.asset`
- 改动：仅第 152 行 `bundleVersion: 0.1.0` → `bundleVersion: 0.3.0`，与 `Tools/Package-WindowsPreview.ps1` 默认 `-Version '0.3.0'` 一致。文件保持 LF、UTF-8 无 BOM。
- 注意：`Assets/Editor/ChoSirenProjectSetup.cs` 的 `ConfigurePlayer()` 里仍写着 `PlayerSettings.bundleVersion = "0.1.0"`；本次没有改它（该方法只在手动点菜单 `CHO-SIREN/Configure Player Settings` 时执行）。如果以后有人点了这个菜单，版本号会被打回 0.1.0——建议下一位改 Editor 脚本的人顺手同步成 `"0.3.0"`。
- 回退：把该行改回 `0.1.0`。

## 2. Packages

- 文件：`Packages/manifest.json`、`Packages/packages-lock.json`
- 移除的直接依赖：
  - `com.unity.ai.assistant` 2.19.0-pre.2（预览包，运行时 DLL 进入玩家构建）
  - `com.unity.ai.navigation` 2.0.14
  - `com.unity.timeline` 6.6.0
  - `com.unity.xr.legacyinputhelpers` 3.0.1
  - `com.unity.2d.tilemap` 1.0.0
- lock 文件同步删除上述 5 个条目，另外删除了只被 `ai.assistant` 引用的 `com.unity.nuget.newtonsoft-json` 3.2.2（depth 1，无其他引用方；Unity 重新 resolve 时也会自动丢弃它）。`com.unity.mathematics`、`com.unity.nuget.mono-cecil` 仍被 URP / collections 引用，保留。
- 所有 `com.unity.modules.*` 一律保留（URP 17 有隐式依赖）。
- 验证：`rg` 全 `Assets/` 未发现 `Timeline`、`Playables`、`AI.Navigation`、`NavMesh`（除 `Main.unity` 场景默认的 `NavMeshSettings` 节，属引擎内建，与 package 无关）、`UnityEngine.XR`、`LegacyInputHelpers`、`Tilemap` 的引用；无 asmdef 引用这些包。两份 JSON 均通过 `ConvertFrom-Json` 校验，保持 LF、无 BOM。
- 回退：把 5 行加回 `manifest.json` 即可，Unity 会自动重建 lock 文件。

## 3. 序列帧 Crunch

### 3a. Editor 脚本

- `Assets/Editor/HeroFrameImportProcessor.cs`
  - `importer.compressionQuality` 82 → 70，`importer.crunchedCompression` false → true
  - `ApplyPlatform()`（Standalone、WebGL）：`compressionQuality` 82 → 70，`crunchedCompression` false → true
  - `maxTextureSize` 仍为 1024，帧数不变。
- `Assets/Editor/ChoSirenProjectSetup.cs` `ConfigureArt()`
  - `isHeroFrame ? 512` → `isHeroFrame ? 1024`，加注释说明以 Postprocessor 为准，避免手动 `Configure Project` 与 Postprocessor 互相打架导致二次重导入。
  - 顺带把 Android 的 `maxTextureSize` 对序列帧固定为 512（`isHeroFrame ? 512 : importer.maxTextureSize`），与现有 `.meta` 里 Android 块的 512 保持一致；不然改成 1024 后一跑 `Configure Project` 就会让 Android 序列帧显存翻 4 倍。

### 3b. 238 个 `.meta`

- 新脚本：`Tools/Set-HeroFrameCrunch.ps1`（ASCII，无中文，不需要 BOM）
  - 只匹配 `platformSettings` 里 `buildTarget: Standalone` 与 `buildTarget: WebGL` 两个块（正则锚定到块内），把 `crunchedCompression: 0` → `1`、`compressionQuality: 82` → `70`。
  - `DefaultTexturePlatform` 与 `Android` 块、`maxTextureSize`、`spriteSheet` 全部不动。
  - 以 UTF-8 无 BOM 写回，保留 LF；遇到 CR 直接抛错拒绝改写。
  - 支持 `-WhatIf`（只统计）与 `-Revert`（改回 82 / 0）。
- 执行结果：`totalMetaFiles=238, changedFiles=238, platformBlocksTouched=476`；再次 `-WhatIf` 为 0，幂等。
- 逐文件校验：238/238 文件满足「Standalone+WebGL 块 `compressionQuality: 70`、`crunchedCompression: 1`、`maxTextureSize: 1024`；DefaultTexturePlatform+Android 块 `crunchedCompression` 仍为 0」。抽查 `hero_000`、`hero_120`、`hero_237` 一致。
- 顺带发现（未改）：`hero_090`–`hero_237` 的 Android 块与前 90 帧不同——`overridden: 0`、`maxTextureSize: 2048`、`compressionQuality: 50`，说明后 148 帧从未跑过 `ConfigureArt`。这不影响 Windows/WebGL；如果要做 Android 构建，跑一次 `CHO-SIREN/Configure Project` 即可统一。
- 预期收益：Crunch 只压缩磁盘/下载载荷，GPU 端仍解成 DXT5（Standalone）/ ETC2 或 DXT（WebGL），显存与绘制性能不变；`resources.assets.resS` 中约 138 MiB 的序列帧预计降到 35–50 MiB，WebGL `.data` 同比缩小。质量 70 在 720 px 上肉眼与 82 无差别；若验收时觉得有块状瑕疵，可把 Postprocessor 与脚本 `-CompressionQuality` 一起提到 80 再重导。
- 验证方式：Unity 打开项目后 Postprocessor 会随 `.meta` 变更触发 238 帧重导入（首次导入时间会明显变长，Crunch 编码较慢）；之后重新构建 Windows，比较 `CHO-SIREN_Data/resources.assets.resS` 大小，并跑 PlayMode 大厅测试 + 截图确认动画帧无异常。
- 回退：
  1. `& .\Tools\Set-HeroFrameCrunch.ps1 -Revert`（把 238 个 `.meta` 改回 82 / 0）
  2. 把 `HeroFrameImportProcessor.cs` 的 70/true 改回 82/false
  3. （可选）`ChoSirenProjectSetup.cs` 的 1024 改回 512

## 4. 字体子集化

- 源字体：`SourceAssets/Fonts/NotoSansSC-Regular.otf` = **16,437,364 字节**（15.68 MiB），cmap 44,810 字符。它仍随源码版本化，但已移出 `Assets/Resources`，不会进入玩家包。
- 运行时文件：`Assets/Resources/Fonts/NotoSansSC-Subset.otf` = **1,570,352 字节**（1.50 MiB），cmap 4,101 字符，体积为原来的 9.55%。
- 新 `.meta`：`NotoSansSC-Subset.otf.meta`，内容复制原 meta，仅 guid 换为随机 `9b7a3807d37abf20e920b3863d77c294`（已确认全项目唯一）。`fontNames` 仍是 `Noto Sans CJK SC`（子集保留了完整 name 表）。
- 新脚本：`Tools/build_font_subset.py`（可重复运行，`--dry-run` 只统计）
  - 字符集来源：
    - 扫描 `Assets/Scripts/**/*.cs`、`Assets/Resources/Data/**/*.json`、`Docs/*.md`、`%LOCALAPPDATA%\Temp\chosiren-stage\**\*.cs;*.json`，当前得到 1,168 个非 ASCII 字符（其中 72 个不在 GB2312 一级）；
    - GB2312 一级常用字 3,755 字（区位 B0A1–D7F9 枚举）；
    - ASCII 可打印、全角 ASCII（U+FF01–FF5E）、CJK 标点（U+3000–303F）、通用标点（U+2010–2027、2030–205E）、`♪♫◇♡☆★×▶◀●○◆■□▲▼→←↑↓…—–·•‰℃°±÷≈≠≤≥∞√` 等 UI 符号。
  - 合并后请求 4,139 字符；子集 cmap 为 4,101。另用 `GlyphTypeface` 对当前运行时代码和数据逐字核验，**项目实际用到的字符 100% 命中**，包括此前漏掉的“骰”。
  - pyftsubset 参数：`--layout-features='*' --glyph-names --name-IDs='*' --name-legacy --notdef-outline --recommended-glyphs --no-hinting`（CFF hint 已关，若接线后觉得小字号发虚可加 `--keep-hinting` 重生成，体积约 +10%）。
  - 运行环境：本机无系统 Python，使用 uv 管理的 `cpython-3.12.14`，在 `%TEMP%\chosiren-fonttools-venv` 建 venv 安装 `fonttools 4.64.0` + `brotli`；venv 在仓库外，不入库。
- **接线（已完成）：** `ChoSirenApp`、`PanelKit`、`PerformanceStagePanel`、`StoryBattlePanel` 与 `LevelMapPanel` 均优先加载：

  ```csharp
  Resources.Load<Font>("Fonts/NotoSansSC-Subset")
  ```

  完整字体已移到 `SourceAssets/Fonts/`，重新运行 `Tools/build_font_subset.py` 仍可确定性生成同一路径的运行时子集。
- 风险：未来新增文案若含 GB2312 一级以外的生僻字（如部分人名、繁体），子集会显示为方框。处理方式：重新运行 `build_font_subset.py`（它会自动扫描到新字符），或在 `EXTRA_SYMBOLS` 里补字。
- 验证方式：切换后跑 PlayMode + 截图，重点看成员名、技能描述、数字与全角标点。
- 回退：把完整字体和 meta 移回 `Assets/Resources/Fonts/`，再将加载路径改回 `Fonts/NotoSansSC-Regular`。

## 5. Windows 打包脚本

- 文件：`Tools/Package-WindowsPreview.ps1`（保持 UTF-8 **带 BOM**、LF；`Parser::ParseFile` 0 错误）
- 改动：
  - 新增 `$duplicateReadmeNames = @('预览说明.txt')`，`Builds\Windows\预览说明.txt` 若存在则不复制进暂存目录（内容已由 `WindowsPreview-README.txt` → `使用说明.txt` 覆盖）。
  - `版本信息.json` 新增 `gitIgnoredJunkExcluded: true`，以及 `excludedPatterns`（列出实际生效的排除规则，便于日后核对包内容）。
- 用当前 `Builds\Windows` 做过滤 dry-test：保留 `CHO-SIREN.exe / CHO-SIREN_Data / MonoBleedingEdge / D3D12 / UnityPlayer.dll / dstorage*.dll / UnityCrashHandler64.exe / 开始游戏.cmd`；排除 `CHO-SIREN_BackUpThisFolder_ButDontShipItWithYourGame` 与 `预览说明.txt`。**未实际运行打包**（会覆盖 `Releases\` 下同名产物）。
- 回退：删掉 `$duplicateReadmeNames` 数组及 `Where-Object` 里对应的 `-and -not (...)`，删掉清单里两个新字段。

## 尚未做、建议下一步

- `managedStrippingLevel` 提到 Medium（`ProjectSettings.asset`，需 Unity 验证 Mono 反射用法）。
- `ChoSirenProjectSetup.ConfigurePlayer()` 的 `bundleVersion = "0.1.0"` 同步为 0.3.0（见第 1 节）。
- 字体切换后把原字体移出 `Resources/`（见第 4 节）。
- `Releases\CHO-SIREN-Windows-Preview-0.3.0\` 解压态暂存目录约 250 MB 与 zip 重复，可在下次打包时手动清理（本次按约定不删任何文件）。

## 本次新增 / 修改文件清单

修改：
- `ProjectSettings/ProjectSettings.asset`（1 行）
- `Packages/manifest.json`、`Packages/packages-lock.json`
- `Assets/Editor/HeroFrameImportProcessor.cs`、`Assets/Editor/ChoSirenProjectSetup.cs`
- `Assets/Resources/Art/HeroFrames/hero_000.png.meta` … `hero_237.png.meta`（238 个）
- `Tools/Package-WindowsPreview.ps1`

新增：
- `Assets/Resources/Fonts/NotoSansSC-Subset.otf`、`NotoSansSC-Subset.otf.meta`
- `Tools/Set-HeroFrameCrunch.ps1`
- `Tools/build_font_subset.py`
- `Docs/asset-optimization-2026-09-03.md`（本文）

# CHO-SIREN 50+ 英雄立绘素材盘点

盘点日期：2026-09-03  
范围：只读检查本地素材包与 Unity 项目；未复制大文件，未修改 `GameModel`、`ChoSirenApp` 或其他 Unity 源码。

## 结论

- 已定位素材包：`D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】`。
- 原始审计记录共 1,911 张图片；其中 RGBA/带透明通道 1,456 张，已筛选 `KEEP` 760 张。
- `01.立绘` 的 `KEEP` 记录有 299 张；按命名规律 `char + 4 位角色组 + 2 位服装/变体` 归并后，有 **55 个角色组**、100 张可选变体。
- 本清单每个角色组只保留 1 张高分辨率 RGBA 主档，共 **55 张候选**，全部路径存在、实测含透明像素，合计约 **463.9 MiB**。
- 55 张候选的 SHA-256 无完全重复；3 张是既有近重复组中的高分辨率保留主档，没有选择 `review_remove` 文件。
- Unity 现有 `Assets/Resources/Art/Members` 只有 9 张成员图；`Assets/Resources/Art/HeroFrames/hero_000.png` 至 `hero_237.png` 是同一段序列帧，**不能当成 238 位英雄**。

> “55 个角色”是根据素材包编号规则归并得到的可执行候选集。正式命名、阵营和角色身份仍应由策划做一次视觉确认；不要直接把编号当作最终显示名。

## 风险标记

- `P`：常规候选，无额外自动风险标记。
- `H`：长边超过 8,192 px；原图直接进 Unity 会增加导入时间、显存和包体，建议先生成长边 2,048/4,096 px 的运行时副本。
- `W`：宽高比大于等于 0.9；用于竖向成员卡时不要使用 `cover`/中心硬裁，需单独校准锚点或留白。
- `ND`：近重复组中的保留主档；不要再导入其低分辨率对应文件。
- 透明边界抽样显示：49/55 的可见内容触及四边，另外 6 张触及两到三边。批量生成运行时图时建议先加 **4%–8% 透明留白**，避免武器、头饰、裙摆被成员卡裁掉。
- 全部素材来自第三方游戏素材包，**商业授权、再分发权与品牌使用权未知**；对外发布前必须做版权确认。部分服装表现还可能触发内容分级或渠道审核。

## 55 张可直接取用的源素材候选

所有行均为 PNG、RGBA，且实测透明像素范围包含 Alpha 0 与 255。这里列原始逻辑目录；其在 `全部图片_平铺` 中的同名审计副本与原图 SHA-256 一致。

| 角色组 | 文件 | 尺寸 | 风险 | 原始路径 |
|---:|---|---:|---|---|
| 0002 | `char000202.png` | 2844×3177 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char000202.png` |
| 0003 | `char000303.png` | 2472×4551 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char000303.png` |
| 0005 | `char000506.png` | 3114×2991 | W | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char000506.png` |
| 0007 | `Char000701.png` | 2562×3624 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\Char000701.png` |
| 0008 | `char000801.png` | 2577×3738 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char000801.png` |
| 0010 | `char001001.png` | 2976×4197 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char001001.png` |
| 0011 | `char001101.png` | 2625×3186 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char001101.png` |
| 0012 | `char001201.png` | 2199×3528 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char001201.png` |
| 0024 | `char002406.png` | 1887×3123 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char002406.png` |
| 0031 | `char003101.png` | 2375×5455 | ND | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003101.png` |
| 0032 | `char003203.png` | 5438×12530 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003203.png` |
| 0033 | `char003303.png` | 5383×11872 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003303.png` |
| 0034 | `char003402.png` | 7696×13111 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003402.png` |
| 0035 | `char003501.png` | 2244×4644 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003501.png` |
| 0037 | `char003702.png` | 4589×7134 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003702.png` |
| 0038 | `char003802.png` | 2370×6579 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char003802.png` |
| 0200 | `char020001.png` | 1572×2946 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020001.png` |
| 0201 | `char020101.png` | 1752×3399 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020101.png` |
| 0202 | `char020201.png` | 2328×3111 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020201.png` |
| 0203 | `char020301.png` | 2763×2937 | W | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020301.png` |
| 0205 | `char020501.png` | 1218×3240 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020501.png` |
| 0207 | `char020701.png` | 4640×9024 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020701.png` |
| 0208 | `char020801.png` | 5016×8352 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020801.png` |
| 0209 | `char020901.png` | 7544×9569 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char020901.png` |
| 0604 | `char060403.png` | 2004×3993 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char060403.png` |
| 0605 | `char060501.png` | 2088×3258 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char060501.png` |
| 0607 | `char060701.png` | 2391×3417 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char060701.png` |
| 0608 | `char060802.png` | 2115×3258 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char060802.png` |
| 0610 | `char061001.png` | 1986×3183 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char061001.png` |
| 0611 | `char061101.png` | 2523×4134 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char061101.png` |
| 0613 | `char061302.png` | 3054×3774 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char061302.png` |
| 0614 | `char061402.png` | 2703×3891 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char061402.png` |
| 0630 | `char063001.png` | 1395×3405 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char063001.png` |
| 0633 | `char063301.png` | 2331×3159 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char063301.png` |
| 0634 | `char063401.png` | 2034×3111 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char063401.png` |
| 0651 | `char065102.png` | 1893×3288 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char065102.png` |
| 0664 | `char066402.png` | 2553×3753 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char066402.png` |
| 0668 | `char066802.png` | 2229×2694 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char066802.png` |
| 0670 | `char067004.png` | 2798×6839 | ND | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067004.png` |
| 0671 | `char067101.png` | 5091×5436 | W | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067101.png` |
| 0672 | `char067202.png` | 7674×13933 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067202.png` |
| 0673 | `char067303.png` | 5503×13648 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067303.png` |
| 0674 | `char067403.png` | 4137×7074 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067403.png` |
| 0675 | `char067502.png` | 11993×12092 | H+W | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067502.png` |
| 0676 | `char067601.png` | 3088×6724 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char067601.png` |
| 1006 | `char100601.png` | 1878×3525 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char100601.png` |
| 1008 | `char100801.png` | 1170×2931 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char100801.png` |
| 1011 | `char101103.png` | 7531×11324 | H | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char101103.png` |
| 1013 | `char101301.png` | 1794×3249 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char101301.png` |
| 1014 | `char101401.png` | 2463×3330 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char101401.png` |
| 1016 | `char101601.png` | 1746×3450 | ND | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char101601.png` |
| 1034 | `char103401.png` | 1524×2931 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char103401.png` |
| 1035 | `char103501.png` | 1632×3495 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char103501.png` |
| 1036 | `char103601.png` | 1257×3330 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char103601.png` |
| 1037 | `char103701.png` | 1404×3363 | P | `D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】\01.立绘\char103701.png` |

## 近重复主档说明

- `char003101.png`：保留主档；不要再用较低分辨率的 `npc300302.png`。
- `char067004.png`：保留主档；不要再用 `char067004_260118223906_3dfe0f_100.png`。
- `char101601.png`：保留主档；不要再用 `char101601_260118230158_c3f3d6_100.png`。

## 建议的落地顺序

1. 用上表的 55 个“角色组”作为唯一键建立策划表，先补最终中文名、稀有度、定位与是否解锁，不要让文件名直接出现在 UI。
2. 原图作为母版保留在素材包；为 Unity 单独生成长边 2,048 px（大卡可 4,096 px）的透明 PNG，统一加 4%–8% 透明留白。不要把 463.9 MiB 原图整包直接复制进 `Assets`。
3. 运行时副本建议命名为 `hero-0002.png`、`hero-0003.png` 等，保持角色组和文件映射可追溯；`Char000701.png` 的大小写应在复制时统一。
4. 成员卡采用等比完整显示，再按角色配置焦点/锚点；宽构图 `0005`、`0203`、`0671`、`0675` 必须单独检查。
5. 导入前先完成版权与内容分级审核；通过后再把 55 条映射接入数据模型。

## 本次盘点依据

- `D:\AIGC\PPT\jcc-chroma-web\browndust2_dataset_audit.csv`
- `D:\AIGC\PPT\jcc-chroma-web\browndust2_dataset_audit.json`
- `D:\AIGC\PPT\jcc-chroma-web\browndust2_final_selection.csv`
- `D:\AIGC\PPT\jcc-chroma-web\browndust2_near_duplicates.csv`
- `D:\AIGC\PPT\jcc-chroma-web\browndust2_flatten_manifest.csv`

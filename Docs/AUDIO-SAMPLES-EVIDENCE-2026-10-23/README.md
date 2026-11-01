# 战斗音效素材来源（2026-10-23）

`Assets/Resources/Audio/Sfx/` 里的 hit / crit / strike-charm / strike-demon / strike-mermaid / strike-bloodelf /
ultimate / ko / hurt / block（各 3 个版本：`name.wav`、`name-2.wav`、`name-3.wav`，游戏随机轮流播）由
`Tools/sfx1023/build_sfx.py` 在本机叠层生成，素材：

| 来源 | 许可 | 用到的 |
|---|---|---|
| Sonniss GDC 2026 Game Audio Bundle（第 1 包），https://gdc.sonniss.com/ | 免版税、可商用、免署名；禁止用于 AI/ML 训练；不能单独转卖原始音效 | 拳击 Body Punch、Sword Slide Cuts、Wind/Metal Whoosh、Water Liquid Impact、Magic Christmas Bells、Bass Drops、Wooden Hit、Metal Bangs、Evil Spell Ambience、Metal Shield Spin |
| lentikula「Basic Spell Impacts [Free/CC0]」，https://lentikula.itch.io/freecc0-basic-spell-impacts-sfx | CC0（公有领域，免署名） | Fire / Water / Ice / Lightning Spell Impact |
| 代码合成层（低频下坠、高频脆响、铃音闪光） | 自有 | 全部 |

许可原文：`licenses/sonniss-gdc-2026-license.pdf`、`licenses/sonniss-gdc-2026-readme.txt`。lentikula 页面原文：
“I release this pack under a CC0 license, so you can use it in whatever project without attribution”。
原始素材在本机 `~/cho-siren-audio-src/`（不进仓库）；旧音效备份在 `~/cho-siren-backups/sfx-before-20261023/`。

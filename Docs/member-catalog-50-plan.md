# CHO-SIREN 50+ 成员目录方案

## 结论

当前成员页不是“素材太少”，而是数据结构只适合 9 名角色。项目内 `GameModel.Members` 是 9 项硬编码数组，存档又把成员身份保存为数组下标；如果直接追加、删减或调整排序，旧存档会把等级、解锁状态和编队错配到其他角色。

本次新增了独立的 `MemberCatalog`、清单校验、确定性战力规则与 v1→v2 迁移工具，但没有修改 `ChoSirenApp.cs` 或 `GameModel.cs`。主代理可在一次受控接线中启用它们。

## 已核对的当前事实

- 现有角色：9 名，定位为主唱、舞者、支援各 3 名；稀有度为 SSR、SR、R 各 3 名。
- 现有成员立绘：9 张，全部为 512×704、带透明通道的 PNG，位于 `Resources/Art/Members`。
- 当前成员页会为每名角色立即创建卡片，并同步 `Resources.Load<Sprite>`；54 名时会一次创建 54 个卡片和 54 张立绘。
- 当前存档键：`ChoSiren.Save.v1`；成员等级、解锁状态、编队均用整数下标。
- 本地素材清单 `browndust2_final_selection.csv` 有 760 个已标记 KEEP 且目标文件存在的候选，其中 444 个为核心角色图、316 个为场景图。
- `browndust2_character_classification.csv` 另有 1450 个 `TARGET_ADULT_FEMALE` 分类结果。
- 并行素材盘点已经从 `01.立绘` 的 299 个 KEEP 记录中按角色编号归并出 **55 个独立角色组、100 张变体**；每组选择一张后得到 55 张实际存在、有透明像素且 SHA-256 无完全重复的母版候选。详见 `Docs/hero-asset-inventory-50.md`。
- 这 55 张母版合计约 463.9 MiB，其中 10 张超大、4 张宽构图；不能原样全部复制进 Unity，应先生成统一尺寸的运行时副本。

这些素材数量足够，而且已有 55 个可执行角色组候选，但筛选表仍是训练/参考素材库，不等于可直接商用的游戏角色资产。导入前仍需做人设一致性、透明背景、裁切、安全区域、授权和重复角色检查。

## 推荐的首批 54 名分布

采用 54 名而不是刚好 50 名，便于三种定位和三个稀有度保持完全均衡：

| 稀有度 | 主唱 | 舞者 | 支援 | 合计 |
| --- | ---: | ---: | ---: | ---: |
| SSR | 4 | 4 | 4 | 12 |
| SR | 6 | 6 | 6 | 18 |
| R | 8 | 8 | 8 | 24 |
| 合计 | 18 | 18 | 18 | 54 |

现有 9 名必须保留原 ID：`xingli`、`feiyin`、`wubai`、`yeying`、`yaoguang`、`hupo`、`xianyue`、`chuxue`、`chengxia`。新角色使用不可变、可读的英文小写 ID；发布后不要改 ID，也不要把退役 ID 分配给新角色。

## 清单格式和路径

建议创建：

`Assets/Resources/Data/member-catalog.json`

最小结构：

```json
{
  "SchemaVersion": 1,
  "Members": [
    {
      "Id": "xingli",
      "Name": "星璃",
      "Role": "主唱",
      "Rarity": "SSR",
      "PortraitResourcePath": "Art/Members/xingli/portrait",
      "ThumbnailResourcePath": "Art/Members/xingli/thumb",
      "BasePower": 9200,
      "StartingLevel": 68,
      "InitiallyUnlocked": true
    }
  ]
}
```

推荐资源目录：

```text
Assets/Resources/Art/Members/<stable-id>/thumb.png
Assets/Resources/Art/Members/<stable-id>/portrait.png
```

运行时路径不带 `Resources/` 前缀和扩展名。`MemberCatalog.TryCreate` 会检查清单版本、数量、ID、中文名称、定位、稀有度、路径、基础战力、初始等级、重复 ID 和重复立绘。编辑器导入流程应传入资源存在性函数，并要求至少 50 名：

```csharp
MemberCatalog.TryCreate(manifest, out MemberCatalog catalog, out string error,
    minimumMemberCount: 50,
    resourceExists: path => Resources.Load<Sprite>(path) != null);
```

上面的 `Resources.Load` 仅适合编辑器构建校验，不应在每次启动时预载全部角色。

首发前另调用 `MemberCatalogRules.TryValidateLaunchDistribution`，它会强制 54 名、三种定位各 18 名，以及 SSR/SR/R 为 12/18/24；日后扩容时应改为独立的赛季配置门禁，不要复用首发精确数量规则。

## 数值生成规则

`MemberCatalogRules` 固定了当前原型的基础战力区间：

- SSR：8200–9600
- SR：7000–8199
- R：5800–6999

新增角色可调用：

```csharp
int basePower = MemberCatalogRules.DeterministicBasePower(memberId, rarity);
```

结果只取决于稳定 ID 和稀有度，不受生成顺序、机器或随机种子影响。正式数值策划仍可覆盖该值；确定性规则用于批量生成首版和防止每次构建漂移。

角色等级继续使用 1–100；当前战力公式可暂时保持 `BasePower + Level * 135`，避免扩容同时改变全局平衡。首批默认仅解锁现有四名，新增成员默认 1 级、未解锁。

## 存档迁移

### 风险

当前 v1 结构把 `UnlockedMembers`、`MemberLevels` 和 `Team` 保存为数组下标。角色数组一旦重排，存档仍能反序列化，但数据会静默绑定到错误角色，这是比崩溃更危险的错误。

### v2 结构

新增类型 `MemberRosterSaveV2` 使用稳定 ID：

```text
Members[] = { MemberId, Level, Unlocked }
TeamMemberIds[] = stable-id
```

推荐新键：`ChoSiren.Save.v2`。迁移流程：

1. 优先读取并校验 v2。
2. v2 不存在时读取 v1。
3. 使用发布时的“旧 9 名顺序”调用 `MemberSaveMigration.FromLegacy`，按旧定义的 ID 映射，而不是按新数组位置复制。
4. 对迁移结果调用 `ToIndexLists` 供尚未完全改造的现有 `GameModel` 使用。
5. 验证至少一名已解锁成员、队伍 1–4 人、等级 1–100 后，写入 v2 并 `PlayerPrefs.Save()`。
6. 保留 v1 至少两个公开版本作为回滚备份；不要迁移成功后立即删除。

`MemberSaveMigration` 会处理新增角色、重排、重复队员、越界旧下标和已退役角色，并保证迁移后仍有至少一名可用成员。

## 加载和 UI 性能

54 张 512×704 RGBA 图如果同时常驻 GPU，未压缩显存约为 74 MiB；再加背景、角色动画和 UI，会显著增加 WebGL 内存压力。当前同步构建所有卡片也会造成首帧卡顿。

建议分两期：

### 当前可交付版本

- 列表卡片使用 256×352 的 `thumb`，详情弹窗才加载 512×704 `portrait`。
- 成员页使用 `ScrollRect`，只维持可见区域约 12 个卡片，滚动时复用卡片对象。
- 用 `Resources.LoadAsync<Sprite>` 加载可见缩略图，并用小型 LRU 缓存；离屏后释放引用。
- 禁止 `Resources.LoadAll`，也不要在 `BuildMembers()` 中同步加载全部立绘。
- PNG 导入：Sprite Single、Mip Maps 关闭、透明裁切一致；缩略图 Max Size 256，详情图 Max Size 512。
- 卡片固定 3 列；54 名为 18 行，标题显示 `已拥有 x/54`，增加定位/稀有度筛选，不一次展开角色详情。

### 内容持续更新版本

将角色图迁移到 Addressables，并按角色或小分组远端下载。`Resources` 内的全部资产会被打入首包，WebGL 更新任意一张角色图也可能导致大数据文件整体缓存变化；Addressables 才适合后续持续增加角色和增量更新。

## 主代理接线 API

首次接线建议只改 `GameModel` 和成员页的数据来源，不改变战斗公式：

```csharp
if (!MemberCatalog.TryLoad(MemberCatalog.DefaultManifestResourcePath,
        out MemberCatalog catalog, out string error))
    throw new InvalidOperationException(error);

MemberDefinition[] definitions = catalog.ToLegacyDefinitions();
```

随后将 `GameModel.Members` 从硬编码数组切为由清单生成的只读结果。所有对外 UI 仍可按整数索引工作，但写盘前必须转换成稳定 ID；整数索引只能作为单次运行期间的缓存。

成员列表侧建议引入接口，避免 UI 永久依赖静态数组：

```csharp
public interface IMemberCatalog
{
    int Count { get; }
    MemberCatalogEntry this[int index] { get; }
    bool TryGetIndex(string memberId, out int index);
}
```

现有 `MemberCatalog` 已实现该接口；JSON 使用可序列化的 `MemberCatalogEntry`，校验完成后转为不可变的 `MemberCatalogRecord`，避免运行时改动 ID 导致索引失配。

## 构建门禁

至少保留这些门禁：

- 清单总数 `>= 50`，推荐精确为 54。
- 所有 ID 唯一且永不复用。
- 中文名称非空；定位仅为主唱/舞者/支援；稀有度仅为 SSR/SR/R。
- 角色分布满足 18/18/18，稀有度满足 12/18/24。
- 每个 `thumb` 与 `portrait` 都存在并可作为 Sprite 加载。
- 立绘 512×704、缩略图 256×352，透明通道和角色安全区域一致。
- v1 存档在新清单重排后，等级、解锁与编队仍跟随成员 ID。
- 成员页真实滚动到第 54 名并可打开详情；快速滚动时没有重复卡片、错图或过期异步回调。
- Windows 与 WebGL 分别记录首开耗时、成员页打开耗时和峰值内存。

新增 EditMode 测试 `MemberCatalogTests.cs` 覆盖 54 名清单、重复 ID、错误路径、确定性战力、重排扩容迁移、退役成员和队伍兜底。

## 仍需人工决定的风险

1. **授权**：本地棕色尘埃 2 素材清单可作为参考或模型训练素材，但不应默认视为可发布的原始游戏资产。
2. **角色唯一性**：同一角色的不同服装/姿态不能误当成多个英雄；先给 54 个稳定 ID，再选每个 ID 的唯一主立绘。
3. **美术一致性**：原始素材尺寸、视角、背景差异很大，必须批量抠图、统一 512×704 画布与脚底/头顶安全区。
4. **WebGL 首包**：继续使用 Resources 会随角色数线性增大首包；50+ 可做原型，但长期应切 Addressables。
5. **存档兼容**：在 v2 正式写盘前必须冻结旧 9 名的 ID 和顺序，并保留 v1 回滚数据。

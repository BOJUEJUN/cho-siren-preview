# 50+ 成员列表接入方案

## 已完成的可扩展底座

新增 `MemberRosterPagination`，把成员筛选、页码边界和三列网格位置从界面绘制中拆出。默认采用 3 列 × 3 行、每页 9 名成员；也支持传入 `pageSize: 12` 改为 3 列 × 4 行。

它只返回当前页的源数据索引，所以成员总数从 9 扩到 50、100 甚至更多时，成员页仍只需创建 9 个卡片对象。筛选会先作用于完整成员目录，再进行分页；结果中的索引仍指向 `GameModel.Members` 原始位置，能继续使用现有的等级、战力、签约和编队存档。

## 推荐接入 `BuildMembers`

在 `ChoSirenApp` 保存成员页状态：

```csharp
private int memberPageIndex;
private string memberRoleFilter;
private string memberRarityFilter;
```

构建当前页时：

```csharp
MemberRosterPage page = MemberRosterPagination.Build(
    GameModel.Members.Length,
    memberPageIndex,
    index =>
    {
        MemberDefinition member = GameModel.Members[index];
        bool roleMatches = string.IsNullOrEmpty(memberRoleFilter) || member.Role == memberRoleFilter;
        bool rarityMatches = string.IsNullOrEmpty(memberRarityFilter) || member.Rarity == memberRarityFilter;
        return roleMatches && rarityMatches;
    });

memberPageIndex = page.PageIndex;
for (int slot = 0; slot < page.VisibleCount; slot++)
{
    int memberIndex = page.SourceIndexAt(slot);
    MemberRosterCell cell = MemberRosterPagination.CellFor(slot);
    int x = 20 + cell.Column * 226;
    int y = 106 + cell.Row * 288;
    MemberGridCard(memberIndex, x, y, 214, 274);
}
```

页面底部增加“上一页”“下一页”和“第 N/M 页”。按钮可用状态直接绑定 `page.HasPrevious` / `page.HasNext`；点击后通过 `MemberRosterPagination.MovePage` 改页并重新调用成员页构建。切换角色定位或稀有度筛选时，应把 `memberPageIndex` 重置为 0。

筛选结果为空时显示独立空状态，不创建占位角色卡，也不要用重复立绘充数。

## 50+ 数据与素材约束

- 每一条 `MemberDefinition.Id` 必须唯一，已有存档目前以数组索引保存等级和签约状态，因此首批 50+ 角色上线后不要随意重新排序。
- `ResourcePath` 必须指向真实且不同的本地角色立绘；缺素材时保留“待导入”状态，不得复制现有图片伪装成新英雄。
- 批量导入前生成清单，检查 ID、中文名、定位、稀有度、资源路径重复和资源缺失。
- 成员数量变化后让现有 `NormalizeSave` 扩展 `MemberLevels`，并保留旧索引顺序，确保旧存档兼容。
- 一页只加载可见立绘。若后续使用 Addressables，应在翻页时释放上一页句柄并预取下一页，而不是一次把 50+ 张大图全部常驻内存。

## 验证范围

`MemberRosterPaginationTests` 覆盖：

- 50 名成员按 9 张卡分成 6 页，全部出现且不重复；
- 12 张卡模式最后一页边界；
- 筛选后分页与原始索引保持一致；
- 空筛选、负页码和超大页码；
- 三列网格坐标；
- 翻页边界和整数溢出；
- 非法参数。

下一步由主界面任务把该模块接入 `BuildMembers`，再增加 PlayMode 点击翻页和筛选测试。角色目录扩充应等待本地素材盘点清单，确认至少 50 份真实可用素材后再落到 `GameModel.Members`。

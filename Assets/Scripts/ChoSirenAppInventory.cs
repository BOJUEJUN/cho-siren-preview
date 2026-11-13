using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Accessory tab redone after NIKKE's 库存 screen: a dark header card (title, tabs, sort),
    /// a light inventory panel with a vertical slot filter on the left and a scrolling 5-column
    /// grid of item tiles. Tapping a tile opens the item card (tier, stats, upgrade, equip).
    /// Equipping per member lives on the member detail page, as in the reference.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private bool inventoryCatalog;
        private int inventorySort;
        private static readonly string[] InventorySortNames = { "稀有度", "强化等级", "最近获得" };
        private static readonly Color InventoryInk = new Color32(240, 234, 250, 255);
        private static readonly Color InventoryPanel = new Color32(12, 8, 22, 255);
        private static readonly Color InventoryTile = new Color32(30, 22, 46, 255);

        private static Color RarityColor(int item)
        {
            ColorUtility.TryParseHtmlString(GameModel.AccessoryRarityColorHexOf(item), out Color c);
            return c;
        }

        private int inventoryPage;
        /// <summary>Category tab on the 库存 page: -1 全部 (default, 2026-11-08), 0 耳饰, 1 项链, 2 手饰, 3 头饰, 4 材料 (11-09).</summary>
        private int inventorySlot = -1;
        private const int InventoryMaterialsTab1109 = 4;
        /// <summary>2026-11-09 (少爷: "加一个材料分类…用来升级的那些总的有个看得见的地方"): what the 材料 tab lists.</summary>
        private static readonly (string Id, string Use)[] InventoryMaterials1109 =
        {
            (CurrencyIds.VoiceCrystal, "30 级后每次升级都要用；关卡掉落"),
            (CurrencyIds.VoiceCore, "可代替任何种族的生魂用于突破"),
            (CurrencyIds.SoulCharm, "魅族成员突破用"),
            (CurrencyIds.SoulDemon, "魔族成员突破用"),
            (CurrencyIds.SoulSea, "海灵族成员突破用"),
            (CurrencyIds.SoulBlood, "血精灵成员突破用"),
            (CurrencyIds.Shard, "生魂不够时 10 个碎片抵 1 个生魂"),
            (CurrencyIds.RecruitTicket, "选秀签约用"),
            (CurrencyIds.CostumeTicket, "换装用"),
        };
        private const string InventoryArt = "Art/Ui1107/";
        /// <summary>PSD (装备2.psd, 1329 wide) → board: 0.65 scale, PSD y 107 (the pig's top) at board y 195 under the HUD.</summary>
        private static Rect Inv1107(float x, float y, float w, float h) => new Rect(x * .65f, 195f + (y - 107f) * .65f, w * .65f, h * .65f);
        private static readonly float[] InventoryRowY = { 597f, 905f, 1195f, 1518f };
        private static readonly float[] InventoryCellX = { 298f, 542f, 783f, 1019f };
        private const int InventoryPerRow = 4;

        /// <summary>
        /// 2026-11-07 库存 (赵金 装备2.psd): title sticker, 饰品 / 图鉴 tabs and the sort chip over the PSD frame.
        /// 2026-11-08: the 耳饰 / 项链 / 手饰 / 头饰 plates are category tabs and the 4×4 grid shows only that category;
        /// the cell's fill and bottom bar show the quality. ‹ › pages the category (会议: 库存分页).
        /// </summary>
        private void BuildInventory()
        {
            RectTransform root = ReferenceBoard038("InventoryBoard", InventoryArt + "page-bg-board");
            CoverPageBackground1108(root);

            Image frame = NewImage("InventoryFrame", root, Resources.Load<Sprite>(InventoryArt + "inv-frame"), White).GetComponent<Image>();
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1f / .65f;
            frame.raycastTarget = false;
            Rect frameRect = Inv1107(30, 240, 1270, 0);
            PlaceTop(frame.rectTransform, frameRect.x, frameRect.y, frameRect.width, 1460f - frameRect.y);

            InventoryArtImage(root, "InventoryTitle", "inv-title", Inv1107(60, 207, 312, 223));
            Rect help = Inv1107(380, 279, 63, 62);
            VisualB1101.HelpButton(root, "InventoryHelp", help.x, help.y, help.width, "饰品说明", AccessoryHelpLines);

            string[] tabs = { "饰品", "图鉴" };
            Rect[] tabRects = { Inv1107(81, 427, 261, 98), Inv1107(351, 430, 242, 94) };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = inventoryCatalog == (i == 1);
                bool catalog = i == 1;
                GameObject tab = BoardButton038("InventoryTab-" + i, root, tabRects[i], () =>
                { inventoryCatalog = catalog; inventoryPage = 0; ShowScreen("accessory"); });
                Image face = tab.GetComponent<Image>();
                face.sprite = Resources.Load<Sprite>(InventoryArt + (active ? "inv-tab-on" : "inv-tab-off"));
                face.color = White;
                BoardText038(tab.transform, "Label", tabs[i], 24, active ? (Color)Brand1025.Ink : White, 0, 0, tabRects[i].width, tabRects[i].height,
                    TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            }

            Rect sortRect = Inv1107(910, 430, 330, 100);
            // 2026-11-08 (少爷: "稀有度的按键是点击后展开可以选择的面板"): the sort plate opens a drop-down to choose from.
            GameObject sort = BoardButton038("InventorySort", root, sortRect, () => ToggleInventorySortMenu1108(root, sortRect));
            sort.GetComponent<Image>().sprite = Resources.Load<Sprite>(InventoryArt + "inv-sort");
            sort.GetComponent<Image>().color = White;
            BoardText038(sort.transform, "Label", InventorySortNames[inventorySort] + "  ▼", 22, White, 66, 0, sortRect.width - 76, sortRect.height,
                TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;

            // 2026-11-12 (原画同事: "这个角色缩小，3 分之一"): the pig is a third smaller (2/3 of 422×329), keeps its centre
            // line and now sits just above the sort plate (PSD y 430) instead of overlapping it.
            const float pigW = 422f * 2f / 3f, pigH = 329f * 2f / 3f, pigBottom = 426f;
            InventoryArtImage(root, "InventoryPig", "inv-pig", Inv1107(1080f - pigW * .5f, pigBottom - pigH, pigW, pigH));

            // 2026-11-08 (少爷: "分类还是加一个全部，然后默认选中的是全部"): five category plates down the left — 全部 (default),
            // 耳饰, 项链, 手饰, 头饰 — evenly spaced over the grid's height; the 4×4 grid shows the chosen category and
            // ‹ › pages through it 16 at a time. Selected plate = solid teal (labels are live text on blank plates).
            inventorySlot = Mathf.Clamp(inventorySlot, -1, InventoryMaterialsTab1109);
            bool materials = inventorySlot == InventoryMaterialsTab1109;
            // 饰品 lists only what you hold; 图鉴 lists every material (11-09, 少爷: "我没有点击图鉴但是显示全部材料了").
            int[] items = materials
                ? InventoryMaterialOrder1109()
                : InventoryItems(inventorySlot);
            int perPage = InventoryPerRow * InventoryRowY.Length;
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Length / (float)perPage));
            inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
            // Six plates (全部, four slots, 材料) stacked close together from the grid's top
            // (2026-11-09, 少爷: "这几个UI紧凑些吧").
            const float plateH = 116f;
            float columnTop = InventoryRowY[0];
            const float plateStep = plateH + 26f;
            for (int slot = -1; slot <= InventoryMaterialsTab1109; slot++)
            {
                int chosen = slot;
                bool on = slot == inventorySlot;
                float y = columnTop + (slot + 1) * plateStep;
                Rect plateRect = Inv1107(58, y, 207, plateH);
                GameObject plate = BoardButton038(slot < 0 ? "InventorySlot-All" : slot == InventoryMaterialsTab1109 ? "InventorySlot-Materials" : "InventorySlot-" + slot, root, plateRect, () =>
                {
                    if (inventorySlot == chosen) return;
                    inventorySlot = chosen; inventoryPage = 0; ShowScreen("accessory");
                });
                Image plateFace = plate.GetComponent<Image>();
                plateFace.sprite = Resources.Load<Sprite>(InventoryArt + (on ? "inv-slot-blank-on" : "inv-slot-blank"));
                plateFace.color = White;
                Text plateLabel = BoardText038(plate.transform, "Label", slot < 0 ? "全部" : slot == InventoryMaterialsTab1109 ? "材料" : GameModel.EquipmentSlotNames[slot], 24,
                    on ? (Color)Brand1025.Ink : White, 0, 0, plateRect.width, plateRect.height, TextAnchor.MiddleCenter);
                plateLabel.fontStyle = FontStyle.Bold;
            }
            RectTransform grid = NewObject("InventoryGrid", root).AddComponent<RectTransform>();
            Stretch(grid);
            for (int row = 0; row < InventoryRowY.Length; row++)
            {
                for (int c = 0; c < InventoryPerRow; c++)
                {
                    int n = inventoryPage * perPage + row * InventoryPerRow + c;
                    Rect cell = Inv1107(InventoryCellX[c], InventoryRowY[row] + 2f, 218, 281);
                    if (n < items.Length && materials)
                    {
                        int m = items[n];
                        InventoryMaterialCell1109(grid, m, cell);
                    }
                    else if (n < items.Length)
                    {
                        int item = items[n];
                        InventoryCell1107(grid, item, cell, () => OpenAccessoryCard(item, -1));
                    }
                    else InventoryArtImage(grid, $"InventoryEmpty-{row}-{c}", "inv-cell-empty", cell);
                }
            }

            int owned = model.Save.OwnedAccessories.Distinct().Count();
            int slotOwned = model.Save.OwnedAccessories.Distinct().Count(i => inventorySlot < 0 || GameModel.AccessorySlot(i) == inventorySlot);
            int slotTotal = Enumerable.Range(0, GameModel.AccessoryNames.Length).Count(i => inventorySlot < 0 || GameModel.AccessorySlot(i) == inventorySlot);
            BoardText038(root, "InventoryCount", materials ? (inventoryCatalog ? $"已收集 {InventoryMaterials1109.Count(m => model.Balance(m.Id) > 0)}/{InventoryMaterials1109.Length}" : $"共 {items.Length} 种材料") : inventoryCatalog ? $"已收集 {slotOwned}/{slotTotal}" : $"共 {slotOwned} 件",
                20, new Color32(190, 178, 222, 255), 50, 1322, 200, 50, TextAnchor.MiddleLeft);
            // ‹ 1/3 › with the shared switch arrows.
            BoardText038(root, "InventoryPage", $"{inventoryPage + 1}/{pages}", 26, White, 382, 1318, 100, 56, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            Button previous = SwitchArrow1107.Create(root, "InventoryPreviousPage", -1, new Rect(300, 1306, 80, 80), () =>
            { inventoryPage = (inventoryPage - 1 + pages) % pages; ShowScreen("accessory"); });
            Button next = SwitchArrow1107.Create(root, "InventoryNextPage", 1, new Rect(484, 1306, 80, 80), () =>
            { inventoryPage = (inventoryPage + 1) % pages; ShowScreen("accessory"); });
            previous.interactable = next.interactable = pages > 1;

            if (owned == 0 && !inventoryCatalog && !materials)
            {
                GameObject go = ModalPolish038Chrome.ActionButton(root, "InventoryEmpty", "去关卡", 24, OpenLevelMap, ModalPolish038ButtonStyle.Primary);
                PlaceTop(go.GetComponent<RectTransform>(), 600, 1310, 210, 64);
                VisualB1101.AddButtonIcon(go, "pin");
            }
        }

        private GameObject InventoryArtImage(Transform parent, string name, string sprite, Rect rect)
        {
            GameObject go = NewImage(name, parent, Resources.Load<Sprite>(InventoryArt + sprite), White);
            PlaceTop(go.GetComponent<RectTransform>(), rect.x, rect.y, rect.width, rect.height);
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        /// <summary>Quality cell art for a rarity: grey / teal / purple / pink (Legendary shares pink with a gold rim).</summary>
        private static int InventoryQuality(int item)
        {
            int rarity = (int)GameModel.AccessoryRarityOf(item);
            return Mathf.Clamp(rarity, 0, 3);
        }

        /// <summary>One PSD cell: quality fill + bar, the item art, LV and the wearer's face; dim when not owned.</summary>
        /// <summary>材料 quality (cell art q1–q3): 声核 3, the race 生魂 2, everything else 1.</summary>
        private static int MaterialQuality1109(string id) => id == CurrencyIds.VoiceCore ? 3 : id.StartsWith("soul-") ? 2 : 1;

        /// <summary>
        /// 11-09 (少爷: "没有按照稀有度排序吗材料"): the 材料 tab follows the sort chip — 稀有度 (and 强化等级, which
        /// materials don't have) puts the rarest first; 最近获得 puts the biggest stacks first. Held ones lead in 图鉴.
        /// </summary>
        private int[] InventoryMaterialOrder1109()
        {
            var list = Enumerable.Range(0, InventoryMaterials1109.Length)
                .Where(m => inventoryCatalog || model.Balance(InventoryMaterials1109[m].Id) > 0);
            var ordered = list.OrderByDescending(m => model.Balance(InventoryMaterials1109[m].Id) > 0);
            ordered = inventorySort == 2
                ? ordered.ThenByDescending(m => model.Balance(InventoryMaterials1109[m].Id))
                : ordered.ThenByDescending(m => MaterialQuality1109(InventoryMaterials1109[m].Id));
            return ordered.ThenBy(m => m).ToArray();
        }

        /// <summary>One 材料 tile: the material's art, its name and how many you hold; tapping says what it is for.</summary>
        private GameObject InventoryMaterialCell1109(Transform parent, int index, Rect cell)
        {
            var (id, use) = InventoryMaterials1109[index];
            int count = model.Balance(id);
            string name = GameModel.CurrencyName(id);
            GameObject tile = BoardButton038("Material-" + id, parent, cell, () => Toast($"{name}：{use}（持有 {count:N0}）"));
            Image face = tile.GetComponent<Image>();
            face.sprite = Resources.Load<Sprite>(InventoryArt + "inv-cell-q" + MaterialQuality1109(id));
            face.color = count > 0 ? White : new Color(.55f, .52f, .62f, .75f);
            GameObject art = NewImage("ItemArt", tile.transform, ChoSiren.UI.RewardItemVisuals.SpriteFor(id), count > 0 ? White : new Color(.6f, .55f, .7f, .6f));
            PlaceTop(art.GetComponent<RectTransform>(), 22, 16, cell.width - 44, cell.height - 76);
            art.GetComponent<Image>().preserveAspect = true;
            art.GetComponent<Image>().raycastTarget = false;
            Text label = BoardText038(tile.transform, "Name", name, 13, InventoryInk, 6, cell.height - 54, cell.width - 12, 18, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            PanelKit.EnableBestFit(label, 10);
            Text amount = BoardText038(tile.transform, "Count", "×" + count.ToString("N0"), 20, InventoryInk, 6, cell.height - 36, cell.width - 12, 28, TextAnchor.MiddleCenter);
            amount.fontStyle = FontStyle.BoldAndItalic;
            return tile;
        }

        private GameObject InventoryCell1107(Transform parent, int item, Rect cell, UnityEngine.Events.UnityAction action)
        {
            bool owned = model.OwnsAccessory(item);
            GameObject tile = BoardButton038("Accessory-" + item, parent, cell, action);
            Image face = tile.GetComponent<Image>();
            face.sprite = Resources.Load<Sprite>(InventoryArt + "inv-cell-q" + InventoryQuality(item));
            face.color = owned ? White : new Color(.55f, .52f, .62f, .75f);
            if (GameModel.AccessoryRarityOf(item) == GameModel.AccessoryRarity.Legendary)
            {
                Outline gold = tile.AddComponent<Outline>();
                gold.effectColor = new Color32(255, 205, 96, 200);
                gold.effectDistance = new Vector2(2f, -2f);
            }
            GameObject art = NewImage("ItemArt", tile.transform, AccessoryItemSprite(item), owned ? White : new Color(.45f, .4f, .55f, .5f));
            PlaceTop(art.GetComponent<RectTransform>(), 14, 18, cell.width - 28, cell.height - 64);
            art.GetComponent<Image>().preserveAspect = true;
            art.GetComponent<Image>().raycastTarget = false;
            if (owned)
            {
                BoardText038(tile.transform, "LvTag", "LV.", 11, InventoryInk, 8, cell.height - 50, 30, 14);
                BoardText038(tile.transform, "Level", (model.AccessoryUpgradeLevel(item) + 1).ToString("00"), 24, InventoryInk, 6, cell.height - 42, 56, 30);
                int wearer = model.AccessoryOwner(item);
                if (wearer >= 0) MemberHead1101(tile.transform, "Wearer", wearer, cell.width - 48, cell.height - 56, 42, Brand1025.Teal);
            }
            else BoardText038(tile.transform, "Locked", "未获得", 15, InventoryInk, 0, cell.height - 40, cell.width, 24, TextAnchor.MiddleCenter);
            return tile;
        }

        private int[] InventoryItems(int slotFilter)
        {
            IEnumerable<int> items = inventoryCatalog
                ? Enumerable.Range(0, GameModel.AccessoryNames.Length)
                : model.Save.OwnedAccessories.Distinct();
            if (slotFilter >= 0) items = items.Where(i => GameModel.AccessorySlot(i) == slotFilter);
            switch (inventorySort)
            {
                case 1: items = items.OrderByDescending(i => model.AccessoryUpgradeLevel(i)).ThenByDescending(i => (int)GameModel.AccessoryRarityOf(i)); break;
                case 2:
                    var obtained = model.Save.OwnedAccessories;
                    items = items.OrderByDescending(i => obtained.IndexOf(i)).ThenBy(i => i);
                    break;
                default: items = items.OrderByDescending(i => model.OwnsAccessory(i)).ThenByDescending(i => (int)GameModel.AccessoryRarityOf(i)).ThenBy(i => i); break;
            }
            return items.ToArray();
        }

        /// <summary>One NIKKE-style item tile: art, rarity hex, LV.00, rarity bar, wearer tag.</summary>
        private GameObject InventoryTileView(Transform parent, int item, Rect cell, UnityEngine.Events.UnityAction action)
        {
            bool owned = model.OwnsAccessory(item);
            GameObject tile = BoardButton038("Accessory-" + item, parent, cell, action);
            tile.GetComponent<Image>().color = owned ? InventoryTile : new Color32(20, 16, 30, 255);
            Outline rim = tile.AddComponent<Outline>();
            Color rimColor = RarityColor(item); rimColor.a = owned ? .55f : .2f;
            rim.effectColor = rimColor;
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            SirenStickerUi.SkinPanel(tile, false);
            GameObject art = NewImage("ItemArt", tile.transform, AccessoryItemSprite(item), owned ? White : new Color(.45f, .4f, .55f, .45f));
            PlaceTop(art.GetComponent<RectTransform>(), 24, 10, cell.width - 30, cell.height - 46);
            art.GetComponent<Image>().preserveAspect = true;
            art.GetComponent<Image>().raycastTarget = false;
            Color rarity = RarityColor(item);
            GameObject hex = new GameObject("RarityHex", typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            hex.transform.SetParent(tile.transform, false);
            PlaceTop(hex.GetComponent<RectTransform>(), 3, 3, 34, 36);
            ProfileEmblemFrame038 hexShape = hex.GetComponent<ProfileEmblemFrame038>();
            hexShape.SolidHex = true; hexShape.color = new Color32(8, 4, 16, 255); hexShape.raycastTarget = false;
            BoardText038(hex.transform, "Slot", GameModel.EquipmentSlotNames[Math.Max(0, GameModel.AccessorySlot(item))].Substring(0, 1), 18, rarity,
                0, 0, 34, 36, TextAnchor.MiddleCenter);
            GameObject bar = NewImage("RarityBar", tile.transform, null, owned ? rarity : new Color32(70, 62, 88, 255));
            PlaceTop(bar.GetComponent<RectTransform>(), 0, cell.height - 10, cell.width, 10);
            bar.GetComponent<Image>().raycastTarget = false;
            if (owned)
            {
                BoardText038(tile.transform, "LvTag", "LV.", 11, InventoryInk, 6, cell.height - 52, 30, 14);
                BoardText038(tile.transform, "Level", (model.AccessoryUpgradeLevel(item) + 1).ToString("00"), 26, InventoryInk, 4, cell.height - 42, 56, 32);
                int wearer = model.AccessoryOwner(item);
                if (wearer >= 0)
                    // 2026-11-01 visual-b: the wearer's face sticker instead of her name.
                    MemberHead1101(tile.transform, "Wearer", wearer, cell.width - 50, cell.height - 56, 46, Brand1025.Teal);
            }
            else BoardText038(tile.transform, "Locked", "未获得", 15, InventoryInk, 0, cell.height - 40, cell.width, 26, TextAnchor.MiddleCenter);
            return tile;
        }

        // ------------------------------------------------------------------ item card

        /// <summary>
        /// Item card. <paramref name="member"/> ≥ 0 when opened from a member's slot: the buttons then
        /// read 解除 / 交换 for that member. 2026-11-01 visual-b: hero shot + icons (ChoSirenAppInventory1101.cs).
        /// </summary>
        private void OpenAccessoryCard(int item, int member, Action afterChange = null)
            => OpenAccessoryCard1101(item, member, afterChange);

        private static readonly Color[] AttributeColors =
            { new Color32(80, 200, 120, 255), new Color32(255, 92, 120, 255), new Color32(90, 160, 255, 255) };

        /// <summary>Small coloured hex in front of 体力 / 攻击力 / 防御力 (font-independent).</summary>
        private void AttributeHex(Transform parent, string name, float x, float y, int kind)
        {
            GameObject hex = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            hex.transform.SetParent(parent, false);
            PlaceTop(hex.GetComponent<RectTransform>(), x, y, 18, 20);
            ProfileEmblemFrame038 shape = hex.GetComponent<ProfileEmblemFrame038>();
            shape.SolidHex = true; shape.color = AttributeColors[Mathf.Clamp(kind, 0, 2)]; shape.raycastTarget = false;
        }

        /// <summary>Scaled 720-wide page inside a full-screen overlay (shared by detail popups).</summary>
        private RectTransform DetailPage(GameObject overlay, out float pageH)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform host = overlay.GetComponent<RectTransform>();
            float hostW = host.rect.width > 1 ? host.rect.width : 720f;
            float hostH = host.rect.height > 1 ? host.rect.height : 1536f;
            float scale = hostW / DetailWidth;
            pageH = hostH / scale;
            GameObject pageObject = NewObject("Page", overlay.transform);
            RectTransform page = pageObject.AddComponent<RectTransform>();
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, 1f);
            page.anchoredPosition = Vector2.zero;
            page.sizeDelta = new Vector2(DetailWidth, pageH);
            page.localScale = Vector3.one * scale;
            return page;
        }

        /// <summary>交换: pick any owned item for the member's slot (worn-by-others items transfer).</summary>
        private void OpenAccessorySwap(int member, int slot, Action afterChange)
        {
            GameObject overlay = NewImage("AccessorySwap", safeRoot, null, new Color32(4, 2, 10, 210));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            RectTransform page = DetailPage(overlay, out float pageH);
            GameObject card = NewImage("Card", page, null, new Color32(14, 9, 24, 255));
            float height = Mathf.Min(pageH - 300f, 980f);
            PlaceTop(card.GetComponent<RectTransform>(), 40, Mathf.Max(150f, (pageH - height) * .5f), 640, height);
            SirenStickerUi.SkinPanel(card);
            // 2026-11-01 visual-b: her face sticker + the slot, no sentence.
            MemberHead1101(card.transform, "TitleHead", member, 26, 14, 60, Brand1025.Teal);
            VisualB1101.NeonIcon(card.transform, "TitleSlotIcon", "hanger", Brand1025.Teal, 100, 26, 36, false);
            SirenStickerUi.SkinTitle(BoardText038(card.transform, "Title", GameModel.EquipmentSlotNames[Math.Max(0, slot)], 30, InventoryInk, 144, 20, 300, 50));
            GameObject close = BoardButton038("Close", card.transform, new Rect(566, 14, 64, 64), () => Destroy(overlay));
            BoardText038(close.transform, "X", "×", 54, InventoryInk, 0, 0, 64, 64, TextAnchor.MiddleCenter);
            SirenStickerUi.SkinButton(close, false, true);
            int current = model.EquippedInSlot(member, slot);
            int[] items = model.Save.OwnedAccessories.Distinct().Where(i => GameModel.AccessorySlot(i) == slot)
                .OrderByDescending(i => (int)GameModel.AccessoryRarityOf(i)).ThenByDescending(i => model.AccessoryUpgradeLevel(i)).ToArray();
            GameObject viewport = NewImage("Viewport", card.transform, null, new Color(1, 1, 1, .004f));
            PlaceTop(viewport.GetComponent<RectTransform>(), 20, 84, 600, height - 104);
            viewport.AddComponent<RectMask2D>();
            GameObject gridObject = NewObject("Grid", viewport.transform);
            RectTransform grid = gridObject.AddComponent<RectTransform>();
            PlaceTop(grid, 0, 0, 600, Mathf.Max(1, (items.Length + 3) / 4) * 168 + 10);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = grid; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            for (int n = 0; n < items.Length; n++)
            {
                int item = items[n];
                GameObject tile = InventoryTileView(grid, item, new Rect(8 + (n % 4) * 148, 6 + (n / 4) * 168, 136, 156), () =>
                {
                    if (item != current) { model.EquipAccessoryForMember(member, item, out string message); Toast(message); }
                    Destroy(overlay);
                    afterChange?.Invoke();
                });
                if (item == current)
                {
                    GameObject mark = NewImage("Current", tile.transform, PunkUi.SlantFill, Cyan);
                    PlaceTop(mark.GetComponent<RectTransform>(), 70, 4, 62, 22);
                    mark.GetComponent<Image>().raycastTarget = false;
                    BoardText038(mark.transform, "Label", "当前", 13, Color.black, 0, 0, 62, 22, TextAnchor.MiddleCenter);
                }
            }
            if (items.Length == 0)
            {
                VisualB1101.NeonIcon(grid, "EmptyIcon", "hanger", new Color32(169, 156, 196, 160), 230, 110, 140);
                GameObject go = ModalPolish038Chrome.ActionButton(grid, "Empty", "去关卡", 24, () => { Destroy(overlay); OpenLevelMap(); },
                    ModalPolish038ButtonStyle.Primary);
                PlaceTop(go.GetComponent<RectTransform>(), 180, 280, 240, 72);
                VisualB1101.AddButtonIcon(go, "pin");
            }
        }

        /// <summary>穿戴: the large-card member picker; tapping one equips the item to her (ChoSirenAppInventory1101.cs).</summary>
        private void OpenAccessoryMemberPicker(int item) => OpenAccessoryWearPicker1101(item);
    
        /// <summary>2026-10-21: a successful 强化 shows the P5 banner with the item and its new level.</summary>
        private void UpgradeFeedback1022(int accessory, bool ok, string message)
        {
            if (!ok || accessory < 0 || accessory >= GameModel.AccessoryItemIds.Length) { Toast(message); return; }
            ChoSiren.UI.RewardBurst1022.Play(safeRoot, font, "强化成功",
                new List<(string, int)> { (GameModel.AccessoryItemIds[accessory], 1) },
                new List<string> { "Lv." + (model.AccessoryUpgradeLevel(accessory) + 1) });
        }

        /// <summary>Drop-down under the sort plate: one row per order, the current one solid teal; tap outside to close.</summary>
        private void ToggleInventorySortMenu1108(RectTransform root, Rect anchor)
        {
            Transform existing = root.Find("InventorySortMenu");
            if (existing != null)
            {
                Destroy(existing.gameObject);
                Transform oldBlocker = root.Find("InventorySortBlocker");
                if (oldBlocker != null) Destroy(oldBlocker.gameObject);
                return;
            }
            // A full-screen tap catcher with a light even dim (the board is scaled, so it is simply oversized).
            GameObject blocker = NewImage("InventorySortBlocker", root, null, new Color(0f, 0f, 0f, .16f));
            RectTransform blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = blockerRect.anchorMax = blockerRect.pivot = new Vector2(.5f, .5f);
            blockerRect.anchoredPosition = Vector2.zero;
            blockerRect.sizeDelta = new Vector2(12000f, 12000f);
            blocker.GetComponent<Image>().raycastTarget = true;
            GameObject menu = NewImage("InventorySortMenu", root, null, new Color32(16, 10, 34, 246));
            blocker.AddComponent<Button>().onClick.AddListener(() => { Destroy(menu); Destroy(blocker); });
            const float rowH = 62f, pad = 8f;
            float width = anchor.width;
            float height = pad * 2f + rowH * InventorySortNames.Length;
            RectTransform menuRect = menu.GetComponent<RectTransform>();
            PlaceTop(menuRect, anchor.x, anchor.yMax + 6f, width, height);
            Outline rim = menu.AddComponent<Outline>();
            rim.effectColor = Brand1025.Teal; rim.effectDistance = new Vector2(2f, -2f);
            for (int i = 0; i < InventorySortNames.Length; i++)
            {
                int order = i;
                bool on = i == inventorySort;
                GameObject row = BoardButton038("InventorySortOption-" + i, menu.transform, new Rect(pad, pad + rowH * i, width - pad * 2f, rowH - 6f), () =>
                {
                    Destroy(menu); Destroy(blocker);
                    if (inventorySort == order) return;
                    inventorySort = order; inventoryPage = 0; ShowScreen("accessory");
                });
                row.GetComponent<Image>().color = on ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .06f);
                BoardText038(row.transform, "Label", InventorySortNames[i], 22, on ? (Color)Brand1025.Ink : White, 0, 0, width - pad * 2f, rowH - 6f,
                    TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            }
            menu.AddComponent<DropdownPop1108>();
        }
}
}

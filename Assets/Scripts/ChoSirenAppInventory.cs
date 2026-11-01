using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
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

        private void BuildInventory()
        {
            RectTransform root = ReferenceBoard038("InventoryBoard", "Art/Reference038/team-board-038");
            int slotFilter = Mathf.Clamp(equipmentSlotFilter, -1, GameModel.EquipmentSlotCount - 1);

            // ---- dark header card
            GameObject header = NewImage("InventoryHeader", root, null, new Color32(14, 10, 22, 245));
            PlaceTop(header.GetComponent<RectTransform>(), 30, 200, 803, 236);
            SirenStickerUi.SkinPanel(header);
            header.GetComponent<Image>().raycastTarget = false;
            GameObject accent = NewImage("Accent", header.transform, null, Brand1025.Teal);
            PlaceTop(accent.GetComponent<RectTransform>(), 30, 26, 6, 52);
            SirenStickerUi.SkinTitle(BoardText038(header.transform, "Title", "库存", 54, White, 48, 14, 300, 72), true);
            // 2026-11-01 visual-b: the explanation line lives behind 「?」.
            VisualB1101.HelpButton(header.transform, "InventoryHelp", 214, 30, 40, "饰品说明", AccessoryHelpLines);
            string[] tabs = { "饰品", "图鉴" };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = inventoryCatalog == (i == 1);
                bool catalog = i == 1;
                GameObject tab = BoardButton038("InventoryTab-" + i, header.transform, new Rect(24 + i * 182, 146, 170, 64), () =>
                { inventoryCatalog = catalog; ShowScreen("accessory"); });
                Image face = tab.GetComponent<Image>();
                face.sprite = PunkUi.SlantFill;
                face.color = active ? (Color)PunkUi.Hot : new Color32(38, 32, 50, 255);
                BoardText038(tab.transform, "Label", tabs[i], 24, White, 0, 0, 170, 64, TextAnchor.MiddleCenter);
                SirenPageChrome.Tab(tab, active);
            }
            GameObject sort = BoardButton038("InventorySort", header.transform, new Rect(530, 146, 250, 64), () =>
            { inventorySort = (inventorySort + 1) % InventorySortNames.Length; ShowScreen("accessory"); });
            sort.GetComponent<Image>().sprite = PunkUi.SlantFill;
            sort.GetComponent<Image>().color = new Color32(38, 32, 50, 255);
            Text sortLabel = BoardText038(sort.transform, "Label", InventorySortNames[inventorySort], 22, White, 30, 0, 220, 64, TextAnchor.MiddleCenter);
            VisualB1101.NeonIcon(sort.transform, "SortIcon", "sort", Brand1025.Teal, 104 - sortLabel.preferredWidth * .5f, 18, 28, false);
            SirenStickerUi.SkinButton(sort, false, true);

            // ---- light inventory panel
            GameObject panel = NewImage("InventoryPanel", root, null, InventoryPanel);
            PlaceTop(panel.GetComponent<RectTransform>(), 30, 450, 803, 1090);
            panel.GetComponent<Image>().raycastTarget = false;
            Outline panelEdge = panel.AddComponent<Outline>();
            panelEdge.effectColor = new Color32(255, 46, 147, 150);
            panelEdge.effectDistance = new Vector2(2, -2);
            SirenStickerUi.SkinPanel(panel, false);

            // Vertical slot filter (全部 + the four equipment slots).
            for (int f = -1; f < GameModel.EquipmentSlotCount; f++)
            {
                int filter = f;
                bool active = slotFilter == f;
                GameObject chip = BoardButton038("InventoryFilter-" + (f + 1), panel.transform, new Rect(20, 24 + (f + 1) * 104, 104, 92), () =>
                { equipmentSlotFilter = filter; ShowScreen("accessory"); });
                chip.GetComponent<Image>().color = active ? (Color)PunkUi.Hot : new Color32(44, 38, 56, 255);
                if (f < 0) BoardText038(chip.transform, "Label", "全部", 24, White, 0, 0, 104, 92, TextAnchor.MiddleCenter);
                else
                {
                    int sample = Enumerable.Range(0, GameModel.AccessoryNames.Length).FirstOrDefault(i => GameModel.AccessorySlot(i) == filter);
                    GameObject icon = NewImage("Icon", chip.transform, AccessoryItemSprite(sample), White);
                    PlaceTop(icon.GetComponent<RectTransform>(), 22, 6, 60, 54);
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().raycastTarget = false;
                    BoardText038(chip.transform, "Label", GameModel.EquipmentSlotNames[f], 17, White, 0, 60, 104, 28, TextAnchor.MiddleCenter);
                }
                SirenPageChrome.Tab(chip, active);
            }

            int[] items = InventoryItems(slotFilter);
            BoardText038(panel.transform, "InventoryCount", inventoryCatalog
                ? $"已收集 {model.Save.OwnedAccessories.Distinct().Count()}/{GameModel.AccessoryNames.Length}"
                : $"共 {items.Length} 件", 18, new Color32(169, 156, 196, 255), 20, 560, 110, 60, TextAnchor.MiddleCenter);

            // Scrolling 5-column grid.
            const float viewX = 140f, viewY = 18f, viewW = 650f, viewH = 1056f;
            const float tileW = 118f, tileH = 140f, gapX = 12f, gapY = 14f;
            GameObject viewport = NewImage("InventoryViewport", panel.transform, null, new Color(1, 1, 1, .004f));
            PlaceTop(viewport.GetComponent<RectTransform>(), viewX, viewY, viewW, viewH);
            viewport.AddComponent<RectMask2D>();
            GameObject contentObject = NewObject("InventoryGrid", viewport.transform);
            RectTransform grid = contentObject.AddComponent<RectTransform>();
            int rows = Mathf.Max(1, (items.Length + 4) / 5);
            PlaceTop(grid, 0, 0, viewW, rows * (tileH + gapY) + 10);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            for (int n = 0; n < items.Length; n++)
            {
                int item = items[n];
                Rect cell = new Rect(4 + (n % 5) * (tileW + gapX), 4 + (n / 5) * (tileH + gapY), tileW, tileH);
                InventoryTileView(grid, item, cell, () => OpenAccessoryCard(item, -1));
            }
            if (items.Length == 0)
            {
                // Empty: a big muted hanger + one 「去关卡」 button instead of a sentence.
                VisualB1101.NeonIcon(grid, "InventoryEmptyIcon", "hanger", new Color32(169, 156, 196, 160), viewW * .5f - 70, 150, 140);
                GameObject go = ModalPolish038Chrome.ActionButton(grid, "InventoryEmpty", "去关卡", 24, OpenLevelMap, ModalPolish038ButtonStyle.Primary);
                PlaceTop(go.GetComponent<RectTransform>(), viewW * .5f - 120, 320, 240, 72);
                VisualB1101.AddButtonIcon(go, "pin");
            }
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
}
}

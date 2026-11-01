using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (visual-b, 赵金 1101c): the accessory pages with pictures instead of sentences.
    /// The item card is a rarity-lit hero shot of the piece with level pips, a slot chip, the wearer's
    /// face, three stat glyphs and icon buttons; the wear picker and the 「饰品管理角色」 selector use the
    /// roster-ui large-card picker (ChoSirenAppMemberPicker1101). Faces are cropped with PortraitFace1101.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private static readonly string[] AccessoryHelpLines =
        {
            "饰品穿在成员身上，提升她的体力 / 攻击 / 防御",
            "每名成员 4 格：耳饰、项链、手饰、头饰，每格 1 件",
            "强化只花金币，最高 Lv.4",
            "关卡掉落新饰品；重复获得自动换成金币",
        };

        // ------------------------------------------------------------------ shared bits

        /// <summary>Torn-paper sticker avatar: the member's face (PortraitFace1101 Head crop) in a ring.</summary>
        private GameObject MemberHead1101(Transform parent, string name, int member, float x, float y, float size, Color ring)
        {
            RectTransform holder = VisualB1101.Rect(name, parent);
            PlaceTop(holder, x, y, size, size);
            MemberDefinition m = GameModel.Members[member];
            Image mask = VisualB1101.Img("Mask", holder, VisualB1101.TornBadge(), Color.white);
            PanelKit.Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            mask.color = new Color32(26, 16, 44, 255);
            Sprite art = Resources.Load<Sprite>(m.ResourcePath);
            Image face = VisualB1101.Img("Face", mask.transform, PortraitFace1101.CropSprite(m.Id, art, 1f, PortraitFace1101.Framing.Head), Color.white);
            PanelKit.Stretch(face.rectTransform);
            face.preserveAspect = true;
            Image rim = VisualB1101.Img("Ring", holder, VisualB1101.Ring(), ring);
            PanelKit.Stretch(rim.rectTransform, -2f, -2f, 2f, 2f);
            return holder.gameObject;
        }

        private static string StatIcon1101(string stat) => stat == "体力" ? "heart" : stat == "攻击" ? "sword" : "shield";

        // ------------------------------------------------------------------ item card

        private void OpenAccessoryCard1101(int item, int member, Action afterChange)
        {
            GameObject overlay = NewImage("AccessoryCard", safeRoot, null, new Color32(4, 2, 10, 210));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            RectTransform page = DetailPage(overlay, out float pageH);
            void Close() { if (overlay != null) { overlay.SetActive(false); Destroy(overlay); } }
            void Changed() { Close(); if (afterChange != null) afterChange(); else RefreshEquipmentCollection(); }

            bool owned = model.OwnsAccessory(item);
            int wearer = model.AccessoryOwner(item);
            int slot = Math.Max(0, GameModel.AccessorySlot(item));
            Color rarity = RarityColor(item);
            const float W = 620f, H = 900f;
            GameObject card = NewImage("Card", page, null, new Color32(14, 9, 24, 255));
            PlaceTop(card.GetComponent<RectTransform>(), 50, Mathf.Max(150f, pageH * .5f - H * .5f), W, H);
            card.GetComponent<Image>().raycastTarget = true;
            SirenStickerUi.SkinPanel(card);
            Transform c = card.transform;

            // ---- hero: rarity glow, slow ring, the piece itself
            Image glow = VisualB1101.Img("RarityGlow", c, VisualB1101.Glow(), new Color(rarity.r, rarity.g, rarity.b, owned ? .6f : .25f));
            PlaceTop(glow.rectTransform, W * .5f - 220f, 20f, 440f, 330f);
            Image halo = VisualB1101.Img("Halo", c, VisualB1101.Ring(), new Color(rarity.r, rarity.g, rarity.b, .35f));
            PlaceTop(halo.rectTransform, W * .5f - 135f, 50f, 270f, 270f);
            halo.rectTransform.pivot = new Vector2(.5f, .5f);
            halo.rectTransform.anchoredPosition += new Vector2(135f, -135f);
            VisualB1101.Spin(halo.rectTransform, 10f);
            Image art = VisualB1101.Img("ItemArt", c, AccessoryItemSprite(item), owned ? Color.white : new Color(.5f, .45f, .6f, .55f));
            art.preserveAspect = true;
            PlaceTop(art.rectTransform, W * .5f - 130f, 70f, 260f, 230f);
            VisualB1101.Pop(art.rectTransform, .05f, .7f);

            // rarity ribbon (top-left), 「?」 and close (top-right)
            RectTransform ribbon = VisualB1101.Chip(c, "TierChip", "star", GameModel.AccessoryRarityNameOf(item), rarity, 26, 26, 46,
                out Text tier, new Color32(10, 6, 20, 240), 24);
            tier.name = "Tier";
            tier.color = rarity;
            ribbon.localEulerAngles = new Vector3(0, 0, 4f);
            VisualB1101.HelpButton(c, "AccessoryHelp", W - 136f, 30f, 40f, "饰品说明", AccessoryHelpLines);
            GameObject close = ModalPolish038Chrome.ActionButton(c, "Close", string.Empty, 20, Close, ModalPolish038ButtonStyle.Ghost);
            PlaceTop(close.GetComponent<RectTransform>(), W - 80f, 22f, 60f, 54f);
            VisualB1101.AddButtonIcon(close, "close", Color.white);

            // level pips: filled = current level, the next one pulses when an upgrade is affordable
            int level = model.AccessoryUpgradeLevel(item);
            bool canUpgrade = model.CanUpgradeAccessory(item, out int cost);
            for (int i = 0; i < 4; i++)
            {
                bool filled = owned && i <= level;
                Image pip = VisualB1101.Img("LevelPip-" + i, c, PunkUi.SlantFill,
                    filled ? rarity : new Color(1f, 1f, 1f, .14f));
                PlaceTop(pip.rectTransform, W * .5f - 86f + i * 44f, 312f, 36f, 12f);
                if (canUpgrade && i == level + 1) { pip.color = VisualB1101.Teal; VisualB1101.Pulse(pip, .9f, .25f); }
            }

            Text name = VisualB1101.Label(c, "Name", GameModel.AccessoryNames[item], 34, InventoryInk, 0, 334, W, 52, TextAnchor.MiddleCenter, 2f);
            SirenStickerUi.SkinTitle(name);

            // slot (+ category when it says something else) and the wearer
            float chipsY = 398f;
            RectTransform slotChip = VisualB1101.Chip(c, "SlotChip", "hanger", GameModel.EquipmentSlotNames[slot], VisualB1101.Teal, 0, chipsY, 40, out _, null, 20);
            string category = GameModel.AccessoryCategory(item);
            RectTransform catChip = category != GameModel.EquipmentSlotNames[slot]
                ? VisualB1101.Chip(c, "CategoryChip", "sparkle", category, VisualB1101.Violet, 0, chipsY, 40, out _, null, 20) : null;
            RectTransform owner;
            if (wearer >= 0)
            {
                owner = VisualB1101.Rect("Owned", c);
                MemberHead1101(owner, "Head", wearer, 0, -6, 52, VisualB1101.Teal);
                Text who = VisualB1101.Label(owner, "Name", GameModel.Members[wearer].Name, 20, Color.white, 58, 0, 160, 40);
                PlaceTop(owner, 0, chipsY, 62 + who.preferredWidth, 40);
            }
            else owner = VisualB1101.Chip(c, "Owned", owned ? "check" : "lock", owned ? "空闲" : "未获得",
                owned ? VisualB1101.Muted : VisualB1101.Danger, 0, chipsY, 40, out _, null, 20);
            float rowW = slotChip.sizeDelta.x + (catChip != null ? catChip.sizeDelta.x + 12f : 0f) + 12f + owner.sizeDelta.x;
            float cx = (W - rowW) * .5f;
            slotChip.anchoredPosition = new Vector2(cx, -chipsY); cx += slotChip.sizeDelta.x + 12f;
            if (catChip != null) { catChip.anchoredPosition = new Vector2(cx, -chipsY); cx += catChip.sizeDelta.x + 12f; }
            owner.anchoredPosition = new Vector2(cx, -chipsY);

            // stats: glyph, value, two-character label
            var bonus = model.EffectiveAccessoryBonuses(item);
            var rows = new[] { ("体力", bonus.Hp), ("攻击", bonus.Attack), ("防御", bonus.Defense) }.Where(r => r.Item2 > 0).ToArray();
            GameObject statBox = NewImage("StatBox", c, PunkUi.SlantFill, new Color32(26, 16, 44, 235));
            PlaceTop(statBox.GetComponent<RectTransform>(), 40, 466, W - 80, 150);
            float cellW = (W - 80f) / Mathf.Max(1, rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                float x0 = i * cellW;
                Color tint = AttributeColors[rows[i].Item1 == "体力" ? 0 : rows[i].Item1 == "攻击" ? 1 : 2];
                VisualB1101.NeonIcon(statBox.transform, "Glyph-" + i, StatIcon1101(rows[i].Item1), tint, x0 + cellW * .5f - 26f, 14, 52);
                VisualB1101.Label(statBox.transform, "Value-" + i, $"+{rows[i].Item2 / 10f:0.#}%", 30, new Color32(255, 196, 72, 255),
                    x0, 70, cellW, 42, TextAnchor.MiddleCenter);
                VisualB1101.Label(statBox.transform, "Name-" + i, rows[i].Item1, 17, VisualB1101.Muted, x0, 110, cellW, 28, TextAnchor.MiddleCenter, 1f, FontStyle.Normal);
            }

            // where it drops (tap → the chapter map)
            string source = GameModel.AccessorySource(item).Replace("初始赠送 / ", string.Empty);
            GameObject sourceChip = VisualB1101.Chip(c, "SourceChip", "pin", source, VisualB1101.Pink, 0, 636, 40, out Text sourceText, null, 19).gameObject;
            PanelKit.EnableBestFit(sourceText, 12);
            RectTransform sourceRect = (RectTransform)sourceChip.transform;
            sourceRect.sizeDelta = new Vector2(Mathf.Min(sourceRect.sizeDelta.x, W - 80f), 40f);
            sourceRect.anchoredPosition = new Vector2((W - sourceRect.sizeDelta.x) * .5f, -636f);
            VisualB1101.MakeButton(sourceChip, () => { Close(); OpenLevelMap(); });

            // upgrade: coin + cost (the pulsing pip above shows the level it buys)
            GameObject upgrade = ModalPolish038Chrome.ActionButton(c, "Upgrade",
                !owned ? "—" : level >= 3 ? "满级" : cost.ToString("N0"), 26, () =>
                {
                    bool ok = model.UpgradeAccessory(item, out string message);
                    if (!ok) { Toast(message); return; }
                    Changed(); OpenAccessoryCard(item, member, afterChange);
                    UpgradeFeedback1022(item, ok, message);
                }, ModalPolish038ButtonStyle.Secondary);
            PlaceTop(upgrade.GetComponent<RectTransform>(), W * .5f - 140f, 700, 280, 68);
            ModalPolish038Chrome.SetActionEnabled(upgrade, canUpgrade);
            VisualB1101.AddButtonIcon(upgrade, level >= 3 && owned ? "crown" : "up", canUpgrade ? VisualB1101.Teal : VisualB1101.Muted);
            if (owned && level < 3)
            {
                Image coin = VisualB1101.Img("Coin", upgrade.transform.Find("Body"), PanelKit.CurrencyIcon("gold"), Color.white);
                coin.preserveAspect = true;
                PlaceTop(coin.rectTransform, 214, 16, 36, 36);
            }

            // bottom pair: 解除 / 交换 (member context) or 卸下 / 穿戴 (inventory)
            string leftLabel, rightLabel, leftIcon, rightIcon;
            UnityEngine.Events.UnityAction left = null, right;
            if (member >= 0)
            {
                bool mine = wearer == member;
                leftLabel = "解除"; leftIcon = "unlink";
                left = mine ? () => { model.EquipAccessoryForMember(member, item, out string m); Toast(m); Changed(); } : null;
                rightLabel = "交换"; rightIcon = "exchange";
                right = () => { Close(); OpenAccessorySwap(member, slot, afterChange); };
            }
            else
            {
                leftLabel = "卸下"; leftIcon = "unlink";
                left = wearer >= 0 ? () => { model.EquipAccessoryForMember(wearer, item, out string m); Toast(m); Changed(); } : null;
                rightLabel = owned ? "穿戴" : "去关卡"; rightIcon = owned ? "hanger" : "pin";
                right = owned ? () => { Close(); OpenAccessoryMemberPicker(item); } : (UnityEngine.Events.UnityAction)(() => { Close(); OpenLevelMap(); });
            }
            GameObject leftButton = ModalPolish038Chrome.ActionButton(c, "CardLeft", leftLabel, 28, left ?? (() => { }), ModalPolish038ButtonStyle.Secondary);
            PlaceTop(leftButton.GetComponent<RectTransform>(), 30, 792, 270, 84);
            ModalPolish038Chrome.SetActionEnabled(leftButton, left != null);
            VisualB1101.AddButtonIcon(leftButton, leftIcon);
            GameObject rightButton = ModalPolish038Chrome.ActionButton(c, "CardRight", rightLabel, 28, right, ModalPolish038ButtonStyle.Primary);
            PlaceTop(rightButton.GetComponent<RectTransform>(), W - 300, 792, 270, 84);
            VisualB1101.AddButtonIcon(rightButton, rightIcon);
            VisualB1101.Pop((RectTransform)card.transform, 0f, .92f);
        }

        // ------------------------------------------------------------------ pickers

        /// <summary>穿戴: the roster-ui large-card grid; tapping a member equips the item to her.</summary>
        private void OpenAccessoryWearPicker1101(int item)
        {
            CloseModal();
            RectTransform host = (RectTransform)safeRoot;
            float hostW = host.rect.width > 1f ? host.rect.width : 720f;
            float hostH = host.rect.height > 1f ? host.rect.height : 1440f;
            float panelW = Mathf.Min(704f, hostW - 16f);
            float panelH = Mathf.Max(760f, hostH - 48f);
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "AccessoryMemberPicker",
                GameModel.AccessoryNames[item], "穿戴", CloseModal, panelW, closeName: "CloseProgression");
            modalObject = frame.Overlay;
            PickerFrame1101(frame, panelW, panelH);
            Image itemArt = VisualB1101.Img("PickerItemArt", frame.Panel, AccessoryItemSprite(item), Color.white);
            itemArt.preserveAspect = true;
            PlaceTop(itemArt.rectTransform, panelW - PickerInset1101 - 150f, 26f, 76f, 76f);
            Text title = frame.Panel.Find("Title")?.GetComponent<Text>();
            if (title != null) title.rectTransform.sizeDelta = new Vector2(panelW - PickerInset1101 * 2f - 190f, title.rectTransform.sizeDelta.y);

            float width = panelW - PickerInset1101 * 2f;
            float bodyH = panelH - PickerHeader1101 - PickerInset1101;
            int slot = Math.Max(0, GameModel.AccessorySlot(item));
            int[] members = model.Save.UnlockedMembers.Distinct().OrderByDescending(model.IsInTeam).ThenByDescending(model.PowerOf).ToArray();
            RectTransform content = PickerScroll1101(frame.Body, 0f, width, bodyH);
            const int columns = 2;
            float cardW = (width - PickerGap1101) / columns, cardH = PickerCardHeight1101;
            for (int i = 0; i < members.Length; i++)
            {
                int member = members[i];
                bool wearing = model.AccessoryOwner(item) == member;
                int worn = model.EquippedInSlot(member, slot);
                Rect cell = new Rect((i % columns) * (cardW + PickerGap1101), (i / columns) * (cardH + PickerGap1101), cardW, cardH);
                GameObject card = PickerCard1101(content, member, cell, "wear", slot, false, () =>
                {
                    if (model.AccessoryOwner(item) == member) return;
                    model.EquipAccessoryForMember(member, item, out string message);
                    Toast(message);
                    VisualB1101.Sfx("shield", .6f);
                    CloseModal(); RefreshEquipmentCollection();
                });
                card.name = "Member-" + GameModel.Members[member].Id;
                card.GetComponent<Button>().interactable = !wearing;
                Transform body = card.transform.Find("Body");
                Text state = body != null ? body.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "PickerState-" + member) : null;
                if (state != null) state.text = wearing ? "已穿戴" : worn >= 0 ? "替换" : "穿戴";
                // What she wears in that slot now: the piece's own art in the card's corner.
                if (body != null && worn >= 0)
                {
                    GameObject now = NewImage("PickerWorn", body, PunkUi.SlantFill, new Color32(12, 6, 24, 235));
                    PlaceTop(now.GetComponent<RectTransform>(), cardW - 86f, cardH - 150f, 72f, 72f);
                    Outline edge = now.AddComponent<Outline>();
                    edge.effectColor = RarityColor(worn); edge.effectDistance = new Vector2(2f, -2f);
                    GameObject art = NewImage("Art", now.transform, AccessoryItemSprite(worn), Color.white);
                    Stretch(art.GetComponent<RectTransform>(), 6, 6, -6, -6);
                    art.GetComponent<Image>().preserveAspect = true;
                }
                if (wearing && body != null) VisualB1101.NeonIcon(body, "PickerWearing", "check", VisualB1101.Teal, cardW - 74f, cardH - 140f, 52f);
            }
            int rows = (members.Length + columns - 1) / columns;
            content.sizeDelta = new Vector2(width, rows * cardH + (rows - 1) * PickerGap1101 + 24f);
        }

        /// <summary>饰品管理角色: the shared large-card member picker with the explanation behind 「?」.</summary>
        private void OpenEquipmentMemberSelector1101(bool ownedOnly, string query)
        {
            OpenMemberSelection("equipment", 0, 0, ownedOnly, query);
            Transform help = modalObject != null ? modalObject.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PickerHelp") : null;
            if (help == null) return;
            help.GetComponent<Text>().text = string.Empty;
            RectTransform rect = (RectTransform)help;
            VisualB1101.HelpButton(help.parent, "PickerHelpButton", rect.rect.width - 40f, 0f, 32f, "管理角色",
                "只切换「饰品」页正在管理的成员", "不会改变编队，也不会转移饰品", "未签约成员只能查看档案");
        }
    }
}

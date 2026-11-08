using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Shared reward art for map previews, result cards and personal equipment.</summary>
    public static class RewardItemVisuals
    {
        private static readonly string[] AccessoryPaths =
        {
            "Art/AccessoryAI/Items/accessory-ear-monitor-ai-v1",
            "Art/AccessoryAI/Items/accessory-heart-necklace-ai-v1",
            "Art/AccessoryAI/Items/accessory-dance-boots-ai-v1",
            "Art/AccessoryAI/Items/accessory-microphone-charm-ai-v1",
            "Art/AccessoryAI/Items/accessory-star-bracelet-ai-v1",
            "Art/AccessoryAI/Items/accessory-stage-crown-ai-v1"
        };
        private static Sprite fragmentIcon, ticketIcon;

        /// <summary>The HUD currency badge ("diamond" / "gold" / "stamina"), from Art/Reward1101.</summary>
        public static Sprite HudBadge(string currency) => Resources.Load<Sprite>("Art/Reward1101/hud-" + currency + "-1101");

        public static Sprite SpriteFor(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 12) return Resources.Load<Sprite>(GameModel.AccessoryCollectionResourcePath(accessory));
            if (accessory >= 0) return Resources.Load<Sprite>(AccessoryPaths[accessory % AccessoryPaths.Length]);
            switch (itemId)
            {
                // 2026-11-01 reward-ui: currencies use the top-HUD badges (cut from the HUD pill art itself), so
                // sign-in, mail, popups, result cards and the HUD read as one icon set.
                case CurrencyIds.Gold: return HudBadge("gold") ?? Resources.Load<Sprite>("Art/UI/ResourceGold-C");
                case CurrencyIds.Diamond: return HudBadge("diamond") ?? Resources.Load<Sprite>("Art/UI/ResourceDiamond-C");
                case CurrencyIds.Stamina: return HudBadge("stamina") ?? Resources.Load<Sprite>("Art/UI/ResourceStamina-C");
                case GameModel.EquipmentFragmentItemId:
                    return fragmentIcon != null ? fragmentIcon : fragmentIcon = BuildSymbol(false);
                case CurrencyIds.RecruitTicket:
                    return Resources.Load<Sprite>("Art/Reward1101/ticket-recruit-1101") ?? (ticketIcon != null ? ticketIcon : ticketIcon = BuildSymbol(true));
                case CurrencyIds.CostumeTicket:
                    return Resources.Load<Sprite>("Art/Reward1101/ticket-costume-1101") ?? (ticketIcon != null ? ticketIcon : ticketIcon = BuildSymbol(true));
                // 2026-11-07: 敌人残魂 / 通用生魂 / race 生魂 icons are GPT stand-ins until 赵金's art lands.
                case CurrencyIds.VoiceCrystal: return Resources.Load<Sprite>("Art/Reward1101/voice-crystal-1107") ?? Resources.Load<Sprite>("Art/Reward1101/material-crystal-1104");
                case CurrencyIds.VoiceCore: return Resources.Load<Sprite>("Art/Reward1101/voice-core-1107") ?? Resources.Load<Sprite>("Art/Reward1101/material-core-1104");
                case CurrencyIds.SoulCharm: case CurrencyIds.SoulDemon: case CurrencyIds.SoulSea: case CurrencyIds.SoulBlood:
                    return Resources.Load<Sprite>("Art/Reward1101/" + itemId + "-1107") ?? Resources.Load<Sprite>("Art/Reward1101/material-core-1104");
                default: return null;
            }
        }

        /// <summary>Tier colour for the dark neon UI, matching
        /// <see cref="GameModel.AccessoryRarityColorHex"/> so text and art never disagree.</summary>
        public static Color RarityColor(GameModel.AccessoryRarity rarity) => rarity switch
        {
            GameModel.AccessoryRarity.Legendary => new Color32(255, 176, 32, 255),
            GameModel.AccessoryRarity.Epic => new Color32(196, 104, 255, 255),
            GameModel.AccessoryRarity.Rare => new Color32(74, 176, 255, 255),
            GameModel.AccessoryRarity.Fine => new Color32(74, 224, 140, 255),
            _ => new Color32(184, 190, 205, 255),
        };

        /// <summary>Rarity accent for the item id, or the currency's own identity colour.
        /// Accessories are keyed on rarity rather than category: the old category tint made
        /// every tier read the same, so players could not tell a 普通 drop from a 传说 one.</summary>
        public static Color AccentFor(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 0) return RarityColor(GameModel.AccessoryRarityOf(accessory));
            return itemId == CurrencyIds.Gold || itemId == CurrencyIds.RecruitTicket
                ? new Color32(255, 213, 123, 255) : new Color32(103, 222, 255, 255);
        }

        /// <summary>True for the white code-drawn fallback glyphs, which are tinted with <see cref="AccentFor"/>.</summary>
        public static bool IsCodeSymbol(Sprite sprite) => sprite != null && sprite.texture != null &&
            (sprite.texture.name == "RecruitTicketSymbol" || sprite.texture.name == "EquipmentFragmentSymbol");

        public static Image CreateIcon(Transform parent, string itemId, string name, float x, float y, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = Vector2.one * size;
            Image image = obj.GetComponent<Image>();
            image.sprite = SpriteFor(itemId);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = IsCodeSymbol(image.sprite) ? AccentFor(itemId) : Color.white;
            return image;
        }

        private static Sprite BuildSymbol(bool ticket)
        {
            // Small code-native symbols; existing item/character artwork stays untouched.
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = ticket ? "RecruitTicketSymbol" : "EquipmentFragmentSymbol", filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool filled;
                if (ticket)
                {
                    filled = x >= 8 && x <= 55 && y >= 15 && y <= 48;
                    if ((x - 8) * (x - 8) + (y - 32) * (y - 32) < 30 ||
                        (x - 55) * (x - 55) + (y - 32) * (y - 32) < 30) filled = false;
                    if (x >= 19 && x <= 44 && (y == 23 || y == 24 || y == 40 || y == 41)) filled = false;
                    if ((x == 45 || x == 46) && y > 19 && y < 44 && y % 6 < 3) filled = false;
                }
                else
                {
                    float dx = Mathf.Abs(x - 29) / 20f, dy = Mathf.Abs(y - 34) / 27f;
                    filled = dx + dy < 1f || (Mathf.Abs(x - 48) / 10f + Mathf.Abs(y - 18) / 14f < 1f);
                    if (Mathf.Abs(x - 29) <= 1 && y > 11 && y < 51) filled = false;
                }
                pixels[y * size + x] = filled ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
        }
    }
}

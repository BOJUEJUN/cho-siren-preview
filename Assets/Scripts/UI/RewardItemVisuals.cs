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

        public static Sprite SpriteFor(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 12) return Resources.Load<Sprite>(GameModel.AccessoryCollectionResourcePath(accessory));
            if (accessory >= 0) return Resources.Load<Sprite>(AccessoryPaths[accessory % AccessoryPaths.Length]);
            switch (itemId)
            {
                case CurrencyIds.Gold: return Resources.Load<Sprite>("Art/UI/ResourceGold-C");
                case CurrencyIds.Diamond: return Resources.Load<Sprite>("Art/UI/ResourceDiamond-C");
                case CurrencyIds.Stamina: return Resources.Load<Sprite>("Art/UI/ResourceStamina-C");
                case GameModel.EquipmentFragmentItemId:
                    return fragmentIcon != null ? fragmentIcon : fragmentIcon = BuildSymbol(false);
                case CurrencyIds.RecruitTicket:
                case CurrencyIds.CostumeTicket:
                    return ticketIcon != null ? ticketIcon : ticketIcon = BuildSymbol(true);
                default: return null;
            }
        }

        public static Color AccentFor(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 12)
            {
                Color32[] categories = { new Color32(190, 143, 255, 255), new Color32(250, 145, 192, 255),
                    new Color32(117, 219, 241, 255), new Color32(251, 210, 121, 255),
                    new Color32(145, 223, 172, 255), new Color32(242, 175, 132, 255) };
                return categories[(accessory - 12) / 9];
            }
            if (accessory >= 6) return new Color32(255, 211, 117, 255);
            if (accessory >= 0) return new Color32(203, 148, 255, 255);
            return itemId == CurrencyIds.Gold || itemId == CurrencyIds.RecruitTicket
                ? new Color32(255, 213, 123, 255) : new Color32(103, 222, 255, 255);
        }

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
            image.color = GameModel.AccessoryIndexForItem(itemId) >= 0 || itemId == CurrencyIds.Gold ||
                itemId == CurrencyIds.Diamond || itemId == CurrencyIds.Stamina ? Color.white : AccentFor(itemId);
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

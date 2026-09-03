using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Shared runtime-UI toolbox for the Panels folder. Mirrors the helper names used by
    /// LevelMapPanel / PerformanceStagePanel / StoryBattlePanel (NewImage, NewPanel, NewButton,
    /// NewPlacedText, PlaceTop, Stretch, RoundedSprite, ...) so the four new panels read the
    /// same way as the existing ones, but keeps a single copy of the texture bookkeeping.
    /// Every generated Texture/Sprite is tracked and destroyed by <see cref="Dispose"/>.
    /// </summary>
    internal sealed class PanelKit
    {
        public static readonly Color White = new Color32(249, 247, 255, 255);
        public static readonly Color Muted = new Color32(196, 190, 220, 255);
        public static readonly Color Pink = new Color32(255, 82, 194, 255);
        public static readonly Color Cyan = new Color32(80, 220, 255, 255);
        public static readonly Color Purple = new Color32(145, 83, 224, 255);
        public static readonly Color Gold = new Color32(255, 205, 96, 255);
        public static readonly Color Glass = new Color32(25, 20, 72, 242);
        public static readonly Color HeaderGlass = new Color32(5, 10, 38, 238);
        public static readonly Color ButtonDark = new Color32(70, 46, 118, 242);
        public static readonly Color Disabled = new Color32(91, 83, 125, 245);

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();
        private readonly string prefix;
        private Sprite radialSprite;

        public PanelKit(string texturePrefix)
        {
            prefix = string.IsNullOrEmpty(texturePrefix) ? "Panel" : texturePrefix;
            Font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
        }

        public Font Font { get; }
        public GameAudio Audio { get; }

        /// <summary>Creates the stretched full-screen root every panel lives in.</summary>
        public static GameObject CreateOverlayRoot(string name, Transform host)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(host, false);
            Stretch(panelObject.GetComponent<RectTransform>());
            CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            return panelObject;
        }

        public void PlayClick()
        {
            if (Audio != null) Audio.PlayClick();
        }

        public void PlaySuccess()
        {
            if (Audio != null) Audio.PlaySuccess();
        }

        public void Dispose()
        {
            for (int index = 0; index < generatedAssets.Count; index++)
            {
                UnityEngine.Object asset = generatedAssets[index];
                if (asset == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
                else UnityEngine.Object.DestroyImmediate(asset);
            }

            generatedAssets.Clear();
            roundedSprites.Clear();
            radialSprite = null;
        }

        // ---------------------------------------------------------------- sprites

        public Sprite RadialSprite()
        {
            if (radialSprite != null) return radialSprite;
            const int size = 128;
            Texture2D texture = NewTexture(prefix + "-Radial", size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalized), 1.8f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            radialSprite = NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, Vector4.zero);
            return radialSprite;
        }

        public Sprite CreateGradientSprite(string name, Color32 top, Color32 middle, Color32 bottom)
        {
            const int width = 4;
            const int height = 256;
            Texture2D texture = NewTexture(prefix + "-" + name, width, height);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float fromBottom = y / (height - 1f);
                Color color = fromBottom < 0.5f
                    ? Color.Lerp(bottom, middle, fromBottom * 2f)
                    : Color.Lerp(middle, top, (fromBottom - 0.5f) * 2f);
                Color32 pixel = color;
                for (int x = 0; x < width; x++) pixels[y * width + x] = pixel;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, Vector4.zero);
        }

        public Sprite RoundedSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 4, 32);
            if (roundedSprites.TryGetValue(radius, out Sprite cached)) return cached;

            const int size = 64;
            Texture2D texture = NewTexture($"{prefix}-Rounded-{radius}", size, size);
            Color32[] pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, r, size - r);
                    float nearestY = Mathf.Clamp(y + 0.5f, r, size - r);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                        new Vector2(nearestX, nearestY));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                new Vector4(radius, radius, radius, radius));
            roundedSprites[radius] = sprite;
            return sprite;
        }

        public Texture2D NewTexture(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            generatedAssets.Add(texture);
            return texture;
        }

        public Sprite NewSprite(Texture2D texture, Rect rect, Vector2 pivot, Vector4 border)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name + "-Sprite";
            sprite.hideFlags = HideFlags.DontSave;
            generatedAssets.Add(sprite);
            return sprite;
        }

        // ---------------------------------------------------------------- objects

        public GameObject NewObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        public RectTransform NewRect(string name, Transform parent)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            return rect;
        }

        public Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public GameObject NewPanel(string name, Transform parent, Color color, int radius)
        {
            Image image = NewImage(name, parent, RoundedSprite(radius), color);
            image.type = Image.Type.Sliced;
            return image.gameObject;
        }

        public Outline AddOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
            return outline;
        }

        public GameObject NewButton(string name, Transform parent, string label, int fontSize, Color background,
            Color foreground, UnityAction action, int radius)
        {
            GameObject result = NewPanel(name, parent, background, radius);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.62f, 0.62f, 0.7f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                PlayClick();
                action?.Invoke();
            });

            Text text = NewText("Label", result.transform, label, fontSize, foreground, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            return result;
        }

        public static Text LabelOf(GameObject button)
        {
            Transform label = button.transform.Find("Label");
            return label != null ? label.GetComponent<Text>() : null;
        }

        public static void SetButtonState(GameObject button, bool interactable, Color background)
        {
            Button component = button.GetComponent<Button>();
            if (component != null) component.interactable = interactable;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = background;
        }

        public Text NewText(string name, Transform parent, string value, int size, Color color, FontStyle style,
            TextAnchor alignment)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = Mathf.Max(12, size);
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        public Text NewPlacedText(Transform parent, string value, int size, Color color, float x, float y,
            float width, float height, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            Text text = NewText("Text", parent, value, size, color, style, alignment);
            PlaceTop(text.rectTransform, x, y, width, height);
            return text;
        }

        /// <summary>Track + fill bar. Returns the fill image (Image.Type.Filled, horizontal).</summary>
        public Image NewBar(string name, Transform parent, float x, float y, float width, float height,
            Color track, Color fill, int radius)
        {
            Image trackImage = NewImage(name, parent, RoundedSprite(radius), track);
            trackImage.type = Image.Type.Sliced;
            PlaceTop(trackImage.rectTransform, x, y, width, height);
            Image fillImage = NewImage("Fill", trackImage.transform, RoundedSprite(radius), fill);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            Stretch(fillImage.rectTransform);
            return fillImage;
        }

        /// <summary>Standard 112 px header with a Back button and title stack.</summary>
        public GameObject NewHeader(Transform parent, string eyebrow, string title, UnityAction back,
            string backName = "Back")
        {
            Image header = NewImage("Header", parent, null, HeaderGlass);
            PlaceTop(header.rectTransform, 0, 0, 720, 112);
            header.raycastTarget = true;

            GameObject backButton = NewButton(backName, header.transform, "返回", 17, ButtonDark, White, back, 18);
            PlaceTop(backButton.GetComponent<RectTransform>(), 18, 27, 88, 56);

            NewPlacedText(header.transform, eyebrow, 13, new Color32(255, 173, 226, 255),
                128, 17, 300, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, title, 27, White,
                128, 40, 300, 42, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image divider = NewImage("Divider", header.transform, null, new Color32(152, 105, 226, 88));
            PlaceTop(divider.rectTransform, 18, 108, 684, 2);
            return header.gameObject;
        }

        /// <summary>Night-city gradient plus two neon glows; the base image blocks raycasts.</summary>
        public void BuildBackdrop(Transform parent)
        {
            Image gradient = NewImage("NightGradient", parent, CreateGradientSprite("Gradient",
                new Color32(7, 8, 35, 255), new Color32(31, 14, 77, 255), new Color32(8, 24, 65, 255)), White);
            Stretch(gradient.rectTransform);
            gradient.raycastTarget = true;

            Image topGlow = NewImage("SkyGlow", parent, RadialSprite(), new Color32(94, 49, 204, 95));
            PlaceTop(topGlow.rectTransform, 40, 20, 640, 420);
            Image bottomGlow = NewImage("CityGlow", parent, RadialSprite(), new Color32(255, 48, 194, 50));
            PlaceTop(bottomGlow.rectTransform, -40, 900, 800, 700);
        }

        // ---------------------------------------------------------------- layout

        public static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
        }

        public static void Stretch(RectTransform rect, float left = 0, float bottom = 0,
            float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public static void PlaceCentered(RectTransform rect, float width, float height, float offsetY = 0f)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(0, offsetY);
            rect.sizeDelta = new Vector2(width, height);
        }

        // ---------------------------------------------------------------- text helpers

        public static string CurrencyName(string currencyId)
        {
            switch (currencyId)
            {
                case "diamond": return "星钻";
                case "gold": return "金币";
                case "stamina": return "体力";
                case "recruit-ticket": return "招募券";
                case "costume-ticket": return "服装券";
                case "shard": return "碎片";
                default: return "道具";
            }
        }

        public static Color CurrencyColor(string currencyId)
        {
            switch (currencyId)
            {
                case "diamond": return Cyan;
                case "gold": return Gold;
                case "stamina": return new Color32(120, 255, 170, 255);
                case "recruit-ticket": return Pink;
                case "costume-ticket": return new Color32(255, 150, 230, 255);
                case "shard": return new Color32(190, 160, 255, 255);
                default: return Muted;
            }
        }

        /// <summary>Resource icon for a currency; null when the project has no matching art.</summary>
        public static Sprite CurrencyIcon(string currencyId)
        {
            switch (currencyId)
            {
                case "diamond": return Resources.Load<Sprite>("Art/UI/ResourceDiamond-C");
                case "gold": return Resources.Load<Sprite>("Art/UI/ResourceGold-C");
                case "stamina": return Resources.Load<Sprite>("Art/UI/ResourceStamina-C");
                default: return null;
            }
        }

        /// <summary>Best-effort Chinese name for an item/character id using the existing roster.</summary>
        public static string MemberNameOrId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "未知";
            MemberDefinition[] members = GameModel.Members;
            for (int index = 0; index < members.Length; index++)
            {
                if (members[index] != null && members[index].Id == itemId) return members[index].Name;
            }

            return "未知";
        }

        /// <summary>Portrait lookup that tolerates both legacy and catalog resource layouts.</summary>
        public static Sprite MemberSpriteOrNull(string itemId, bool thumbnail)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            MemberDefinition[] members = GameModel.Members;
            for (int index = 0; index < members.Length; index++)
            {
                MemberDefinition member = members[index];
                if (member == null || member.Id != itemId) continue;
                string path = thumbnail ? member.ThumbnailResourcePath : member.ResourcePath;
                Sprite sprite = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
                if (sprite != null) return sprite;
            }

            Sprite legacy = Resources.Load<Sprite>("Art/Members/member-" + itemId);
            if (legacy != null) return legacy;
            return Resources.Load<Sprite>("Art/Members/" + itemId + (thumbnail ? "/thumb" : "/portrait"));
        }

        /// <summary>30 → "3.0%", 185 → "18.5%". Always one decimal so rate tables line up.</summary>
        public static string Permille(int permille)
        {
            return (permille / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
    }
}

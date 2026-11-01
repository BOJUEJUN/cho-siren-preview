using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>A short click-through hand cue on the dice readout, without covering the battlefield.</summary>
    public sealed class DiceComboShow : MonoBehaviour
    {
        private const float Duration = .85f;
        private const float StampDuration = 1.15f;
        private RectTransform stamp;
        private bool stampMode;
        private static readonly Dictionary<string, string> StampKeys = new Dictionary<string, string>
        {
            { "一对", "pair" }, { "两对", "twopair" }, { "三条", "threekind" }, { "三对", "threepair" },
            { "顺子", "straight" }, { "葫芦", "fullhouse" }, { "四条", "fourkind" }, { "双三条", "doublethree" },
            { "六连顺", "sixstraight" }, { "五条", "fivekind" }, { "六同", "sixkind" },
        };
        private static readonly Dictionary<string, Sprite> stampSprites = new Dictionary<string, Sprite>();
        /// <summary>GPT word-art stamp for a hand (owner 2026-10-20: 顺子-style letters, no backing).</summary>
        public static Sprite StampFor(string comboName)
        {
            if (string.IsNullOrEmpty(comboName)) return null;
            string key = null;
            foreach (var pair in StampKeys) if (comboName.StartsWith(pair.Key) && (key == null || pair.Key.Length > key.Length)) key = pair.Key;
            if (key == null) return null;
            if (stampSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Texture2D texture = Resources.Load<Texture2D>("UI/DiceStamps/" + StampKeys[key]);
            if (texture == null) return null;
            return stampSprites[key] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        }
        private RectTransform layer, root;
        private Func<bool> paused;
        private Func<float> delta;
        private CanvasGroup group;
        private float elapsed;
        private bool playing;

        public bool IsPlaying => playing;

        /// <summary>Hand strength remains independent of how much presentation it receives.</summary>
        public static int TierOf(int pattern)
        {
            switch (pattern)
            {
                case 0: return 0;
                case 1: case 2: return 1;
                case 3: case 4: case 8: return 2;
                case 5: case 6: case 9: return 3;
                default: return 4;
            }
        }

        public static DiceComboShow Attach(RectTransform layer, Func<bool> isPaused, Func<float> animationDelta)
        {
            DiceComboShow show = UnityNull.Real(layer.GetComponent<DiceComboShow>()) ?? layer.gameObject.AddComponent<DiceComboShow>();
            show.layer = layer;
            show.paused = isPaused;
            show.delta = animationDelta;
            return show;
        }

        public void Play(string comboName, float multiplierValue, int comboTier, Font font,
            IList<RectTransform> fromDice, IList<RectTransform> targets, Action<int> arrive,
            RectTransform readoutAnchor = null)
        {
            Cancel();
            // No travelling shards remain. Apply each card's presentational number release
            // immediately, including weak/reduced-motion hands, so Hold can never be stranded.
            if (targets != null) for (int i = 0; i < targets.Count; i++) arrive?.Invoke(i);
            if (BattlePresentationOptions.ReduceMotion || layer == null) return;
            Sprite art = comboTier >= 1 ? StampFor(comboName) : null;
            if (art != null) { PlayStamp(art, multiplierValue, comboTier, font, fromDice); return; }
            if (comboTier <= 1) return;

            Rect bounds = ReadoutBounds(readoutAnchor, fromDice);
            float maxWidth = Mathf.Max(120f, layer.rect.width - 32f);
            float width = Mathf.Clamp(bounds.width, Mathf.Min(180f, maxWidth), Mathf.Min(520f, maxWidth));
            float height = Mathf.Clamp(bounds.height, 32f, 48f);
            Vector2 center = bounds.center;
            center.x = Mathf.Clamp(center.x, -layer.rect.width * .5f + width * .5f + 8f,
                layer.rect.width * .5f - width * .5f - 8f);
            center.y = Mathf.Clamp(center.y, -layer.rect.height * .5f + height * .5f + 4f,
                layer.rect.height * .5f - height * .5f - 4f);

            root = NewRect("DiceComboShow", layer);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = center;
            root.sizeDelta = new Vector2(width, height);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            group.alpha = 0f;
            root.SetAsLastSibling();

            Image border = NewImage("CompactEdge", root, PunkUi.SlantFill,
                new Color32(107, 72, 211, 255));
            Stretch(border.rectTransform, 0f);
            Image face = NewImage("CompactFace", root, PunkUi.SlantFill,
                new Color32(9, 5, 25, 255));
            Stretch(face.rectTransform, 2f);
            Text title = NewText("Title", root, comboName + $" ×{multiplierValue:0.##}", font);
            Stretch(title.rectTransform, 4f);
            title.fontSize = 26;
            title.resizeTextForBestFit = true;
            title.resizeTextMaxSize = 26;
            title.resizeTextMinSize = 18;
            elapsed = 0f;
            playing = true;
            Advance(0f);
            if (paused?.Invoke() ?? false) root.gameObject.SetActive(false);
        }

        /// <summary>The hand's word-art slams down over the dice row: pop, wiggle, hold, fade.</summary>
        private void PlayStamp(Sprite art, float multiplierValue, int tier, Font font, IList<RectTransform> dice)
        {
            Vector2 center = new Vector2(0f, -layer.rect.height * .3f);
            if (dice != null && dice.Count > 0)
            {
                Vector2 sum = Vector2.zero; int count = 0;
                foreach (RectTransform die in dice)
                    if (die != null) { sum += (Vector2)layer.InverseTransformPoint(die.TransformPoint(die.rect.center)) - layer.rect.center; count++; }
                if (count > 0) center = sum / count + new Vector2(0f, 120f);
            }
            float width = Mathf.Min(layer.rect.width - 24f, 300f + 40f * Mathf.Clamp(tier, 1, 4));
            float height = width * art.rect.height / Mathf.Max(1f, art.rect.width);
            root = NewRect("DiceComboStamp", layer);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = center;
            root.sizeDelta = new Vector2(width, height);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            group.alpha = 0f;
            root.SetAsLastSibling();
            Image image = NewImage("Stamp", root, art, Color.white);
            image.preserveAspect = true;
            Stretch(image.rectTransform, 0f);
            Text mult = NewText("Multiplier", root, $"×{multiplierValue:0.##}", font);
            mult.fontSize = 30; mult.fontStyle = FontStyle.BoldAndItalic;
            mult.alignment = TextAnchor.MiddleRight;
            mult.color = tier >= 3 ? new Color32(255, 214, 92, 255) : new Color32(255, 95, 180, 255);
            var shadow = mult.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2f, -2f);
            mult.rectTransform.anchorMin = new Vector2(.5f, 0f); mult.rectTransform.anchorMax = new Vector2(1f, 0f);
            mult.rectTransform.pivot = new Vector2(1f, 1f);
            mult.rectTransform.anchoredPosition = new Vector2(-8f, 10f); mult.rectTransform.sizeDelta = new Vector2(0f, 40f);
            stamp = root; stampMode = true;
            elapsed = 0f;
            playing = true;
            Advance(0f);
            if (paused?.Invoke() ?? false) root.gameObject.SetActive(false);
        }

        private Rect ReadoutBounds(RectTransform anchor, IList<RectTransform> dice)
        {
            if (anchor != null)
            {
                var corners = new Vector3[4];
                anchor.GetWorldCorners(corners);
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = (Vector2)layer.InverseTransformPoint(corner) - layer.rect.center;
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            // Standalone presentation fixtures may have no hand label. Use the dice's own
            // local row, never the middle of the stage; production always supplies the label.
            Vector2 center = new Vector2(0f, -layer.rect.height * .35f);
            if (dice != null && dice.Count > 0)
            {
                center = Vector2.zero;
                int count = 0;
                foreach (RectTransform die in dice)
                    if (die != null)
                    {
                        center += (Vector2)layer.InverseTransformPoint(die.TransformPoint(die.rect.center)) - layer.rect.center;
                        count++;
                    }
                if (count > 0) center = center / count + new Vector2(0f, 64f);
            }
            return new Rect(center - new Vector2(180f, 22f), new Vector2(360f, 44f));
        }

        public void Cancel()
        {
            playing = false;
            stampMode = false; stamp = null;
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = null;
            group = null;
        }

        private void OnDisable() => Cancel();
        private void Update()
        {
            if (!playing) return;
            bool stopped = paused?.Invoke() ?? false;
            if (root != null && root.gameObject.activeSelf == stopped) root.gameObject.SetActive(!stopped);
            if (stopped) return;
            if (BattlePresentationOptions.ReduceMotion) { Cancel(); return; }
            Advance(delta != null ? delta() : Time.unscaledDeltaTime);
        }

        private void Advance(float dt)
        {
            if (root == null || group == null) { playing = false; return; }
            elapsed += Mathf.Max(0f, dt);
            if (stampMode && stamp != null)
            {
                float pop = Mathf.Clamp01(elapsed / .16f);
                float scale = elapsed < .16f ? Mathf.Lerp(1.9f, .94f, pop * pop) : Mathf.Lerp(.94f, 1f, Mathf.Clamp01((elapsed - .16f) / .1f));
                stamp.localScale = Vector3.one * scale;
                stamp.localEulerAngles = new Vector3(0f, 0f, elapsed < .3f ? Mathf.Sin(elapsed * 50f) * 4f * (1f - elapsed / .3f) : 0f);
                group.alpha = Mathf.Min(Mathf.Clamp01(elapsed / .06f), Mathf.Clamp01((StampDuration - elapsed) / .25f));
                if (elapsed >= StampDuration) Cancel();
                return;
            }
            group.alpha = Mathf.Min(Mathf.Clamp01(elapsed / .12f), Mathf.Clamp01((Duration - elapsed) / .2f));
            if (elapsed >= Duration) Cancel();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, string value, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color32(242, 238, 255, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
    }
}

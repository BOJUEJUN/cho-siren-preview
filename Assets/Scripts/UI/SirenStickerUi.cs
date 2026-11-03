using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Shared authored paper/crystal chrome. Never changes a control's hit rectangle or callback.</summary>
    public static class SirenStickerUi
    {
        public static readonly Color Ink = new Color32(13, 9, 22, 255);
        public static readonly Color Paper = new Color32(248, 245, 255, 255);
        public static readonly Color Violet = new Color32(137, 62, 255, 255);
        public static readonly Color Cyan = new Color32(63, 240, 255, 255);
        public static readonly Color Muted = new Color32(194, 182, 212, 255);
        /// <summary>Frame tint of a main action: the paper button frame drawn in brand teal.</summary>
        public static readonly Color ActionTint = new Color32(120, 250, 245, 255);
        private static Sprite panel, button, tab, quiet, primary;
        public static Sprite PanelSprite => panel != null ? panel : panel = MakeFrame("SirenPaperPanel", false, false);
        public static Sprite ButtonSprite => button != null ? button : button = MakeFrame("SirenPaperButton", true, false);
        public static Sprite TabSprite => tab != null ? tab : tab = MakeFrame("SirenPaperTab", false, true);
        private static Sprite QuietSprite => quiet != null ? quiet : quiet = MakeFrame("SirenQuietCard", false, true);
        /// <summary>The one main action on a screen (2026-10-25 unified design): brand-teal sticker, ink label.</summary>
        public static Sprite PrimarySprite => primary != null ? primary : primary = MakeFrame("SirenPrimaryButton", true, false, true);
        /// <summary>Quiet frame for list rows and secondary cards.</summary>
        public static Sprite QuietPanelSprite => QuietSprite;

        // Retain this shared entry point for existing callers, but retire the AI shard
        // cutout everywhere. Primary actions now use the same quiet frame as BattleExit.
        public static Sprite ActionPlate => ButtonSprite;

        public static bool IsReadingPanel(string name, Color color)
        {
            if (color.a < .35f || string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            string[] excluded = { "fill", "track", "mask", "shade", "dim", "glow", "aura", "bar", "meter",
                "portrait", "background", "backdrop", "overlay", "icon", "dot", "ring", "line", "beam", "slot" };
            foreach (string word in excluded) if (n.Contains(word)) return false;
            return true;
        }

        public static void SkinPanel(GameObject root, bool framed = true)
        {
            if (root == null) return;
            var image = root.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = root.AddComponent<UnityEngine.UI.Image>();
            image.sprite = framed ? PanelSprite : QuietSprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.preserveAspect = false;
            image.color = Color.white;
            foreach (var oldEffect in root.GetComponents<UnityEngine.UI.Shadow>()) oldEffect.enabled = false;
            // Preserve the caller's raycast policy: many panels are modal hit blockers.
        }

        /// <summary>
        /// 2026-11-03 (用户: 亮起来会让玩家误以为已经点过): the solid teal sticker now means "selected" only (tabs,
        /// chosen options; <paramref name="selected"/>). The screen's main action (<paramref name="primary"/>) keeps the
        /// dark face with a teal frame and teal label, so it stands out without looking already pressed.
        /// </summary>
        public static void SkinButton(GameObject root, bool primary = false, bool compact = false, bool selected = false)
        {
            if (root == null) return;
            var control = root.GetComponent<UnityEngine.UI.Button>();
            if (control == null) return;
            var hit = root.GetComponent<UnityEngine.UI.Image>();
            if (hit != null) hit.color = Color.clear;
            Transform existingBody = root.transform.Find("Body");
            Transform parent = existingBody != null ? existingBody : root.transform;
            DisableOldFace(root.transform.Find("Shadow"));
            if (existingBody != null)
            {
                DisableOldFace(existingBody);
                DisableOldFace(existingBody.Find("Shadow"));
                DisableOldFace(existingBody.Find("Edge"));
                DisableOldFace(existingBody.Find("Edge/Inset"));
                DisableOldFace(existingBody.Find("Edge/Face"));
                DisableOldFace(existingBody.Find("Inset"));
                DisableOldFace(existingBody.Find("Face"));
            }
            Transform found = parent.Find("SirenButtonFace");
            UnityEngine.UI.Image face;
            if (found == null)
            {
                var node = new GameObject("SirenButtonFace", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                node.transform.SetParent(parent, false);
                node.transform.SetAsFirstSibling();
                face = node.GetComponent<UnityEngine.UI.Image>();
                Stretch(face.rectTransform);
            }
            else face = found.GetComponent<UnityEngine.UI.Image>();
            // Unified design (2026-10-25, 少爷: "不要一个地方一个设计"): primary = brand sticker with an ink label everywhere.
            // Buttons painted into a baked art board keep that art; a brand face would be cut into its silhouette.
            if (root.GetComponent<BakedRegionMotion038>() != null) primary = selected = false;
            bool brand = selected && control.IsInteractable();
            bool action = primary && !selected && control.IsInteractable();
            face.sprite = brand ? PrimarySprite : compact && !primary && !selected ? TabSprite : ButtonSprite;
            face.type = UnityEngine.UI.Image.Type.Sliced;
            face.preserveAspect = false;
            face.pixelsPerUnitMultiplier = 1f;
            face.color = !control.IsInteractable() ? new Color(.70f, .66f, .76f, .94f) : action ? ActionTint : Color.white;
            face.raycastTarget = false;
            var labels = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var label in labels)
            {
                if (label.transform.name == "Label" || labels.Length == 1)
                {
                    label.fontStyle = FontStyle.Bold;
                    label.color = brand ? (Color)Brand1025.Ink : action ? (Color)Brand1025.Teal : control.IsInteractable() ? Paper : Muted;
                    label.raycastTarget = false;
                    OutlineText(label, 1.1f);
                    label.GetComponent<UnityEngine.UI.Outline>().enabled = !brand;
                }
            }
            // A single motion owner; this component also observes enabled/disabled state.
            var feedback = root.GetComponent<SirenStickerButtonFeedback>();
            if (feedback == null) feedback = root.AddComponent<SirenStickerButtonFeedback>();
            feedback.Configure(control, face, labels, primary, compact, selected);
        }

        public static void SkinTitle(UnityEngine.UI.Text title, bool large = false)
        {
            if (title == null) return;
            title.color = Paper;
            title.fontStyle = FontStyle.Bold;
            title.raycastTarget = false;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            OutlineText(title, large ? 2f : 1.2f);
            if (!large || title.transform.parent == null) return;
            string name = "SirenTitlePlate-" + title.name;
            var binding = title.GetComponent<SirenStickerTitleBinding>();
            if (binding == null) binding = title.gameObject.AddComponent<SirenStickerTitleBinding>();
            UnityEngine.UI.Image art;
            if (binding.Plate == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(LayoutElement));
                node.transform.SetParent(title.transform.parent, false);
                node.GetComponent<LayoutElement>().ignoreLayout = true;
                art = node.GetComponent<UnityEngine.UI.Image>();
                binding.Plate = art;
            }
            else art = binding.Plate;
            RectTransform source = title.rectTransform;
            RectTransform rect = art.rectTransform;
            float sourceWidth = Mathf.Max(1f, source.rect.width);
            float sourceHeight = Mathf.Max(1f, source.rect.height);
            float plateWidth = Mathf.Clamp(title.preferredWidth + 56f, 64f, Mathf.Max(64f, sourceWidth));
            float plateHeight = sourceHeight + 18f;
            int horizontal = (int)title.alignment % 3;
            float x = horizontal == 0 ? -source.pivot.x * sourceWidth + plateWidth * .5f - 22f
                : horizontal == 2 ? (1f - source.pivot.x) * sourceWidth - plateWidth * .5f + 22f
                : (.5f - source.pivot.x) * sourceWidth;
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = source.anchoredPosition + new Vector2(x, (.5f - source.pivot.y) * sourceHeight);
            rect.sizeDelta = new Vector2(plateWidth, plateHeight);
            rect.localScale = Vector3.one;
            art.sprite = ActionPlate;
            art.type = UnityEngine.UI.Image.Type.Sliced;
            art.preserveAspect = false;
            art.pixelsPerUnitMultiplier = 1f;
            art.color = Color.white;
            art.raycastTarget = false;
            int titleIndex = title.transform.GetSiblingIndex();
            if (rect.GetSiblingIndex() < titleIndex) titleIndex--;
            rect.SetSiblingIndex(titleIndex);
        }

        private static void DisableOldFace(Transform node)
        {
            if (node == null) return;
            var image = node.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.enabled = false;
        }

        private static void OutlineText(UnityEngine.UI.Text text, float size)
        {
            var outline = text.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.enabled = true;
            outline.effectColor = new Color32(0, 0, 0, 235);
            outline.effectDistance = new Vector2(size, -size);
            outline.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Sprite MakeFrame(string name, bool heavy, bool subdued, bool brand = false)
        {
            const int w = 384, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Deterministic small paper nicks plus deliberate opposing cut corners.
                float nick = ((x / 11) % 5 == 0 ? 1.5f : (x / 7) % 4 == 0 ? .6f : 0f);
                float top = h - 3f - nick - (x > w - 26 ? (x - (w - 26)) * .46f : 0f);
                float bottom = 3f + nick + (x < 26 ? (26 - x) * .46f : 0f);
                float left = 3f + (y > h - 20 ? (y - (h - 20)) * .38f : 0f);
                float right = w - 3f - (y < 20 ? (20 - y) * .38f : 0f);
                float distance = Mathf.Min(Mathf.Min(x + .5f - left, right - x - .5f),
                    Mathf.Min(y + .5f - bottom, top - y - .5f));
                if (distance < -.5f) continue;
                Color32 c;
                if (distance < 1.5f) c = new Color32(1, 0, 5, 255);
                else if (distance < (heavy ? 5.7f : 3.6f))
                    c = subdued ? new Color32(168, 151, 195, 145) : (Color32)Paper;
                else if ((x < 30 && y < 23) || (x > w - 35 && y > h - 25))
                    c = brand ? (Color32)Ink : (Color32)Violet;
                else if (x < 52 && y > h - 28 && (x + y) % 9 < 2)
                    c = brand ? Brand1025.TealDeep : new Color32(72, 36, 108, subdued ? (byte)175 : (byte)255);
                else c = brand ? Brand1025.Teal : new Color32(13, 9, 22, subdued ? (byte)233 : (byte)255);
                c.a = (byte)(c.a * Mathf.Clamp01(distance + .5f));
                pixels[y * w + x] = c;
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            Sprite result = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.one * .5f,
                100f, 0, SpriteMeshType.FullRect, new Vector4(28, 24, 28, 24));
            result.name = name; result.hideFlags = HideFlags.DontSave;
            return result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SirenStickerButtonFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private UnityEngine.UI.Button button;
        private UnityEngine.UI.Image face;
        private bool hovered, pressed;
        private float amount;
        private bool animateVisual = true;
        private bool wasInteractable = true;
        private bool primary, compact, selected;
        private UnityEngine.UI.Text[] labels;
        public void Configure(UnityEngine.UI.Button control, UnityEngine.UI.Image image, UnityEngine.UI.Text[] captions,
            bool isPrimary = false, bool isCompact = false, bool isSelected = false)
        {
            button = control; face = image; labels = captions; primary = isPrimary; compact = isCompact; selected = isSelected;
            ResolveMotionOwner();
            wasInteractable = !button.IsInteractable();
        }
        private void Start() { ResolveMotionOwner(); }
        private void ResolveMotionOwner()
        {
            var baked = GetComponent<BakedRegionMotion038>();
            animateVisual = baked == null && GetComponent<ModalPolish038PressFeedback>() == null;
            if (baked != null && face != null) baked.RegisterVisual(face.rectTransform);
        }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        private int captionScanFrame1103 = -1000;

        /// <summary>
        /// 2026-11-03 (用户: 开始挑战看不见了): many callers add or recolour their caption after skinning and still
        /// paint it ink for the old teal face. On the dark action face any near-ink caption is invisible, so it is
        /// redrawn in teal whenever it appears (checked a few times a second, captions only).
        /// </summary>
        private void KeepActionCaptionsReadable1103(bool active)
        {
            if (Time.frameCount - captionScanFrame1103 < 8) return;
            captionScanFrame1103 = Time.frameCount;
            foreach (var text in GetComponentsInChildren<UnityEngine.UI.Text>(false))
            {
                Color c = text.color;
                if (c.a <= .2f || c.r * .3f + c.g * .59f + c.b * .11f >= .2f) continue;
                Color readable = active ? (Color)Brand1025.Teal : SirenStickerUi.Muted;   // locked/disabled: muted
                readable.a = c.a;
                text.color = readable;
            }
        }

        private void OnDisable()
        { hovered = pressed = false; amount = 0; if (face != null) face.rectTransform.localScale = Vector3.one; }
        private void LateUpdate()
        {
            if (face == null || button == null) return;
            bool active = button.IsInteractable();
            if (primary && !selected) KeepActionCaptionsReadable1103(active);
            if (active != wasInteractable)
            {
                wasInteractable = active;
                bool brand = selected && active;
                bool action = primary && !selected && active;
                face.sprite = brand ? SirenStickerUi.PrimarySprite : compact && !primary && !selected ? SirenStickerUi.TabSprite : SirenStickerUi.ButtonSprite;
                if (labels != null) foreach (var label in labels)
                    if (label != null && (label.name == "Label" || labels.Length == 1))
                    {
                        label.color = brand ? (Color)Brand1025.Ink : action ? (Color)Brand1025.Teal : active ? SirenStickerUi.Paper : SirenStickerUi.Muted;
                        var outline = label.GetComponent<UnityEngine.UI.Outline>();
                        if (outline != null) outline.enabled = !brand;
                    }
            }
            float target = active && pressed ? -1f : active && hovered ? .55f : 0f;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 12f);
            if (animateVisual)
            {
                face.rectTransform.localScale = Vector3.one * (1f + amount * .025f);
                face.rectTransform.anchoredPosition = new Vector2(0f, amount < 0 ? -2f : 0f);
            }
            Color rest = primary && !selected ? SirenStickerUi.ActionTint : Color.white;
            face.color = active ? Color.Lerp(rest, primary || selected ? new Color(.82f, 1f, .88f) : new Color(.79f, .61f, 1f), Mathf.Max(0, amount) * .3f)
                : new Color(.70f, .66f, .76f, .94f);
        }
    }
}

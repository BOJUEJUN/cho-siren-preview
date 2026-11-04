using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Swap only the two authored character layers; the homepage UI never changes parents.</summary>
    public sealed class PsdHomeCharacterCarousel : MonoBehaviour
    {
        private Image portrait, outline;
        private PsdHomeMotion motion;
        private PsdHomeCharacterSurface performance;
        private PsdHomeIdolVideo1025 idols;
        private Sprite[] portraits, outlines;
        private Action<int> selected;
        private Func<bool> blocked;
        private Button previous, next;
        private float elapsed, responseUntil, nextIdleGestureAt;
        private int direction, pendingIndex;
        private bool swapped;
        // PSD pixel rects (x, y, w, h) of each idol's portrait and rim layer.
        private static readonly Vector4[] PortraitRects =
        {
            new Vector4(7, 184, 4268, 8358), new Vector4(-1066, 751, 5170, 7513),
            new Vector4(-777, 647, 5912, 8614), new Vector4(-207, 517, 5486, 8425),
        };
        private static readonly Vector4[] OutlineRects =
        {
            new Vector4(0, 184, 4027, 8358), new Vector4(0, 755, 2743, 7521),
            new Vector4(-852, 685, 4352, 8568), new Vector4(-272, 759, 4754, 8134),
        };
        /// <summary>
        /// 2026-11-04 (赵金 "这个角色中线偏了，要正一点"): stage units (720 wide) each idol moves sideways so her face
        /// sits on the screen's centre line; measured on the authored stills (face centres were .535/.41/.555/.425).
        /// The idle clips are children of the portrait and move with it.
        /// </summary>
        public static readonly float[] CenterShift1104 = { -25f, 63f, -40f, 54f };
        /// <summary>
        /// 2026-11-04 (赵金 "第四个角色可以歪一点…上面往右边歪下面往左边"): degrees each idol turns (negative = clockwise) about her face;
        /// 粉珀人鱼 turns 4° counter-clockwise (用户箭头 上往左、下往右): her idle clip already leans ~6°, so in game she ends near upright. The face stays where the centring put it.
        /// </summary>
        public static readonly float[] Tilt1104 = { 0f, 0f, 0f, 4f };
        // Face centre (stage units, y down) the tilt turns about, after CenterShift1104.
        private static readonly Vector2[] TiltPivot1104 = { Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(365f, 397f) };
        public int Index { get; private set; }
        public bool Switching { get; private set; }
        public bool HasPerformance => Index == 0;

        public void Initialize(PsdHomeMotion owner, Font font, Sprite plate, int initial,
            Action<int> onSelected, Func<bool> isBlocked)
        {
            motion = owner; selected = onSelected; blocked = isBlocked;
            portrait = transform.Find("layer-04VisualV2").GetComponent<Image>();
            outline = transform.Find("layer-03VisualV2").GetComponent<Image>();
            performance = GetComponentInChildren<PsdHomeCharacterSurface>();
            // 2026-10-29 PSD v3: 角色3 (purple elf) and 组1 (pink mermaid) join the two original idols.
            portraits = new[] { portrait.sprite, ReferenceArt038.Load("Art/Reference038/home-layer-character2-portrait"),
                ReferenceArt038.Load("Art/Reference038/home-layer-character3-portrait"),
                ReferenceArt038.Load("Art/Reference038/home-layer-character4-portrait") };
            outlines = new[] { outline.sprite, ReferenceArt038.Load("Art/Reference038/home-layer-character2-outline"),
                ReferenceArt038.Load("Art/Reference038/home-layer-character3-outline"),
                ReferenceArt038.Load("Art/Reference038/home-layer-character4-outline") };
            previous = Arrow("PreviousCharacter", -1, 12, font, plate);
            next = Arrow("NextCharacter", 1, 648, font, plate);
            Apply(Mathf.Clamp(initial, 0, portraits.Length - 1));
        }

        /// <summary>2026-10-25: the idols' own looping clips replace the page's performance overlay.</summary>
        public void AttachIdols(PsdHomeIdolVideo1025 clips)
        {
            idols = clips;
            PsdHomeCharacterBridge.SetIdle(false);
            PsdHomeCharacterBridge.Stop();
            idols?.ShowIdol(Index, portrait.rectTransform);
        }

        private Button Arrow(string name, int step, float x, Font font, Sprite plate)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            Place(go.GetComponent<RectTransform>(), x, 534, 60, 78);
            var hit = go.GetComponent<Image>(); hit.color = Color.clear;
            var visual = new GameObject(name + "VisualV2", typeof(RectTransform));
            visual.transform.SetParent(go.transform, false);
            Place(visual.GetComponent<RectTransform>(), 7, 9, 46, 60);
            var glyph = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(PsdHomeChevron));
            glyph.transform.SetParent(visual.transform, false);
            Place(glyph.GetComponent<RectTransform>(), 0, 0, 46, 60);
            var chevron = glyph.GetComponent<PsdHomeChevron>(); chevron.Initialize(step);
            var button = go.GetComponent<Button>(); button.targetGraphic = chevron;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Switch(step));
            go.AddComponent<PsdHomeChevronFeedback>().Configure(chevron, visual.GetComponent<RectTransform>());
            motion.RegisterUi(go);
            return button;
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        public void Switch(int step)
        {
            if (step == 0 || Switching || blocked?.Invoke() == true) return;
            int dir = step > 0 ? 1 : -1;
            pendingIndex = Index;
            // Skip an idol whose art is missing instead of stopping the carousel on her neighbour.
            for (int tries = 0; tries < portraits.Length; tries++)
            {
                pendingIndex = (pendingIndex + dir + portraits.Length) % portraits.Length;
                if (portraits[pendingIndex] != null && outlines[pendingIndex] != null) break;
            }
            if (pendingIndex == Index) return;
            motion.CancelCharacterInteraction();
            PsdHomeCharacterBridge.SetIdle(false);
            PsdHomeCharacterBridge.Stop();
            performance.ResetPlayback();
            direction = step > 0 ? 1 : -1;
            Switching = true; swapped = false; elapsed = 0; responseUntil = 0;
        }

        private void Apply(int index)
        {
            Index = index;
            portrait.sprite = portraits[index]; outline.sprite = outlines[index];
            const float scale = 720f / 4034f;
            Vector4 p = PortraitRects[index], o = OutlineRects[index];
            float shift = CenterShift1104[index];
            Place(portrait.rectTransform, p.x * scale + shift, p.y * scale, p.z * scale, p.w * scale);
            Place(outline.rectTransform, o.x * scale + shift, o.y * scale, o.z * scale, o.w * scale);
            portrait.rectTransform.localScale = outline.rectTransform.localScale = Vector3.one;
            Tilt(portrait.rectTransform, index); Tilt(outline.rectTransform, index);
            portrait.enabled = outline.enabled = true;
            motion.SetCharacter(portrait.rectTransform); motion.SetOutline(outline.rectTransform);
            motion.SetSpeaker(index);
            PsdHomeCharacterBridge.SetIdle(idols == null && HasPerformance && isActiveAndEnabled);
            idols?.ShowIdol(index, portrait.rectTransform);
        }

        // Turns a top-left-pivoted layer about the idol's face by moving its pivot so the face point stays put.
        private static void Tilt(RectTransform rect, int index)
        {
            float degrees = Tilt1104[index];
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            if (Mathf.Approximately(degrees, 0f)) return;
            Vector2 face = new Vector2(TiltPivot1104[index].x, -TiltPivot1104[index].y);
            Vector2 v = face - rect.anchoredPosition;
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            rect.anchoredPosition = face - new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public void Respond()
        {
            if (!Switching) responseUntil = Time.unscaledTime + .55f;
            nextIdleGestureAt = Time.unscaledTime + UnityEngine.Random.Range(6f, 9f);
        }

        private void Update()
        {
            bool available = !Switching && !motion.UiHidden && blocked?.Invoke() != true;
            previous.interactable = next.interactable = available;
            // Without an idle clip she bounces on her own now and then, like a live idle loop.
            if (!HasPerformance && (idols == null || !idols.Showing) && !Switching && Time.unscaledTime >= nextIdleGestureAt)
            {
                if (nextIdleGestureAt > 0f) responseUntil = Time.unscaledTime + .55f;
                nextIdleGestureAt = Time.unscaledTime + UnityEngine.Random.Range(6f, 9f);
            }
            if (!Switching) return;
            elapsed += Time.unscaledDeltaTime;
            if (!swapped && elapsed >= .16f)
            {
                Apply(pendingIndex); swapped = true; selected?.Invoke(Index);
            }
            if (elapsed >= .38f)
            {
                Switching = false;
                portrait.color = outline.color = Color.white;
            }
        }

        private void LateUpdate()
        {
            if (!Switching) idols?.SetSwipeAlpha(1f);
            if (Switching)
            {
                float t = swapped ? Mathf.Clamp01((elapsed - .16f) / .22f) : Mathf.Clamp01(elapsed / .16f);
                float ease = t * t * (3 - 2 * t);
                float offset = direction * (swapped ? 34 * (1 - ease) : -28 * ease);
                float alpha = swapped ? ease : 1 - ease;
                portrait.rectTransform.anchoredPosition += Vector2.right * offset;
                outline.rectTransform.anchoredPosition += Vector2.right * offset;
                portrait.color = outline.color = new Color(1, 1, 1, alpha);
                idols?.SetSwipeAlpha(alpha);
            }
            else if (responseUntil > Time.unscaledTime)
            {
                float progress = 1 - (responseUntil - Time.unscaledTime) / .55f;
                float lift = Mathf.Sin(progress * Mathf.PI) * 8f;
                portrait.rectTransform.anchoredPosition += Vector2.up * lift;
                outline.rectTransform.anchoredPosition += Vector2.up * lift;
            }
        }

        private void OnEnable() { if (portraits != null) PsdHomeCharacterBridge.SetIdle(idols == null && HasPerformance); }
        private void OnDisable() { PsdHomeCharacterBridge.SetIdle(false); PsdHomeCharacterBridge.Stop(); }
    }

    // Geometry keeps the controls crisp and independent of the Chinese font's glyph subset.
    public sealed class PsdHomeChevron : MaskableGraphic
    {
        private int direction;
        public void Initialize(int step) { direction = step; raycastTarget = false; color = PsdHomeChevronFeedback.RestColor; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Vector2 center = rectTransform.rect.center;
            Vector2 a = center + new Vector2(-6 * direction, 13);
            Vector2 b = center + new Vector2(6 * direction, 0);
            Vector2 c = center + new Vector2(-6 * direction, -13);
            Line(vh, a, b); Line(vh, b, c);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * 1.8f;
            int start = vh.currentVertCount;
            foreach (Vector2 p in new[] { a - n, a + n, b + n, b - n }) vh.AddVert(p, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <summary>Only the chevron brightens and presses; its invisible hit rectangle stays fixed.</summary>
    public sealed class PsdHomeChevronFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public static readonly Color RestColor = new Color32(237, 229, 255, 172);
        private Button button;
        private PsdHomeChevron glyph;
        private RectTransform visual;
        private bool hovered, pressed;

        public void Configure(PsdHomeChevron shape, RectTransform body)
        {
            button = GetComponent<Button>(); glyph = shape; visual = body;
            ResetVisual();
        }

        private void Update()
        {
            if (glyph == null || visual == null) return;
            if (button == null || !button.IsInteractable()) { ResetVisual(); return; }
            Color target = hovered || pressed ? new Color32(249, 247, 255, 240) : RestColor;
            glyph.color = Color.Lerp(glyph.color, target, Mathf.Clamp01(Time.unscaledDeltaTime * 16f));
            visual.localScale = Vector3.Lerp(visual.localScale,
                Vector3.one * (pressed ? .94f : hovered ? 1.025f : 1f), Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
        }

        public void OnPointerEnter(PointerEventData data) { if (button != null && button.IsInteractable()) hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && button != null && button.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData data) => pressed = false;
        private void OnDisable() => ResetVisual();
        private void ResetVisual()
        {
            hovered = pressed = false;
            if (glyph != null) glyph.color = RestColor;
            if (visual != null) visual.localScale = Vector3.one;
        }
    }
}

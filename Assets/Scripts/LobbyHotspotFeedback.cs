using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Subtle edges for baked artwork and tactile motion for separate live image layers.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LobbyHotspotFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum VisualKind { Entry, CallToAction, Navigation, CurrencyPlus, Profile, Settings }

        private Button button;
        private RectTransform effectRoot, glyphRect;
        private CanvasGroup effectGroup;
        private LobbyHotspotAccentGraphic accent;
        private VisualKind kind;
        private bool hovered, pressed;
        private float sweep = 1f;
        private readonly TactileFeedbackTween motion = new TactileFeedbackTween();
        private readonly TactileVisualMotion visuals = new TactileVisualMotion();

        private void Awake() { button = GetComponent<Button>(); }

        public void Configure(VisualKind visualKind, Sprite glyph = null)
        {
            visuals.Restore();
            kind = visualKind;
            BuildEffect(glyph);
            visuals.Capture(transform);
            ResetImmediately();
        }

        /// <summary>The underline's measured centre/baseline remain independent of input and icon motion.</summary>
        public void ConfigureNavigationVisual(float centerFromLeft, float baselineFromTop, float width)
        {
            if (kind != VisualKind.Navigation || accent == null) return;
            RectTransform rect = accent.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(centerFromLeft, -baselineFromTop);
            rect.sizeDelta = new Vector2(width, 8f);
            accent.SetVerticesDirty();
        }

        private void OnEnable() => ResetImmediately();
        private void OnDisable() => ResetImmediately();

        private void LateUpdate()
        {
            if (effectGroup == null) return;
            if (button == null || !button.enabled || !button.IsInteractable())
            { ResetImmediately(); return; }
            motion.Update(pressed ? 2 : hovered ? 1 : 0, Time.unscaledDeltaTime);
            effectGroup.alpha = motion.Alpha;
            visuals.Apply(motion.Scale, motion.Lift);
            // A plus overlay is a visual child too; never rotate or enlarge the entire hit rectangle.
            if (glyphRect != null) glyphRect.localScale = Vector3.one * motion.Scale;
            if (accent != null && hovered && sweep < 1f)
            {
                sweep = Mathf.Min(1f, sweep + Time.unscaledDeltaTime / .30f);
                accent.Phase = sweep;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.enabled || !button.IsInteractable()) return;
            hovered = true; sweep = 0f;
            visuals.Capture(transform);
            if (accent != null) accent.Phase = 0f;
        }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left && button != null && button.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void BuildEffect(Sprite glyph)
        {
            Transform existing = transform.Find("InteractionFx");
            if (existing != null) { existing.gameObject.SetActive(false); Destroy(existing.gameObject); }
            accent = null; glyphRect = null;
            GameObject root = new GameObject("InteractionFx", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            effectRoot = root.GetComponent<RectTransform>();
            effectRoot.anchorMin = Vector2.zero; effectRoot.anchorMax = Vector2.one;
            effectRoot.offsetMin = effectRoot.offsetMax = Vector2.zero;
            effectGroup = root.GetComponent<CanvasGroup>();
            effectGroup.alpha = 0f; effectGroup.interactable = false; effectGroup.blocksRaycasts = false;
            if (kind == VisualKind.CurrencyPlus && glyph != null)
            {
                GameObject node = new GameObject("HoverGlyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = gameObject.layer; node.transform.SetParent(effectRoot, false);
                glyphRect = node.GetComponent<RectTransform>();
                glyphRect.anchorMin = glyphRect.anchorMax = glyphRect.pivot = Vector2.one * .5f;
                glyphRect.anchoredPosition = Vector2.zero;
                glyphRect.sizeDelta = glyph.rect.size * (720f / 821f);
                Image image = node.GetComponent<Image>();
                image.sprite = glyph; image.color = new Color32(217, 206, 255, 240);
                image.preserveAspect = true; image.useSpriteMesh = true; image.raycastTarget = false;
            }
            else
            {
                GameObject node = new GameObject(AccentName(kind), typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbyHotspotAccentGraphic));
                node.layer = gameObject.layer; node.transform.SetParent(effectRoot, false);
                RectTransform rect = node.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                accent = node.GetComponent<LobbyHotspotAccentGraphic>(); accent.Configure(kind);
            }
            root.transform.SetAsLastSibling();
        }

        private void ResetImmediately()
        {
            hovered = pressed = false; sweep = 1f; motion.Reset(); visuals.Restore();
            if (effectGroup != null) effectGroup.alpha = 0f;
            if (glyphRect != null) glyphRect.localScale = Vector3.one;
            if (accent != null) accent.Phase = 1f;
        }

        private static string AccentName(VisualKind visualKind) => visualKind switch
        {
            VisualKind.CallToAction => "CrystalAccent",
            VisualKind.Navigation => "HoverUnderline",
            VisualKind.Profile => "AvatarRing",
            VisualKind.Settings => "GearRing",
            _ => "HoverAccent",
        };
    }

    /// <summary>Unfilled, feathered slanted edges; one sweep per entry, with no looping flash.</summary>
    internal sealed class LobbyHotspotAccentGraphic : MaskableGraphic
    {
        private LobbyHotspotFeedback.VisualKind kind;
        private float phase = 1f;
        public float Phase
        {
            get => phase;
            set { if (Mathf.Abs(phase - value) < .001f) return; phase = value; SetVerticesDirty(); }
        }
        public void Configure(LobbyHotspotFeedback.VisualKind visualKind)
        {
            kind = visualKind; color = new Color32(197, 157, 248, 230);
            raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            switch (kind)
            {
                case LobbyHotspotFeedback.VisualKind.Navigation:
                    SoftLine(vh, Point(rect, .12f, .5f), Point(rect, .88f, .5f), 1.6f, new Color32(175, 213, 255, 225));
                    break;
                case LobbyHotspotFeedback.VisualKind.Profile:
                    Arc(vh, new Vector2(rect.xMin + rect.width * .23f, rect.center.y),
                        Mathf.Min(rect.height * .39f, rect.width * .19f));
                    break;
                case LobbyHotspotFeedback.VisualKind.Settings:
                    Arc(vh, rect.center, Mathf.Min(rect.width, rect.height) * .31f);
                    break;
                case LobbyHotspotFeedback.VisualKind.CurrencyPlus:
                    SoftLine(vh, Point(rect, .35f, .22f), Point(rect, .68f, .22f), 1.3f, color);
                    break;
                case LobbyHotspotFeedback.VisualKind.CallToAction:
                    DrawCrystal(vh, rect);
                    break;
                default:
                    DrawEntry(vh, rect);
                    break;
            }
        }

        private void DrawEntry(VertexHelper vh, Rect rect)
        {
            Vector2 tl = Point(rect, .035f, .80f), tr = Point(rect, .90f, .975f);
            Vector2 bl = Point(rect, .11f, .04f), br = Point(rect, .97f, .20f);
            SoftLine(vh, Vector2.Lerp(tl, tr, .10f), Vector2.Lerp(tl, tr, .55f), 1.15f, color);
            SoftLine(vh, Vector2.Lerp(bl, br, .43f), Vector2.Lerp(bl, br, .89f), 1.15f, new Color32(135, 197, 249, 165));
            Sweep(vh, tl, tr);
        }

        private void DrawCrystal(VertexHelper vh, Rect rect)
        {
            // Asymmetric CTA triangle matches the long upper-right crystal tip and lower point.
            Vector2 left = Point(rect, .045f, .43f), tip = Point(rect, .95f, .95f), bottom = Point(rect, .48f, .035f);
            SoftLine(vh, Vector2.Lerp(left, tip, .08f), Vector2.Lerp(left, tip, .73f), 1.35f, color);
            SoftLine(vh, Vector2.Lerp(left, bottom, .12f), Vector2.Lerp(left, bottom, .72f), 1.2f, new Color32(147, 186, 255, 160));
            SoftLine(vh, Vector2.Lerp(bottom, tip, .10f), Vector2.Lerp(bottom, tip, .37f), 1.1f, new Color32(183, 139, 248, 120));
            Sweep(vh, left, tip);
        }

        private void Sweep(VertexHelper vh, Vector2 start, Vector2 end)
        {
            if (phase <= 0 || phase >= 1) return;
            float envelope = Mathf.Sin(phase * Mathf.PI);
            Color tint = new Color(.75f, .90f, 1f, envelope * .85f);
            SoftLine(vh, Vector2.Lerp(start, end, Mathf.Max(0, phase - .08f)),
                Vector2.Lerp(start, end, Mathf.Min(1, phase + .08f)), 1.65f, tint);
        }

        private void Arc(VertexHelper vh, Vector2 center, float radius)
        {
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(.15f, 1.3f, i / (float)segments) * Mathf.PI;
                float b = Mathf.Lerp(.15f, 1.3f, (i + 1) / (float)segments) * Mathf.PI;
                SoftLine(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 1.2f, color);
            }
        }

        private static Vector2 Point(Rect rect, float x, float y) => new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);
        private static void SoftLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Color halo = tint; halo.a *= .055f;
            AddLine(vh, a, b, thickness + 5f, halo);
            halo.a = tint.a * .13f; AddLine(vh, a, b, thickness + 2f, halo);
            AddLine(vh, a, b, thickness, tint);
        }
        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 tint)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            int first = vh.currentVertCount;
            AddVertex(vh, a - normal, tint); AddVertex(vh, a + normal, tint);
            AddVertex(vh, b + normal, tint); AddVertex(vh, b - normal, tint);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
        private static void AddVertex(VertexHelper vh, Vector2 position, Color32 tint)
        { UIVertex v = UIVertex.simpleVert; v.position = position; v.color = tint; vh.AddVert(v); }
    }
}

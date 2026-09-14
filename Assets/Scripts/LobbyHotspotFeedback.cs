using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Visible pointer feedback for the 0.3.8 lobby's transparent hotspots. The approved
    /// golden remains untouched; this component only animates a zero-alpha child effect.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LobbyHotspotFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum VisualKind
        {
            Entry,
            CallToAction,
            Navigation,
            CurrencyPlus,
            Profile,
            Settings,
        }

        private Button button;
        private RectTransform effectRoot;
        private CanvasGroup effectGroup;
        private LobbyHotspotAccentGraphic accent;
        private VisualKind kind;
        private bool hovered;
        private bool pressed;
        private float currentAlpha;
        private float currentScale = 1f;
        private float currentRotation;
        private float phase;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void Configure(VisualKind visualKind, Sprite glyph = null)
        {
            kind = visualKind;
            BuildEffect(glyph);
            ResetImmediately();
        }

        private void OnEnable()
        {
            hovered = false;
            pressed = false;
            ResetImmediately();
        }

        private void LateUpdate()
        {
            if (effectRoot == null || effectGroup == null) return;

            bool interactable = button != null && button.IsInteractable();
            if (!interactable)
            {
                hovered = false;
                pressed = false;
            }

            float targetAlpha = pressed && interactable ? 1f : hovered && interactable ? HoverAlpha(kind) : 0f;
            float targetScale = pressed && interactable ? PressScale(kind) : hovered && interactable ? HoverScale(kind) : 1f;
            float targetRotation = pressed && interactable ? PressRotation(kind) :
                hovered && interactable ? HoverRotation(kind) : 0f;
            float response = pressed ? 30f : hovered ? 22f : 18f;
            float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);

            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, blend);
            currentScale = Mathf.Lerp(currentScale, targetScale, blend);
            currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, blend);

            if (Mathf.Abs(currentAlpha - targetAlpha) < 0.001f) currentAlpha = targetAlpha;
            if (Mathf.Abs(currentScale - targetScale) < 0.0005f) currentScale = targetScale;
            if (Mathf.Abs(Mathf.DeltaAngle(currentRotation, targetRotation)) < 0.05f)
                currentRotation = targetRotation;

            effectGroup.alpha = currentAlpha;
            effectRoot.localScale = Vector3.one * currentScale;
            effectRoot.localRotation = Quaternion.Euler(0f, 0f, currentRotation);

            if (accent != null && hovered && interactable)
            {
                phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * (kind == VisualKind.CallToAction ? 0.9f : 0.55f), 1f);
                accent.Phase = phase;
            }
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            ResetImmediately();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable()) hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable()) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        private void BuildEffect(Sprite glyph)
        {
            Transform existing = transform.Find("InteractionFx");
            if (existing != null) Destroy(existing.gameObject);

            GameObject rootObject = new GameObject("InteractionFx", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.layer = gameObject.layer;
            rootObject.transform.SetParent(transform, false);
            effectRoot = rootObject.GetComponent<RectTransform>();
            effectRoot.anchorMin = Vector2.zero;
            effectRoot.anchorMax = Vector2.one;
            effectRoot.offsetMin = Vector2.zero;
            effectRoot.offsetMax = Vector2.zero;
            effectRoot.pivot = new Vector2(0.5f, 0.5f);

            effectGroup = rootObject.GetComponent<CanvasGroup>();
            effectGroup.alpha = 0f;
            effectGroup.interactable = false;
            effectGroup.blocksRaycasts = false;

            if (kind == VisualKind.CurrencyPlus && glyph != null)
            {
                GameObject glyphObject = new GameObject("HoverGlyph", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                glyphObject.layer = gameObject.layer;
                glyphObject.transform.SetParent(effectRoot, false);
                RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
                glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
                glyphRect.pivot = new Vector2(0.5f, 0.5f);
                glyphRect.anchoredPosition = Vector2.zero;
                glyphRect.sizeDelta = new Vector2(glyph.rect.width, glyph.rect.height) * (720f / 821f);
                Image glyphImage = glyphObject.GetComponent<Image>();
                glyphImage.sprite = glyph;
                glyphImage.color = Color.white;
                glyphImage.preserveAspect = true;
                glyphImage.useSpriteMesh = true;
                glyphImage.raycastTarget = false;
            }
            else
            {
                GameObject accentObject = new GameObject(AccentName(kind), typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(LobbyHotspotAccentGraphic));
                accentObject.layer = gameObject.layer;
                accentObject.transform.SetParent(effectRoot, false);
                RectTransform accentRect = accentObject.GetComponent<RectTransform>();
                accentRect.anchorMin = Vector2.zero;
                accentRect.anchorMax = Vector2.one;
                accentRect.offsetMin = Vector2.zero;
                accentRect.offsetMax = Vector2.zero;
                accent = accentObject.GetComponent<LobbyHotspotAccentGraphic>();
                accent.Configure(kind);
                accent.raycastTarget = false;
            }

            rootObject.transform.SetAsLastSibling();
        }

        private void ResetImmediately()
        {
            currentAlpha = 0f;
            currentScale = 1f;
            currentRotation = 0f;
            phase = 0f;
            if (effectGroup != null) effectGroup.alpha = 0f;
            if (effectRoot != null)
            {
                effectRoot.localScale = Vector3.one;
                effectRoot.localRotation = Quaternion.identity;
            }
            if (accent != null) accent.Phase = 0f;
        }

        private static string AccentName(VisualKind visualKind)
        {
            return visualKind switch
            {
                VisualKind.CallToAction => "CrystalAccent",
                VisualKind.Navigation => "HoverUnderline",
                VisualKind.Profile => "AvatarRing",
                VisualKind.Settings => "GearRing",
                _ => "HoverAccent",
            };
        }

        private static float HoverAlpha(VisualKind visualKind)
        {
            return visualKind switch
            {
                VisualKind.Entry => 0.72f,
                VisualKind.CallToAction => 0.85f,
                VisualKind.Navigation => 0.78f,
                VisualKind.CurrencyPlus => 0.92f,
                VisualKind.Profile => 0.80f,
                VisualKind.Settings => 0.85f,
                _ => 0.75f,
            };
        }

        private static float HoverScale(VisualKind visualKind)
        {
            return visualKind switch
            {
                VisualKind.Entry => 1.012f,
                VisualKind.CallToAction => 1.018f,
                VisualKind.Navigation => 1.025f,
                VisualKind.CurrencyPlus => 1.16f,
                VisualKind.Profile => 1.04f,
                VisualKind.Settings => 1.06f,
                _ => 1.02f,
            };
        }

        private static float PressScale(VisualKind visualKind)
        {
            return visualKind switch
            {
                VisualKind.Entry => 0.965f,
                VisualKind.CallToAction => 0.95f,
                VisualKind.Navigation => 0.92f,
                VisualKind.CurrencyPlus => 0.78f,
                VisualKind.Profile => 0.90f,
                VisualKind.Settings => 0.86f,
                _ => 0.95f,
            };
        }

        private static float HoverRotation(VisualKind visualKind)
        {
            return visualKind == VisualKind.Profile ? 8f : visualKind == VisualKind.Settings ? 14f : 0f;
        }

        private static float PressRotation(VisualKind visualKind)
        {
            return visualKind == VisualKind.Profile ? -4f : visualKind == VisualKind.Settings ? -8f : 0f;
        }
    }

    /// <summary>Procedural, unfilled accents: no lobby pixels or rectangular backgrounds are copied.</summary>
    internal sealed class LobbyHotspotAccentGraphic : MaskableGraphic
    {
        private LobbyHotspotFeedback.VisualKind kind;
        private float phase;

        public float Phase
        {
            get => phase;
            set
            {
                if (Mathf.Abs(phase - value) < 0.001f) return;
                phase = value;
                SetVerticesDirty();
            }
        }

        public void Configure(LobbyHotspotFeedback.VisualKind visualKind)
        {
            kind = visualKind;
            color = visualKind == LobbyHotspotFeedback.VisualKind.Navigation
                ? new Color32(235, 219, 255, 255)
                : new Color32(176, 93, 255, 255);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            switch (kind)
            {
                case LobbyHotspotFeedback.VisualKind.Navigation:
                    DrawNavigation(vh, rect);
                    break;
                case LobbyHotspotFeedback.VisualKind.Profile:
                    DrawRing(vh, rect, new Vector2(rect.xMin + rect.width * 0.23f, rect.center.y),
                        Mathf.Min(rect.height * 0.39f, rect.width * 0.19f), 2.2f, 36);
                    break;
                case LobbyHotspotFeedback.VisualKind.Settings:
                    DrawRing(vh, rect, rect.center, Mathf.Min(rect.width, rect.height) * 0.31f, 2.2f, 32);
                    break;
                default:
                    DrawCornerAccents(vh, rect,
                        kind == LobbyHotspotFeedback.VisualKind.CallToAction ? 0.19f : 0.14f);
                    DrawMovingGlint(vh, rect);
                    break;
            }
        }

        private void DrawCornerAccents(VertexHelper vh, Rect rect, float lengthRatio)
        {
            float shortSide = Mathf.Min(rect.width, rect.height);
            float inset = Mathf.Clamp(shortSide * 0.035f, 5f, 12f);
            float length = Mathf.Clamp(shortSide * lengthRatio, 18f, 70f);
            float thickness = Mathf.Clamp(shortSide * 0.012f, 2f, 4f);
            float left = rect.xMin + inset;
            float right = rect.xMax - inset;
            float bottom = rect.yMin + inset;
            float top = rect.yMax - inset;

            AddLine(vh, new Vector2(left, top), new Vector2(left + length, top), thickness, color);
            AddLine(vh, new Vector2(left, top), new Vector2(left, top - length), thickness, color);
            AddLine(vh, new Vector2(right, top), new Vector2(right - length, top), thickness, color);
            AddLine(vh, new Vector2(right, top), new Vector2(right, top - length), thickness, color);
            AddLine(vh, new Vector2(left, bottom), new Vector2(left + length, bottom), thickness, color);
            AddLine(vh, new Vector2(left, bottom), new Vector2(left, bottom + length), thickness, color);
            AddLine(vh, new Vector2(right, bottom), new Vector2(right - length, bottom), thickness, color);
            AddLine(vh, new Vector2(right, bottom), new Vector2(right, bottom + length), thickness, color);
        }

        private void DrawMovingGlint(VertexHelper vh, Rect rect)
        {
            float travel = Mathf.Lerp(rect.xMin + rect.width * 0.16f, rect.xMax - rect.width * 0.16f, phase);
            float half = Mathf.Clamp(rect.width * 0.055f, 12f, 30f);
            Color32 glint = new Color32(117, 232, 255, 235);
            AddLine(vh, new Vector2(travel - half, rect.yMin + rect.height * 0.12f),
                new Vector2(travel + half, rect.yMin + rect.height * 0.18f), 2f, glint);
        }

        private void DrawNavigation(VertexHelper vh, Rect rect)
        {
            float width = rect.width * Mathf.Lerp(0.34f, 0.64f, 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f));
            float y = rect.yMin + Mathf.Clamp(rect.height * 0.08f, 7f, 13f);
            Color32 cyan = new Color32(121, 226, 255, 245);
            AddLine(vh, new Vector2(rect.center.x - width * 0.5f, y),
                new Vector2(rect.center.x + width * 0.5f, y), 2.5f, cyan);
        }

        private void DrawRing(VertexHelper vh, Rect rect, Vector2 center, float radius, float thickness, int segments)
        {
            float pulse = 1f + 0.035f * Mathf.Sin(phase * Mathf.PI * 2f);
            float radiusX = radius * pulse;
            float radiusY = Mathf.Min(radius, rect.height * 0.40f) * pulse;
            for (int index = 0; index < segments; index++)
            {
                float a = index * Mathf.PI * 2f / segments;
                float b = (index + 1) * Mathf.PI * 2f / segments;
                Vector2 start = center + new Vector2(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY);
                Vector2 end = center + new Vector2(Mathf.Cos(b) * radiusX, Mathf.Sin(b) * radiusY);
                AddLine(vh, start, end, thickness, color);
            }
        }

        private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color32 tint)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            int first = vh.currentVertCount;
            AddVertex(vh, start - normal, tint);
            AddVertex(vh, start + normal, tint);
            AddVertex(vh, end + normal, tint);
            AddVertex(vh, end - normal, tint);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color32 tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vh.AddVert(vertex);
        }
    }
}

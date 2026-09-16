using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Sprites;

namespace ChoSiren
{
    /// <summary>
    /// Whole-visual-body pointer feedback for controls whose artwork is baked into the
    /// page's authored board. Attach() adds a "BakedRegionBody" overlay inside the hit
    /// button that draws a "RegionArt" slice of the board texture plus a white
    /// "RegionSheen" copy, and drives the button's existing children in place —
    /// children are NEVER reparented, so Transform.Find / LabelOf / sibling order and
    /// every dynamic-label code path stay exactly as authored. Scaling a shared group
    /// about the region centre and scaling each child about that same centre are the
    /// same transform; the component writes child.position/localScale each frame from
    /// captured baselines (with drift re-sync, same convention as TactileVisualMotion).
    ///
    /// Seam discipline: the slice's outer edge fades alpha over a few board pixels via
    /// the vertex ramp in <see cref="BakedRegionGraphic038"/>, and the motion scale
    /// NEVER drops below 1 — idle is pixel-identical to the board underneath, hover
    /// grows the real body, press settles from the hover enlargement toward base plus
    /// brightness. The copy always covers the identical source art, so no static or
    /// double frame and no rectangular copied-background seam can appear; texture
    /// alpha is honoured so boards with authored transparent contours keep their
    /// native silhouette.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BakedRegionMotion038 : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public const string BodyName = "BakedRegionBody";

        private sealed class Part
        {
            public RectTransform Rect;
            public Vector3 BaseScale, LastScale;
            public Vector3 BaseWorldPos, LastWorldPos;
            public Vector2 BaseCentreHost;
        }

        private Button button;
        private RectTransform host;
        private RectTransform body;
        private CanvasGroup sheen;
        private Vector2 regionCentreHost;
        private readonly List<Part> parts = new List<Part>();
        private Vector3 bodyBaseScale = Vector3.one;
        private bool hovered, pressed;
        private int state;
        private float elapsed, duration, fromScale = 1f, fromAlpha;
        private float scale = 1f, alpha;
        private bool release;

        /// <summary>Default edge-feather width, in board source pixels.</summary>
        public static BakedRegionMotion038 Attach(GameObject target, Image board,
            Rect regionInBoardPixels, params Transform[] extras)
            => Attach(target, board, regionInBoardPixels, 3f, null, extras);

        /// <param name="target">Hit-only button whose visible body lives in the board art.</param>
        /// <param name="board">The authored board Image this page is built on.</param>
        /// <param name="regionInBoardPixels">The control's baked art bounds in board source
        /// pixels (top-left origin) — the whole silhouette, which may exceed the hit rect.</param>
        /// <param name="featherPixels">Alpha fade width at the overlay edge, in board pixels.</param>
        /// <param name="extras">Optional nodes parented elsewhere on the page that visually
        /// belong to this control; they are animated in place like the button's children.</param>
        public static BakedRegionMotion038 Attach(GameObject target, Image board,
            Rect regionInBoardPixels, float featherPixels, params Transform[] extras)
            => Attach(target, board, regionInBoardPixels, featherPixels, null, extras);

        /// <param name="silhouetteBoardPixels">Optional polygon (board source pixels,
        /// top-left origin) tracing the control's own baked silhouette. When set, the
        /// slice mesh is tessellated and every vertex outside the polygon fades to zero
        /// alpha, so neighbouring plaques/spikes/frames that physically interleave the
        /// rectangular region are NOT dragged. Where silhouettes interleave, prefer
        /// cutting a tip of the control's own art over covering a neighbour's.</param>
        public static BakedRegionMotion038 Attach(GameObject target, Image board,
            Rect regionInBoardPixels, float featherPixels, Vector2[] silhouetteBoardPixels,
            params Transform[] extras)
        {
            if (target == null || board == null || board.sprite == null || board.sprite.texture == null)
                return null;
            RectTransform host = target.transform as RectTransform;
            if (host == null) return null;

            RectTransform boardRect = board.rectTransform;
            Sprite sprite = board.sprite;
            Rect boardLocal = boardRect.rect;
            if (boardLocal.width <= .01f || boardLocal.height <= .01f
                || sprite.rect.width <= .01f || sprite.rect.height <= .01f) return null;
            // A traced silhouette may legitimately reach the board edge; the slice itself
            // must never sample outside the source rect or it would smear edge pixels.
            regionInBoardPixels = Rect.MinMaxRect(
                Mathf.Max(0f, regionInBoardPixels.xMin), Mathf.Max(0f, regionInBoardPixels.yMin),
                Mathf.Min(sprite.rect.width, regionInBoardPixels.xMax),
                Mathf.Min(sprite.rect.height, regionInBoardPixels.yMax));
            if (regionInBoardPixels.width <= .01f || regionInBoardPixels.height <= .01f) return null;
            float fit = Mathf.Min(boardLocal.width / sprite.rect.width,
                boardLocal.height / sprite.rect.height);
            Vector2 boardCentre = new Vector2(boardLocal.width * (.5f - boardRect.pivot.x),
                boardLocal.height * (.5f - boardRect.pivot.y));

            // Region centre: board source px (top-left origin) -> board local -> world -> host local.
            Vector2 regionCentrePx = new Vector2(regionInBoardPixels.x + regionInBoardPixels.width * .5f,
                regionInBoardPixels.y + regionInBoardPixels.height * .5f);
            Vector2 centreOnBoard = boardCentre + new Vector2(
                (regionCentrePx.x - sprite.rect.width * .5f) * fit,
                (sprite.rect.height * .5f - regionCentrePx.y) * fit);
            Vector3 centreWorld = boardRect.TransformPoint(new Vector3(centreOnBoard.x, centreOnBoard.y, 0f));
            Vector2 centreInHost = host.InverseTransformPoint(centreWorld);

            float worldPerBoardPixel = fit * boardRect.lossyScale.x;
            float unitScale = host.lossyScale.x > 0.0001f
                ? worldPerBoardPixel / host.lossyScale.x : 1f;
            Vector2 sizeInHost = regionInBoardPixels.size * unitScale;

            Rect hit = host.rect;
            // Idempotent re-attach: drop a previous overlay container before rebuilding.
            Transform existingBody = host.Find(BodyName);
            if (existingBody != null) Destroy(existingBody.gameObject);

            GameObject group = new GameObject(BodyName, typeof(RectTransform));
            RectTransform body = (RectTransform)group.transform;
            body.SetParent(host, false);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = body.offsetMax = Vector2.zero;
            body.pivot = hit.width > .01f && hit.height > .01f
                ? new Vector2(
                    Mathf.Clamp01(centreInHost.x / hit.width + host.pivot.x),
                    Mathf.Clamp01(centreInHost.y / hit.height + host.pivot.y))
                : new Vector2(.5f, .5f);
            // Overlay renders UNDER the button's live children: the opaque slice must
            // never cover labels/portraits; it only has to sit above the board art.
            body.SetAsFirstSibling();

            // centreInHost is measured from the host pivot; a centre-anchored child is
            // positioned from the rect centre — convert between the two origins.
            Vector2 centreAnchorOffset = new Vector2((host.pivot.x - .5f) * hit.width,
                (host.pivot.y - .5f) * hit.height);
            Vector2 artCentre = centreInHost + centreAnchorOffset;

            Rect source = sprite.rect;
            Rect slice = new Rect(source.x + regionInBoardPixels.x,
                source.y + source.height - regionInBoardPixels.y - regionInBoardPixels.height,
                regionInBoardPixels.width, regionInBoardPixels.height);
            Sprite region = Sprite.Create(sprite.texture, slice, new Vector2(.5f, .5f), 100f);
            float featherLocal = Mathf.Max(0f, featherPixels * unitScale);
            Vector2[] silhouetteLocal = null;
            if (silhouetteBoardPixels != null && silhouetteBoardPixels.Length >= 3
                && regionInBoardPixels.width > .01f && regionInBoardPixels.height > .01f)
            {
                silhouetteLocal = new Vector2[silhouetteBoardPixels.Length];
                for (int i = 0; i < silhouetteBoardPixels.Length; i++)
                {
                    Vector2 p = silhouetteBoardPixels[i];
                    // Board px (top-left origin) -> region-local units (bottom-left origin,
                    // matching RectTransform.rect space the mesh is generated in).
                    silhouetteLocal[i] = new Vector2(
                        (p.x - regionInBoardPixels.x) * unitScale,
                        (regionInBoardPixels.y + regionInBoardPixels.height - p.y) * unitScale);
                }
            }

            GameObject art = new GameObject("RegionArt", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(BakedRegionGraphic038));
            RectTransform artRect = (RectTransform)art.transform;
            artRect.SetParent(body, false);
            artRect.anchorMin = artRect.anchorMax = new Vector2(.5f, .5f);
            artRect.pivot = new Vector2(.5f, .5f);
            artRect.anchoredPosition = artCentre;
            artRect.sizeDelta = sizeInHost;
            BakedRegionGraphic038 artGraphic = art.GetComponent<BakedRegionGraphic038>();
            artGraphic.Region = region;
            artGraphic.Feather = featherLocal;
            artGraphic.Silhouette = silhouetteLocal;
            artGraphic.raycastTarget = false;
            art.transform.SetAsFirstSibling();

            GameObject sheenNode = new GameObject("RegionSheen", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(BakedRegionGraphic038), typeof(CanvasGroup));
            RectTransform sheenRect = (RectTransform)sheenNode.transform;
            sheenRect.SetParent(body, false);
            sheenRect.anchorMin = sheenRect.anchorMax = new Vector2(.5f, .5f);
            sheenRect.pivot = new Vector2(.5f, .5f);
            sheenRect.anchoredPosition = artCentre;
            sheenRect.sizeDelta = sizeInHost;
            BakedRegionGraphic038 sheenGraphic = sheenNode.GetComponent<BakedRegionGraphic038>();
            sheenGraphic.Region = region;
            sheenGraphic.Feather = featherLocal;
            sheenGraphic.Silhouette = silhouetteLocal;
            sheenGraphic.color = new Color(1f, 1f, 1f, .9f);
            sheenGraphic.raycastTarget = false;
            CanvasGroup sheenGroup = sheenNode.GetComponent<CanvasGroup>();
            sheenGroup.alpha = 0f;
            sheenGroup.interactable = false;
            sheenGroup.blocksRaycasts = false;
            sheenNode.transform.SetAsLastSibling();

            BakedRegionMotion038 component =
                target.GetComponent<BakedRegionMotion038>() ??
                target.AddComponent<BakedRegionMotion038>();
            component.Configure(host, body, sheenGroup, centreInHost);
            component.CaptureChildren(host, extras);
            return component;
        }

        private void Configure(RectTransform hostRect, RectTransform motionBody,
            CanvasGroup sheenGroup, Vector2 centreHost)
        {
            host = hostRect;
            body = motionBody;
            sheen = sheenGroup;
            regionCentreHost = centreHost;
            bodyBaseScale = body != null ? body.localScale : Vector3.one;
            button = GetComponent<Button>();
            ResetImmediately();
        }

        /// <summary>
        /// Captures the control's live children (plus optional extras) as motion parts.
        /// Hierarchy is not modified — every Find()/LabelOf path keeps working.
        /// </summary>
        private void CaptureChildren(RectTransform hostRect, Transform[] extras)
        {
            parts.Clear();
            for (int i = 0; i < hostRect.childCount; i++)
            {
                Transform child = hostRect.GetChild(i);
                if (child == body || child.name == "InteractionFx"
                    || child.name == "ButtonInteractionFx") continue;
                // Nested interactive controls keep their own hit geometry: a child
                // with its own Selectable (button/field/toggle) is never dragged by
                // the parent's body motion, so its hit rect stays fixed.
                if (child.GetComponent<Selectable>() != null) continue;
                AddPart(child as RectTransform);
            }
            if (extras != null)
                foreach (Transform extra in extras)
                    if (extra != null && extra != body
                        && extra.GetComponent<Selectable>() == null) AddPart(extra as RectTransform);
        }

        private void AddPart(RectTransform rect)
        {
            if (rect == null) return;
            Vector3 worldCentre = rect.TransformPoint(rect.rect.center);
            parts.Add(new Part
            {
                Rect = rect,
                BaseScale = rect.localScale,
                LastScale = rect.localScale,
                BaseWorldPos = rect.position,
                LastWorldPos = rect.position,
                BaseCentreHost = host.InverseTransformPoint(worldCentre),
            });
        }

        private void OnEnable() => ResetImmediately();
        private void OnDisable() => ResetImmediately();

        private void LateUpdate()
        {
            if (body == null || host == null) return;
            if (button != null && (!button.enabled || !button.IsInteractable()))
            { ResetImmediately(); return; }
            int target = pressed ? 2 : hovered ? 1 : 0;
            if (state != target)
            {
                release = state == 2 && target == 1;
                state = target; elapsed = 0f;
                duration = target == 2 ? .08f : target == 0 ? .12f : .16f;
                fromScale = scale; fromAlpha = alpha;
            }
            elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
            float t = duration <= 0f ? 1f : elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float spring = release ? 1f + 1.65f * Mathf.Pow(t - 1f, 3f) + .65f * Mathf.Pow(t - 1f, 2f) : eased;
            // Scale floor is 1: the overlay always covers the identical baked art, so a
            // press can never shrink the copy inside its own static frame.
            float targetScale = target == 2 ? 1.006f : target == 1 ? 1.03f : 1f;
            scale = Mathf.Max(1f, Mathf.LerpUnclamped(fromScale, targetScale, spring));
            alpha = Mathf.Lerp(fromAlpha, target == 2 ? .55f : target == 1 ? .30f : 0f, eased);
            body.localScale = bodyBaseScale * scale;
            if (sheen != null) sheen.alpha = alpha;

            float shift = scale - 1f;
            foreach (Part part in parts)
            {
                if (part.Rect == null) continue;
                Vector3 applied = host.TransformVector(
                    new Vector3((part.BaseCentreHost.x - regionCentreHost.x) * shift,
                        (part.BaseCentreHost.y - regionCentreHost.y) * shift, 0f));
                Vector3 expectedPos = part.BaseWorldPos + applied;
                Vector3 expectedScale = part.BaseScale * scale;
                // An external layout change becomes the new baseline (TactileVisualMotion rule).
                if ((part.Rect.position - part.LastWorldPos).sqrMagnitude > .000001f
                    || (part.Rect.localScale - part.LastScale).sqrMagnitude > .000001f)
                {
                    part.BaseWorldPos = part.Rect.position - applied;
                    part.BaseScale = part.Rect.localScale / Mathf.Max(scale, .0001f);
                    expectedPos = part.Rect.position;
                    expectedScale = part.Rect.localScale;
                }
                part.LastWorldPos = expectedPos;
                part.LastScale = expectedScale;
                part.Rect.position = expectedPos;
                part.Rect.localScale = expectedScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && (!button.enabled || !button.IsInteractable())) return;
            hovered = true;
        }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left
                && (button == null || button.enabled && button.IsInteractable())) pressed = true;
        }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void ResetImmediately()
        {
            hovered = pressed = false;
            state = 0; elapsed = duration = 0f; release = false;
            scale = fromScale = 1f; alpha = fromAlpha = 0f;
            if (body != null) body.localScale = bodyBaseScale;
            if (sheen != null) sheen.alpha = 0f;
            foreach (Part part in parts)
            {
                if (part.Rect == null) continue;
                part.Rect.position = part.BaseWorldPos;
                part.Rect.localScale = part.BaseScale;
                part.LastWorldPos = part.BaseWorldPos;
                part.LastScale = part.BaseScale;
            }
        }
    }

    /// <summary>
    /// Board-slice image whose outermost pixels fade to zero alpha through a vertex
    /// ramp (no shader asset, no readable-texture requirement). UVs map linearly across
    /// the whole rect, so at scale 1 the visible pixels match the underlying board
    /// exactly; only the alpha ramp softens the rectangular cut when the body is
    /// scaled past its baked frame. Texture alpha is sampled normally, preserving any
    /// authored transparent contour inside the slice.
    /// </summary>
    public sealed class BakedRegionGraphic038 : MaskableGraphic
    {
        public Sprite Region;
        /// <summary>Edge fade width in local units.</summary>
        public float Feather = 3f;
        /// <summary>Optional silhouette polygon in local rect space (bottom-left origin).
        /// When set, the quad is tessellated and each vertex fades to zero alpha outside
        /// the polygon, isolating the slice so neighbouring baked art is not dragged.</summary>
        public Vector2[] Silhouette;

        public override Texture mainTexture =>
            Region != null ? Region.texture : base.mainTexture;

        public void SetRegion(Sprite region, float feather)
        {
            Region = region; Feather = feather;
            SetVerticesDirty(); SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f || Region == null) return;
            if (Silhouette != null && Silhouette.Length >= 3)
            {
                PopulateSilhouetteMesh(vh, r);
                return;
            }
            Vector4 uv = DataUtility.GetOuterUV(Region);
            float fx = Mathf.Min(Feather, r.width * .25f);
            float fy = Mathf.Min(Feather, r.height * .25f);

            Color32 outer = color; outer.a = 0;
            Color32 inner = color;
            int first = vh.currentVertCount;
            // Verts 0-3 outer ring (alpha 0): BL, BR, TR, TL; verts 4-7 inner (alpha 1).
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2((i & 1) == 0 ? r.xMin : r.xMax,
                    i < 2 ? r.yMin : r.yMax);
                AddVert(vh, p, UvAt(r, uv, p), outer);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2((i & 1) == 0 ? r.xMin + fx : r.xMax - fx,
                    i < 2 ? r.yMin + fy : r.yMax - fy);
                AddVert(vh, p, UvAt(r, uv, p), inner);
            }
            // Clockwise winding (Unity UI front faces). Each ring quad lists its
            // corners SW, NW, NE, SE and emits (a,b,c),(c,d,a).
            // Inner core: iBL, iTL, iTR, iBR.
            vh.AddTriangle(first + 4, first + 7, first + 6);
            vh.AddTriangle(first + 6, first + 5, first + 4);
            // Bottom ring: oBL, iBL, iBR, oBR.
            vh.AddTriangle(first + 0, first + 4, first + 5);
            vh.AddTriangle(first + 5, first + 1, first + 0);
            // Right ring: oBR, iBR, iTR, oTR.
            vh.AddTriangle(first + 1, first + 5, first + 6);
            vh.AddTriangle(first + 6, first + 2, first + 1);
            // Top ring: oTR, iTR, iTL, oTL.
            vh.AddTriangle(first + 2, first + 6, first + 7);
            vh.AddTriangle(first + 7, first + 3, first + 2);
            // Left ring: oBL, oTL, iTL, iBL.
            vh.AddTriangle(first + 0, first + 3, first + 7);
            vh.AddTriangle(first + 7, first + 4, first + 0);
        }

        /// <summary>
        /// Tessellates the rect into a grid; every vertex carries alpha = soft polygon
        /// coverage (signed distance / feather), so art outside the silhouette never
        /// renders and therefore never moves. Cell size is capped so the soft edge stays
        /// within a few board pixels of the authored contour.
        /// </summary>
        private void PopulateSilhouetteMesh(VertexHelper vh, Rect r)
        {
            Vector4 uv = DataUtility.GetOuterUV(Region);
            int cols = Mathf.Clamp(Mathf.CeilToInt(r.width / 14f), 8, 40);
            int rows = Mathf.Clamp(Mathf.CeilToInt(r.height / 14f), 8, 40);
            float fade = Mathf.Max(Feather, 1.5f);
            int first = vh.currentVertCount;
            for (int row = 0; row <= rows; row++)
            {
                for (int col = 0; col <= cols; col++)
                {
                    Vector2 p = new Vector2(
                        Mathf.Lerp(r.xMin, r.xMax, (float)col / cols),
                        Mathf.Lerp(r.yMin, r.yMax, (float)row / rows));
                    // Mesh points live in pivot-centred rect space; the silhouette is
                    // region-local with a bottom-left origin — shift before testing.
                    float d = SignedDistanceToPolygon(new Vector2(p.x - r.xMin, p.y - r.yMin));
                    Color32 tint = color;
                    tint.a = (byte)Mathf.Clamp(Mathf.RoundToInt(
                        tint.a * Mathf.Clamp01((d + fade) / (fade * 2f))), 0, 255);
                    AddVert(vh, p, UvAt(r, uv, p), tint);
                }
            }
            int stride = cols + 1;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int bl = first + row * stride + col;
                    int br = bl + 1, tl = bl + stride, tr = tl + 1;
                    vh.AddTriangle(bl, tl, tr);
                    vh.AddTriangle(tr, br, bl);
                }
            }
        }

        /// <summary>Signed distance to the silhouette polygon: positive inside.
        /// <paramref name="p"/> is in the same space as <see cref="Silhouette"/> —
        /// region-local units with a bottom-left origin.</summary>
        private float SignedDistanceToPolygon(Vector2 p)
        {
            float minSq = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = Silhouette.Length - 1; i < Silhouette.Length; j = i++)
            {
                Vector2 a = Silhouette[j], b = Silhouette[i];
                float dx = b.x - a.x, dy = b.y - a.y;
                float t = dx * dx + dy * dy < .0001f ? 0f
                    : Mathf.Clamp01(((p.x - a.x) * dx + (p.y - a.y) * dy) / (dx * dx + dy * dy));
                float ex = a.x + dx * t - p.x, ey = a.y + dy * t - p.y;
                float sq = ex * ex + ey * ey;
                if (sq < minSq) minSq = sq;
                if (a.y > p.y != b.y > p.y
                    && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            float d = Mathf.Sqrt(minSq);
            return inside ? d : -d;
        }

        private static Vector2 UvAt(Rect r, Vector4 uv, Vector2 p) => new Vector2(
            Mathf.Lerp(uv.x, uv.z, Mathf.InverseLerp(r.xMin, r.xMax, p.x)),
            Mathf.Lerp(uv.y, uv.w, Mathf.InverseLerp(r.yMin, r.yMax, p.y)));

        private static void AddVert(VertexHelper vh, Vector2 p, Vector2 uv, Color32 tint)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p; v.color = tint; v.uv0 = uv;
            vh.AddVert(v);
        }
    }
}

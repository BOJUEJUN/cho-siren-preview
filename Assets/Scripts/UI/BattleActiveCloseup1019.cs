using System;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Brief face close-up for a real small active skill. One current cue; no stale backlog.
    /// 2026-11-01 (赵金 "这个贴脸图大一点"): the close-up is a full foreground beat — the member's own PSD paper
    /// cut-in, uniformly enlarged from under the top HUD down to the member cards' top edge (≈97% of the width),
    /// over a soft ink veil that sinks the enemy stage behind it. Members without authored paper art get a
    /// torn-paper card in the same style with their own illustration rising out of it.
    /// </summary>
    public sealed class BattleActiveCloseup1019 : MonoBehaviour
    {
        public const float Duration = .9f;
        private const float S = 720f / 2946f;
        /// <summary>Highest edge of the cut-in region: just under the HUD's health bar (canvas units, from the stage top).</summary>
        public const float RegionTop = 190f;
        /// <summary>The member cards start at PSD y 3670; nothing of the cut-in paints below their top edge.</summary>
        public const float RegionBottom = 3670f * S;
        /// <summary>On-screen width an authored cut-in is enlarged to (≈97% of the 720-unit stage).</summary>
        public const float AuthoredWidth = 700f;
        private const float ClipTop = RegionTop - 22f;

        RectTransform banner, clip, motion, window, portrait, veil, plateRoot;
        RawImage actor;
        Image plate, accent, veilImage;
        Graphic paperShadow, paperEdge, paper, paperStreak, tape;
        Text title, nameLabel;
        CanvasGroup group, plateGroup;
        PanelKit kit;
        Func<bool> paused;
        Func<int> speed;
        float elapsed, gap;
        float viewportLeft, viewportRight = 720f;
        int enterSide = 1;   // +1 enters from the right, -1 from the left
        readonly Vector3[] edgeCorners1102 = new Vector3[4];
        public bool IsShowing { get; private set; }
        public float VisualClock { get; private set; }
        public bool UsesAuthoredCutin { get; private set; }
        /// <summary>The full-stage root of the cue (re-parented by the battle into its foreground layer).</summary>
        public RectTransform Root => banner;
        /// <summary>Settled on-screen rectangle of the character art, in stage canvas units (x, y from the top-left).</summary>
        public Rect ArtRect { get; private set; }

        public void Configure(RectTransform host, Func<bool> pause, Func<int> playbackSpeed)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            paused = pause; speed = playbackSpeed;
            if (banner != null) return;
            kit = new PanelKit("ActiveCloseup1019");
            banner = kit.NewRect("ActiveSkillCloseup", host);
            PanelKit.PlaceTop(banner, 0, 0, 720, 6144f * S);
            group = banner.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;

            // Ink veil: sinks the enemy stage (figures, plates, rings, bubbles) behind the cut-in; soft at both ends
            // and never over the HUD above or the member cards below.
            veilImage = kit.NewImage("ForegroundVeil", banner, VeilSprite(), new Color32(10, 5, 26, 148));
            veil = veilImage.rectTransform;
            PanelKit.PlaceTop(veil, 0, RegionTop - 24f, 720, RegionBottom - (RegionTop - 24f));

            clip = kit.NewRect("CutinClip", banner);
            PanelKit.PlaceTop(clip, 0, ClipTop, 720, RegionBottom + 2f - ClipTop);
            // No RectMask2D anywhere in the cue: the art is cropped through its UVs instead (see ShowCropped), so a
            // failing clipper elsewhere on the canvas can never leave the close-up clipped away.
            motion = kit.NewRect("CutinMotion", clip);
            PanelKit.Stretch(motion);

            // Fallback paper (members without authored cut-in art).
            paperShadow = Paper<UI.TornPaper1025>("FallbackPaperShadow", Brand1025.Ink);
            paperEdge = Paper<UI.TornPaper1025>("FallbackPaperEdge", new Color32(250, 247, 255, 255));
            paper = Paper<UI.TornPaper1025>("FallbackPaper", new Color32(122, 216, 255, 255));
            paperStreak = Paper<UI.TornPaper1025>("FallbackPaperStreak", new Color32(46, 118, 236, 150));

            window = kit.NewRect("CloseupWindow", motion);
            actor = new GameObject("OriginalCharacterCloseup", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            actor.transform.SetParent(window, false);
            portrait = actor.rectTransform;
            tape = Paper<Image>("FallbackTape", new Color32(255, 70, 196, 225));

            // Skill name board (fallback only): sits on the paper's lower left, slides in on its own beat.
            plateRoot = kit.NewRect("CloseupTitleBoard", banner);
            PanelKit.PlaceTop(plateRoot, 0, 0, 720, 6144f * S);
            plateGroup = plateRoot.gameObject.AddComponent<CanvasGroup>();
            plate = kit.NewImage("SlantedBackdrop", plateRoot, PunkUi.SlantFill, new Color32(6, 4, 19, 242));
            accent = kit.NewImage("Accent", plateRoot, PunkUi.SlantFill, PanelKit.Pink);
            nameLabel = kit.NewPlacedText(plateRoot, string.Empty, 22, PanelKit.Pink, 0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
            title = kit.NewPlacedText(plateRoot, string.Empty, 44, Color.white, 0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 24; title.resizeTextMaxSize = 44;
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
            foreach (Text t in new[] { title, nameLabel })
            {
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = Brand1025.Ink; outline.effectDistance = new Vector2(2f, -2f);
            }
            foreach (var g in banner.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            banner.gameObject.SetActive(false);
        }

        private T Paper<T>(string name, Color color) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(motion, false);
            var g = go.GetComponent<T>();
            g.color = color; g.raycastTarget = false;
            return g;
        }

        public bool Play(Sprite croppedOriginal, string actorName, string skillName, Color color, bool authoredCutin = false,
            string memberId = null)
        {
            if (banner == null || croppedOriginal == null || !isActiveAndEnabled || IsShowing || gap > 0 ||
                (paused?.Invoke() ?? false) || BattlePresentationOptions.ReduceMotion || string.IsNullOrEmpty(skillName)) return false;
            LayoutArt(croppedOriginal, authoredCutin, memberId);
            nameLabel.text = actorName ?? string.Empty; title.text = skillName;
            nameLabel.color = accent.color = color;
            elapsed = 0; IsShowing = true; banner.gameObject.SetActive(true); Pose(); return true;
        }

        /// <summary>Content rows of each PSD paper group (fraction of the image height; transparent margins excluded).</summary>
        public static void AuthoredContent(string key, out float top, out float bottom)
        {
            switch (key)
            {
                case "cutin-demon": top = .077f; bottom = .80f; return;
                case "cutin-charm": top = .044f; bottom = .995f; return;
                case "cutin-mermaid": top = .08f; bottom = .991f; return;
                case "cutin-bloodelf": top = .007f; bottom = .996f; return;
                default: top = 0f; bottom = 1f; return;
            }
        }

        /// <summary>
        /// Settled rectangle (stage canvas units, y from the top) of an authored PSD cut-in: the PSD group is scaled
        /// uniformly (never stretched) until its on-screen part is <see cref="AuthoredWidth"/> wide or its content spans
        /// the whole region; the paper keeps the screen edge it was drawn against and its content stands on the cards.
        /// </summary>
        public static Rect AuthoredLayout(string key, float psdX, float psdY, float psdWidth, float psdHeight)
        {
            AuthoredContent(key, out float top, out float bottom);
            const float stage = 2946f;
            float onScreen = (Mathf.Min(stage, psdX + psdWidth) - Mathf.Max(0f, psdX)) * S;
            float content = psdHeight * S * Mathf.Max(.2f, bottom - top);
            float factor = Mathf.Min(AuthoredWidth / Mathf.Max(1f, onScreen), (RegionBottom - RegionTop) / Mathf.Max(1f, content));
            float w = psdWidth * S * factor, h = psdHeight * S * factor;
            bool rightEdge = psdX + psdWidth >= stage - 24f;
            float x = rightEdge
                ? 720f - w * ((stage - psdX) / psdWidth)            // the same share stays past the right edge
                : -w * (Mathf.Max(0f, -psdX) / psdWidth);            // the left cut stays on the left edge
            // 雾白: discard another ~10% screen width of the left paper through the existing UV crop.
            if (key == "cutin-mermaid") x -= 64f;
            float y = RegionBottom - h * bottom;
            return new Rect(x, y, w, h);
        }

        private void LayoutArt(Sprite art, bool authored, string memberId)
        {
            UsesAuthoredCutin = authored;
            var native = authored ? BattlePsdArt928.Find(art.name) : null;
            bool paperMode = native == null;
            paperShadow.gameObject.SetActive(paperMode); paperEdge.gameObject.SetActive(paperMode);
            paper.gameObject.SetActive(paperMode); paperStreak.gameObject.SetActive(paperMode);
            tape.gameObject.SetActive(paperMode);
            plateRoot.gameObject.SetActive(paperMode);
            foreach (Graphic g in new Graphic[] { plate, accent, nameLabel, title }) g.gameObject.SetActive(paperMode);
            window.localEulerAngles = Vector3.zero;
            if (native != null)
            {
                // The finished PSD groups are different compositions: each keeps its own proportions and the screen
                // edge it was drawn against, only larger. No invented title board beside the supplied art.
                Rect r = AuthoredLayout(native.key, native.x, native.y, native.width, native.height);
                bool fromRight = native.x + native.width >= 2946f - 24f;
                r.x += fromRight ? viewportRight - 720f + 1f : viewportLeft - 1f;
                ArtRect = r;
                // Shown on the stage only, never below the member cards' top edge.
                ShowCropped(art, r, Rect.MinMaxRect(viewportLeft - 1f, ClipTop, viewportRight + 1f, RegionBottom + 2f));
                enterSide = fromRight ? 1 : -1;
                motion.pivot = new Vector2(fromRight ? viewportRight / 720f : viewportLeft / 720f, 0f);
                return;
            }
            LayoutPaperCard(art, memberId);
        }

        /// <summary>Fallback: a torn-paper card across the region, the member's own art rising out of it.</summary>
        private void LayoutPaperCard(Sprite art, string memberId)
        {
            enterSide = 1;
            motion.pivot = new Vector2(1f, 0f);
            // Paper (stage units) — slightly tilted, like the authored sheets.
            var paperRect = new Rect(16f, RegionBottom - 400f, 690f, 384f);
            foreach (Graphic g in new[] { paperShadow, paperEdge, paper })
            {
                Place(g.rectTransform, paperRect);
                PanelKit.CenterPivot(g.rectTransform);
                g.rectTransform.localEulerAngles = new Vector3(0, 0, 4f);
            }
            paperShadow.rectTransform.anchoredPosition += new Vector2(10f, -12f);
            paperEdge.rectTransform.sizeDelta += new Vector2(12f, 12f);
            Place(paperStreak.rectTransform, new Rect(paperRect.x + 40f, paperRect.y + 120f, paperRect.width * .82f, 120f));
            PanelKit.CenterPivot(paperStreak.rectTransform);
            paperStreak.rectTransform.localEulerAngles = new Vector3(0, 0, 13f);
            Place(tape.rectTransform, new Rect(paperRect.x + 40f, paperRect.y - 6f, 120f, 30f));
            PanelKit.CenterPivot(tape.rectTransform);
            tape.rectTransform.localEulerAngles = new Vector3(0, 0, -16f);

            // The character: her bust centred on the paper's right half, head and shoulders above its top edge,
            // the body cut by the paper's lower edge (window mask).
            Rect focus = PanelKit.MemberBustFocus(memberId);
            float sw = art.rect.width, sh = art.rect.height;
            float bustHeight = 360f;
            float scale = bustHeight / Mathf.Max(1f, focus.height * sh);
            scale = Mathf.Min(scale, 760f / Mathf.Max(1f, sw));
            float w = sw * scale, h = sh * scale;
            // Focus centre → (x 486, y paper top + 30) in stage units.
            float fx = (focus.x + focus.width * .5f) * w, fyFromTop = (1f - (focus.y + focus.height * .5f)) * h;
            float left = 486f - fx, topY = paperRect.y + 30f - fyFromTop;
            ArtRect = new Rect(left, topY, w, h);
            ShowCropped(art, ArtRect, Rect.MinMaxRect(0f, RegionTop, 720f, paperRect.yMax - 6f));

            // Name board on the paper's lower left.
            float bx = 34f, by = paperRect.y + 170f;
            PanelKit.PlaceTop(plate.rectTransform, bx, by, 330f, 150f);
            PanelKit.PlaceTop(accent.rectTransform, bx + 14f, by + 136f, 300f, 7f);
            PanelKit.PlaceTop(nameLabel.rectTransform, bx + 26f, by + 14f, 290f, 34f);
            PanelKit.PlaceTop(title.rectTransform, bx + 22f, by + 46f, 296f, 86f);
            plate.rectTransform.localEulerAngles = accent.rectTransform.localEulerAngles = Vector3.zero;
        }

        /// <summary>Visible part of the art (stage units) and its texture sub-rectangle, after cropping.</summary>
        public Rect VisibleRect { get; private set; }
        public Sprite Art { get; private set; }

        /// <summary>
        /// The window takes the whole art rectangle; the RawImage inside it shows only the part inside
        /// <paramref name="allowed"/>, with matching UVs — the same picture a mask would give, no stretching.
        /// </summary>
        private void ShowCropped(Sprite art, Rect artStage, Rect allowed)
        {
            Art = art;
            Place(window, artStage);
            Rect vis = Rect.MinMaxRect(Mathf.Max(artStage.xMin, allowed.xMin), Mathf.Max(artStage.yMin, allowed.yMin),
                Mathf.Min(artStage.xMax, allowed.xMax), Mathf.Min(artStage.yMax, allowed.yMax));
            VisibleRect = vis;
            if (vis.width <= 0f || vis.height <= 0f || art.texture == null) { actor.enabled = false; return; }
            actor.enabled = true;
            float fx0 = (vis.xMin - artStage.xMin) / artStage.width, fx1 = (vis.xMax - artStage.xMin) / artStage.width;
            float fy0 = (vis.yMin - artStage.yMin) / artStage.height, fy1 = (vis.yMax - artStage.yMin) / artStage.height;
            Texture tex = art.texture;
            Rect tr = art.textureRect;
            float u0 = (tr.x + fx0 * tr.width) / tex.width, u1 = (tr.x + fx1 * tr.width) / tex.width;
            float v0 = (tr.y + (1f - fy1) * tr.height) / tex.height, v1 = (tr.y + (1f - fy0) * tr.height) / tex.height;
            actor.texture = tex;
            actor.uvRect = new Rect(u0, v0, u1 - u0, v1 - v0);
            PanelKit.PlaceTop(portrait, vis.xMin - artStage.xMin, vis.yMin - artStage.yMin, vis.width, vis.height);
        }

        /// <summary>Places <paramref name="rect"/> at a stage rectangle, whatever its parent inside the cue.</summary>
        private void Place(RectTransform rect, Rect stageRect)
        {
            PanelKit.PlaceTop(rect, stageRect.x, stageRect.y - (rect.parent == motion || rect.parent == clip ? ClipTop : 0f),
                stageRect.width, stageRect.height);
        }

        void Update() => AdvancePresentation(Mathf.Min(Time.unscaledDeltaTime, .1f));
        public void AdvancePresentation(float seconds)
        {
            if (BattlePresentationOptions.ReduceMotion) { Cancel(); return; }
            if (banner == null || (paused?.Invoke() ?? false) || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float dt = seconds * Mathf.Clamp(speed?.Invoke() ?? 1, 1, 1.5f) * BattlePresentationOptions.Pace;
            VisualClock += dt;
            if (!IsShowing) { gap = Mathf.Max(0, gap - dt); return; }
            elapsed += dt;
            if (elapsed >= Duration) { IsShowing = false; banner.gameObject.SetActive(false); gap = .65f; }
            else Pose();
        }

        /// <summary>0 → 1 → 0 strength of the foreground beat (the battle fades enemy readouts by it).</summary>
        public float Presence => IsShowing ? Mathf.Clamp01(Mathf.Min(elapsed / .08f, (Duration - elapsed) / .14f)) : 0f;
        /// <summary>True until the cut-in starts slipping away; hits that land meanwhile wait to show their numbers.</summary>
        public bool HoldsForeground => IsShowing && elapsed < Duration - .16f;

        void Pose()
        {
            // Slam in (≈0.12) with a little overshoot, a slow push while it holds, then slip out (last ≈0.16).
            float t = Mathf.Clamp01(elapsed / .12f);
            float enter = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = Mathf.Sin(t * Mathf.PI) * .035f;
            float exit = Mathf.Clamp01((elapsed - (Duration - .16f)) / .16f);
            exit = exit * exit;
            group.alpha = Mathf.Clamp01(t * 2.4f) * (1f - exit);
            float push = Mathf.Clamp01(elapsed / Duration) * .025f;
            motion.anchoredPosition = new Vector2(enterSide * (120f * (1f - enter) + 40f * exit), -10f * (1f - enter));
            float scale = 1.06f - .06f * enter + overshoot + push + exit * .03f;
            motion.localScale = new Vector3(scale, scale, 1f);
            // Source crops are not always exactly on the PSD edge. Keep the rendered crop,
            // including its animation transform, outside the actual viewport on every frame.
            if (UsesAuthoredCutin && actor.enabled)
            {
                portrait.GetWorldCorners(edgeCorners1102);
                float edge = clip.InverseTransformPoint(edgeCorners1102[enterSide > 0 ? 2 : 0]).x;
                float target = enterSide > 0 ? viewportRight + 1f : viewportLeft - 1f;
                float correction = enterSide > 0 ? Mathf.Max(0f, target - edge) : Mathf.Min(0f, target - edge);
                motion.anchoredPosition += Vector2.right * correction;
            }
            veilImage.canvasRenderer.SetAlpha(Mathf.Clamp01(elapsed / .1f) * (1f - exit));   // no mesh rebuild per frame
            if (plateRoot.gameObject.activeSelf)
            {
                float b = Mathf.Clamp01((elapsed - .05f) / .12f);
                float be = 1f - Mathf.Pow(1f - b, 3f);
                plateRoot.anchoredPosition = new Vector2(-160f * (1f - be) - 30f * exit, 0f);
                plateGroup.alpha = b;
            }
        }

        public void SetViewportEdges(float left, float right)
        {
            if (right <= left || (Mathf.Abs(viewportLeft - left) < .01f && Mathf.Abs(viewportRight - right) < .01f)) return;
            viewportLeft = left; viewportRight = right;
            PanelKit.PlaceTop(veil, left, RegionTop - 24f, right - left, RegionBottom - (RegionTop - 24f));
            if (IsShowing && UsesAuthoredCutin && Art != null) { LayoutArt(Art, true, null); Pose(); }
        }

        public void Cancel() { IsShowing = false; elapsed = gap = 0; if (banner != null) banner.gameObject.SetActive(false); }
        void OnDisable() => Cancel();
        void OnDestroy() { if (banner != null) Destroy(banner.gameObject); kit?.Dispose(); }

        static Sprite veilSprite;
        /// <summary>Vertical alpha ramp (soft top, solid middle, soft bottom) for the ink veil; built once.</summary>
        static Sprite VeilSprite()
        {
            if (veilSprite != null) return veilSprite;
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CloseupVeil1101" };
            var px = new Color32[h];
            for (int i = 0; i < h; i++)
            {
                float y = i / (h - 1f);            // 0 bottom … 1 top
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(y / .1f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - y) / .22f));
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            veilSprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(.5f, .5f), 100f);
            veilSprite.name = "CloseupVeil1101";
            return veilSprite;
        }
    }
}

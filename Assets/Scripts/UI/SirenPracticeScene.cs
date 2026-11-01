using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 practice stage (练习室舞台). Every owned member stands on the stellar stage as a
    /// sticker figure — the four story leads use their Q chibi art, everyone else their own full-body
    /// thumbnail (never a borrowed chibi). Tapping a figure picks the trainee: it steps forward under
    /// a spotlight with a teal glow outline, a foot ring and a small bounce while the rest idle. More
    /// than four members page horizontally (drag or ‹ ›). Reduce-motion freezes every loop but keeps
    /// the same selected state; the stage never changes the economy, it only reports the pick.
    /// </summary>
    public sealed class SirenPracticeScene : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public const string ArtRoot = "Art/SirenSticker20261013/";
        public const int PerPage = 4;
        private const string StageArt = "Art/TeamAI/team-stellar-stage-bg-ai-v1-20260903";
        private const float FeetY = 66f, TitleBand = 84f, FloorFraction = .58f;
        private static readonly string[] CastIds = { "xingli", "feiyin", "wubai", "yeying" };
        private static readonly Color TealGlow = new Color32(28, 242, 236, 255);
        private static readonly Color Hot = new Color32(255, 46, 147, 255);
        private static readonly Color Ground = new Color32(9, 6, 22, 255);

        private sealed class Actor
        {
            public int Member;
            public string Id;
            public bool Chibi;
            public RectTransform Root, Body, Figure;
            public Image FigureImage, Shadow, Pool, Ring, Ripple, Plate, Tag;
            public Text Name, TagText;
            public Button Button;
            public CanvasGroup Group;
            public bool PendingArt;
            public float Sel, Phase, BaseX;
        }

        private readonly List<Actor> actors = new List<Actor>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private readonly List<Image> glowLayers = new List<Image>();
        private readonly List<Image> dots = new List<Image>();
        private readonly List<Text> notes = new List<Text>();
        private readonly List<float> noteBirth = new List<float>();
        private readonly List<Vector2> noteFrom = new List<Vector2>();
        private RectTransform rect, cast, viewport, glowRoot, notesRoot;
        private Image backdrop, spotlight, floorGlow, halo, topFade, bottomFade;
        private Image tornPaper, tornShadow;
        private Button prev, next;
        private ScrollRect scroll;
        private Font font;
        private GameModel model;
        private Action<int> onPick;
        private int selected = -1;
        private bool practicing, reduceMotion, dragging, poseDirty = true, lastStill;
        private float stageW = 720f, stageH = 480f, slotW = 170f, pad = 20f, figureH = 260f;
        private float targetX, spotX, nextNoteAt;
        private int page;

        public int MissingArtCount { get; private set; }
        public bool HasCompleteCast => MissingArtCount == 0;
        public int SelectedMember => selected >= 0 && selected < actors.Count ? actors[selected].Member : -1;
        public int Count => actors.Count;
        public int Page => page;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(actors.Count / (float)PerPage));
        public bool Practicing => practicing;

        public IReadOnlyList<int> Roster
        {
            get
            {
                var list = new List<int>(actors.Count);
                foreach (Actor actor in actors) list.Add(actor.Member);
                return list;
            }
        }

        /// <summary>Owned members, team order first (captain leads), then the rest of the roster.</summary>
        public static List<int> OwnedRoster(GameModel gameModel, int mustInclude)
        {
            var list = new List<int>();
            if (gameModel != null)
            {
                foreach (int index in gameModel.Save.Team)
                    if (gameModel.IsUnlocked(index) && !list.Contains(index)) list.Add(index);
                foreach (int index in gameModel.Save.UnlockedMembers)
                    if (index >= 0 && index < GameModel.Members.Length && !list.Contains(index)) list.Add(index);
            }

            if (mustInclude >= 0 && mustInclude < GameModel.Members.Length && !list.Contains(mustInclude))
                list.Insert(0, mustInclude);
            return list;
        }

        public static SirenPracticeScene Build(Transform parent, Font font, GameModel gameModel,
            IList<int> roster, int selectedMember, Action<int> picked)
        {
            // Its own canvas: the idle loop only re-batches the stage, never the static cards (WebGL).
            var node = new GameObject("PracticeStage", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster),
                typeof(RectMask2D), typeof(ScrollRect), typeof(SirenPracticeScene));
            node.transform.SetParent(parent, false);
            var scene = node.GetComponent<SirenPracticeScene>();
            scene.font = font;
            scene.model = gameModel;
            scene.onPick = picked;
            scene.BuildStage(roster);
            scene.SetSelected(selectedMember, true);
            return scene;
        }

        // ------------------------------------------------------------------ public control

        public void SetPracticing(bool active, bool reduced)
        {
            practicing = active;
            reduceMotion = reduced;
            poseDirty = true;
            if (!active) HideNotes();
            if (Still) ApplyPose(0f, true);
        }

        public bool SetSelected(int member, bool instant = false)
        {
            int index = actors.FindIndex(actor => actor.Member == member);
            if (index < 0) return false;
            selected = index;
            Actor chosen = actors[index];
            EnsureFigure(chosen);
            poseDirty = true;
            chosen.Root.SetAsLastSibling();
            MoveGlowTo(chosen);
            for (int i = 0; i < actors.Count; i++) StylePlate(actors[i], i == index);
            ShowPage(index / PerPage, instant);
            if (instant || Still || !Application.isPlaying) ApplyPose(0f, true);
            return true;
        }

        /// <summary>Refreshes the Lv. labels and team tags after a level-up or roster change.</summary>
        public void RefreshLabels()
        {
            for (int i = 0; i < actors.Count; i++) StylePlate(actors[i], i == selected);
        }

        public RectTransform ActorRect(int member)
        {
            Actor actor = actors.Find(a => a.Member == member);
            return actor != null ? actor.Root : null;
        }

        public float SelectionAmount(int member)
        {
            Actor actor = actors.Find(a => a.Member == member);
            return actor != null ? actor.Sel : 0f;
        }

        public void ShowPage(int value, bool instant = false)
        {
            page = Mathf.Clamp(value, 0, PageCount - 1);
            float contentW = ContentWidth;
            targetX = -Mathf.Min(page * PerPage * slotW, Mathf.Max(0f, contentW - stageW));
            if (instant && cast != null) cast.anchoredPosition = new Vector2(targetX, 0f);
            if (scroll != null) scroll.velocity = Vector2.zero;
            poseDirty = true;
            RefreshPager();
        }

        /// <summary>Re-fits backdrop, figures and paging to the stage size the panel assigns.</summary>
        public void Relayout(float width, float height)
        {
            stageW = Mathf.Max(320f, width);
            stageH = Mathf.Max(240f, height);
            if (rect == null) return;
            rect.sizeDelta = new Vector2(stageW, stageH);

            if (backdrop != null && backdrop.sprite != null)
            {
                float bgH = stageW * backdrop.sprite.rect.height / backdrop.sprite.rect.width;
                float feetFromTop = stageH - FeetY;
                float shiftUp = Mathf.Clamp(FloorFraction * bgH - feetFromTop, 0f, Mathf.Max(0f, bgH - stageH));
                PracticeFx1101.Place(backdrop.rectTransform, 0f, -shiftUp, stageW, bgH);
            }

            figureH = Mathf.Clamp((stageH - FeetY - TitleBand) * .8f, 150f, 290f);
            bool paged = actors.Count > PerPage;
            pad = paged ? 46f : 0f;
            slotW = paged ? (stageW - pad * 2f) / PerPage : Mathf.Min(176f, (stageW - 40f) / Mathf.Max(1, actors.Count));
            if (!paged) pad = (stageW - slotW * actors.Count) * .5f;

            cast.sizeDelta = new Vector2(ContentWidth, stageH);
            for (int i = 0; i < actors.Count; i++) LayoutActor(actors[i], i);

            PracticeFx1101.Place(topFade.rectTransform, 0f, 0f, stageW, 150f);
            topFade.rectTransform.pivot = new Vector2(0f, .5f);
            topFade.rectTransform.anchoredPosition = new Vector2(0f, -75f);
            PracticeFx1101.Place(bottomFade.rectTransform, 0f, stageH - 120f, stageW, 120f);
            PracticeFx1101.PlaceCenter(floorGlow.rectTransform, stageW * .5f, stageH - FeetY, stageW * 1.3f, 150f);
            PracticeFx1101.Place(tornShadow.rectTransform, -10f, stageH - 22f, stageW + 20f, 24f);
            PracticeFx1101.Place(tornPaper.rectTransform, -6f, stageH - 16f, stageW + 12f, 22f);
            spotlight.rectTransform.sizeDelta = new Vector2(figureH * 1.25f, stageH - 6f);

            float arrowY = stageH - FeetY - figureH * .55f;
            PracticeFx1101.PlaceCenter((RectTransform)prev.transform, 22f, arrowY, 38f, 92f);
            PracticeFx1101.PlaceCenter((RectTransform)next.transform, stageW - 22f, arrowY, 38f, 92f);
            ShowPage(page, true);
            ApplyPose(0f, true);
        }

        // ------------------------------------------------------------------ construction

        private bool Still => reduceMotion || BattlePresentationOptions.ReduceMotion;
        private float ContentWidth => Mathf.Max(stageW, pad * 2f + slotW * actors.Count);

        private void BuildStage(IList<int> roster)
        {
            rect = (RectTransform)transform;
            Sprite stageSprite = Resources.Load<Sprite>(StageArt);
            backdrop = PracticeFx1101.NewImage("StageBackdrop", transform, stageSprite,
                stageSprite != null ? new Color(.8f, .78f, .9f, 1f) : (Color)Ground);
            backdrop.raycastTarget = true; // drag surface for paging
            backdrop.preserveAspect = false;

            topFade = PracticeFx1101.NewImage("StageTopFade", transform, PracticeFx1101.Fade, Ground);
            topFade.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            floorGlow = BattleFxArt1021.NewImage("StageFloorGlow", transform, BattleFxArt1021.Dot,
                new Color(.55f, .25f, 1f, .42f), true);

            spotlight = BattleFxArt1021.NewImage("StageSpotlight", transform, PracticeFx1101.Cone,
                new Color(.55f, 1f, .98f, .32f), true);
            RectTransform spot = spotlight.rectTransform;
            spot.anchorMin = spot.anchorMax = new Vector2(0f, 1f);
            spot.pivot = new Vector2(.5f, 1f);

            var viewportNode = new GameObject("StageCastViewport", typeof(RectTransform));
            viewportNode.transform.SetParent(transform, false);
            viewport = (RectTransform)viewportNode.transform;
            PracticeFx1101.Stretch(viewport);
            var castNode = new GameObject("StageCast", typeof(RectTransform));
            castNode.transform.SetParent(viewport, false);
            cast = (RectTransform)castNode.transform;
            cast.anchorMin = cast.anchorMax = Vector2.zero;
            cast.pivot = Vector2.zero;

            BuildGlow();
            if (roster != null)
                for (int i = 0; i < roster.Count; i++) actors.Add(BuildActor(roster[i], i));

            bottomFade = PracticeFx1101.NewImage("StageBottomFade", transform, PracticeFx1101.Fade,
                new Color(Ground.r, Ground.g, Ground.b, .85f));
            bottomFade.transform.SetSiblingIndex(viewport.GetSiblingIndex());

            var notesNode = new GameObject("StageNotes", typeof(RectTransform));
            notesNode.transform.SetParent(transform, false);
            notesRoot = (RectTransform)notesNode.transform;
            PracticeFx1101.Stretch(notesRoot);

            tornShadow = PracticeFx1101.NewImage("StageTornShadow", transform, PracticeFx1101.TornEdge, Hot);
            tornPaper = PracticeFx1101.NewImage("StageTornPaper", transform, PracticeFx1101.TornEdge,
                SirenStickerUi.Paper);

            BuildTitle();
            prev = Arrow("StagePrev", "‹", () => ShowPage(page - 1));
            next = Arrow("StageNext", "›", () => ShowPage(page + 1));

            scroll = GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = cast;
            scroll.horizontal = actors.Count > PerPage;
            scroll.vertical = false;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f;

            Relayout(stageW, stageH);
            if (MissingArtCount > 0)
                Debug.LogWarning("SIREN_PRACTICE_CHIBI_ART_PENDING count=" + MissingArtCount + " root=" + ArtRoot);
        }

        private void BuildTitle()
        {
            Image live = PracticeFx1101.NewImage("StageLiveChip", transform, PracticeFx1101.Slab, Hot);
            PracticeFx1101.Place(live.rectTransform, 22f, 18f, 92f, 34f);
            Text liveText = PracticeFx1101.NewText("Label", live.transform, font, "● LIVE", 18, SirenStickerUi.Paper);
            PracticeFx1101.Stretch(liveText.rectTransform);
            Text title = PracticeFx1101.NewText("StageTitle", transform, font, "练习舞台", 28, SirenStickerUi.Paper,
                TextAnchor.MiddleLeft);
            PracticeFx1101.Place(title.rectTransform, 124f, 14f, 200f, 42f);
            PracticeFx1101.Ink(title, 2f);

            Image hintPlate = PracticeFx1101.NewImage("StageHintPlate", transform, PracticeFx1101.Slab,
                new Color(Ground.r, Ground.g, Ground.b, .78f));
            PracticeFx1101.Place(hintPlate.rectTransform, stageW - 262f, 18f, 240f, 34f);
            hintPlate.rectTransform.anchorMin = hintPlate.rectTransform.anchorMax = new Vector2(1f, 1f);
            hintPlate.rectTransform.pivot = new Vector2(1f, 1f);
            hintPlate.rectTransform.anchoredPosition = new Vector2(-22f, -18f);
            Text hint = PracticeFx1101.NewText("StageHint", hintPlate.transform, font, "点击小人 · 切换成员", 18,
                TealGlow);
            PracticeFx1101.Stretch(hint.rectTransform);

            var dotsNode = new GameObject("StagePageDots", typeof(RectTransform));
            dotsNode.transform.SetParent(transform, false);
            var dotsRect = (RectTransform)dotsNode.transform;
            dotsRect.anchorMin = dotsRect.anchorMax = new Vector2(1f, 1f);
            dotsRect.pivot = new Vector2(1f, 1f);
            dotsRect.anchoredPosition = new Vector2(-26f, -58f);
            dotsRect.sizeDelta = new Vector2(200f, 14f);
            int pages = PageCount;
            for (int i = 0; i < pages && pages > 1; i++)
            {
                Image dot = PracticeFx1101.NewImage("PageDot-" + i, dotsRect, null, SirenStickerUi.Muted);
                RectTransform d = dot.rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(1f, .5f);
                d.pivot = new Vector2(.5f, .5f);
                d.anchoredPosition = new Vector2(-6f - (pages - 1 - i) * 20f, 0f);
                d.sizeDelta = new Vector2(10f, 10f);
                d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                dots.Add(dot);
            }
        }

        private Button Arrow(string name, string glyph, UnityEngine.Events.UnityAction action)
        {
            Image hit = PracticeFx1101.NewImage(name, transform, PracticeFx1101.Slab, new Color(Ground.r, Ground.g, Ground.b, .82f));
            hit.raycastTarget = true;
            Outline rim = hit.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(TealGlow.r, TealGlow.g, TealGlow.b, .9f);
            rim.effectDistance = new Vector2(2f, -2f);
            Text label = PracticeFx1101.NewText("Glyph", hit.transform, font, glyph, 46, SirenStickerUi.Paper);
            PracticeFx1101.Stretch(label.rectTransform, 0f, 6f, 0f, 0f);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            return button;
        }

        private void BuildGlow()
        {
            var node = new GameObject("SelectGlow", typeof(RectTransform));
            node.transform.SetParent(transform, false);
            glowRoot = (RectTransform)node.transform;
            glowRoot.anchorMin = glowRoot.anchorMax = new Vector2(.5f, 0f);
            glowRoot.pivot = new Vector2(.5f, 0f);
            halo = BattleFxArt1021.NewImage("SelectHalo", glowRoot, BattleFxArt1021.Dot,
                new Color(TealGlow.r, TealGlow.g, TealGlow.b, .38f), true);
            Material material = PracticeFx1101.Silhouette;
            if (material == null) return;
            for (int i = 0; i < 8; i++)
            {
                Image layer = PracticeFx1101.NewImage("SelectOutline-" + i, glowRoot, null, TealGlow);
                layer.material = material;
                layer.preserveAspect = true;
                glowLayers.Add(layer);
            }
        }

        private Actor BuildActor(int member, int order)
        {
            MemberDefinition definition = GameModel.Members[member];
            var actor = new Actor { Member = member, Id = definition.Id, Phase = order * 1.37f };
            var root = new GameObject("PracticeActor-" + definition.Id, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            root.transform.SetParent(cast, false);
            actor.Root = (RectTransform)root.transform;
            actor.Root.anchorMin = actor.Root.anchorMax = Vector2.zero;
            actor.Root.pivot = new Vector2(.5f, 0f);
            Image hit = root.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            actor.Group = root.AddComponent<CanvasGroup>();
            actor.Button = root.GetComponent<Button>();
            actor.Button.targetGraphic = hit;
            actor.Button.transition = Selectable.Transition.None;
            int captured = member;
            actor.Button.onClick.AddListener(() => onPick?.Invoke(captured));

            actor.Pool = BattleFxArt1021.NewImage("Pool", actor.Root, BattleFxArt1021.Dot, new Color(.5f, .3f, 1f, .5f), true);
            actor.Ring = BattleFxArt1021.NewImage("FootRing", actor.Root, BattleFxArt1021.Ring, new Color(TealGlow.r, TealGlow.g, TealGlow.b, 0f), true);
            actor.Ripple = BattleFxArt1021.NewImage("FootRipple", actor.Root, BattleFxArt1021.Ring, new Color(TealGlow.r, TealGlow.g, TealGlow.b, 0f), true);

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(actor.Root, false);
            actor.Body = (RectTransform)body.transform;
            actor.Body.anchorMin = actor.Body.anchorMax = new Vector2(.5f, 0f);
            actor.Body.pivot = new Vector2(.5f, 0f);

            // Story leads load now; other members only when first on stage (rosters can reach 50+).
            actor.Chibi = Array.IndexOf(CastIds, definition.Id) >= 0;
            Sprite sprite = actor.Chibi ? LoadFigure(definition, out actor.Chibi) : null;
            actor.PendingArt = sprite == null;
            actor.Shadow = PracticeFx1101.NewImage("StickerShadow", actor.Body, sprite, new Color(.55f, .18f, .95f, .85f));
            actor.Shadow.material = PracticeFx1101.Silhouette;
            actor.Shadow.preserveAspect = true;
            actor.Shadow.enabled = sprite != null && actor.Shadow.material != null;
            actor.FigureImage = PracticeFx1101.NewImage((actor.Chibi ? "Chibi-" : "Figure-") + definition.Id,
                actor.Body, sprite, Color.white);
            actor.FigureImage.preserveAspect = true;
            actor.FigureImage.enabled = sprite != null;
            actor.Figure = actor.FigureImage.rectTransform;

            actor.Plate = PracticeFx1101.NewImage("NamePlate", actor.Root, SirenStickerUi.ButtonSprite, Color.white);
            actor.Plate.type = Image.Type.Sliced;
            actor.Name = PracticeFx1101.NewText("Name", actor.Plate.transform, font, definition.Name, 20, SirenStickerUi.Paper);
            PracticeFx1101.Stretch(actor.Name.rectTransform, 6f, 0f, -6f, 0f);
            PracticeFx1101.Ink(actor.Name, 1.2f);
            actor.Tag = PracticeFx1101.NewImage("TeamTag", actor.Root, PracticeFx1101.Slab, Hot);
            actor.TagText = PracticeFx1101.NewText("Label", actor.Tag.transform, font, string.Empty, 14, SirenStickerUi.Paper);
            PracticeFx1101.Stretch(actor.TagText.rectTransform);
            return actor;
        }

        private Sprite LoadFigure(MemberDefinition definition, out bool chibi)
        {
            chibi = Array.IndexOf(CastIds, definition.Id) >= 0;
            if (chibi)
            {
                Sprite sprite = Resources.Load<Sprite>(ArtRoot + "chibi-" + definition.Id);
                if (sprite == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(ArtRoot + "chibi-" + definition.Id);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
                        sprite.hideFlags = HideFlags.DontSave;
                        ownedSprites.Add(sprite);
                    }
                }

                if (sprite != null) return sprite;
                // Missing Q art stays honest: count it and fall back to the member's own figure.
                MissingArtCount++;
                chibi = false;
            }

            Sprite thumb = string.IsNullOrEmpty(definition.ThumbnailResourcePath) ? null
                : Resources.Load<Sprite>(definition.ThumbnailResourcePath);
            return thumb != null ? thumb : Resources.Load<Sprite>(definition.ResourcePath);
        }

        private void EnsureFigure(Actor actor)
        {
            if (!actor.PendingArt) return;
            actor.PendingArt = false;
            Sprite sprite = LoadFigure(GameModel.Members[actor.Member], out _);
            actor.FigureImage.sprite = sprite;
            actor.FigureImage.enabled = sprite != null;
            actor.Shadow.sprite = sprite;
            actor.Shadow.enabled = sprite != null && actor.Shadow.material != null;
            LayoutActor(actor, actors.IndexOf(actor));
        }

        private void LayoutActor(Actor actor, int index)
        {
            actor.BaseX = pad + slotW * (index + .5f);
            actor.Root.sizeDelta = new Vector2(slotW, FeetY + figureH * 1.08f);
            actor.Root.anchoredPosition = new Vector2(actor.BaseX, 0f);
            Sprite sprite = actor.FigureImage.sprite;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : .73f;
            Vector2 size = new Vector2(figureH * aspect, figureH);
            actor.Body.anchoredPosition = new Vector2(0f, FeetY);
            actor.Body.sizeDelta = size;
            SetBottom(actor.Figure, size, Vector2.zero);
            SetBottom(actor.Shadow.rectTransform, size, new Vector2(7f, -4f));
            float poolW = Mathf.Min(slotW * 1.05f, 190f);
            Center(actor.Pool.rectTransform, new Vector2(0f, FeetY), new Vector2(poolW, poolW * .32f));
            Center(actor.Ring.rectTransform, new Vector2(0f, FeetY), new Vector2(poolW * .95f, poolW * .3f));
            Center(actor.Ripple.rectTransform, new Vector2(0f, FeetY), new Vector2(poolW * .95f, poolW * .3f));
            float plateW = Mathf.Min(slotW - 12f, 150f);
            Center(actor.Plate.rectTransform, new Vector2(0f, 39f), new Vector2(plateW, 38f));
            Center(actor.Tag.rectTransform, new Vector2(-plateW * .5f + 22f, 64f), new Vector2(48f, 22f));
            if (selected >= 0 && selected < actors.Count && actors[selected] == actor) MoveGlowTo(actor);
        }

        private static void SetBottom(RectTransform target, Vector2 size, Vector2 offset)
        {
            target.anchorMin = target.anchorMax = new Vector2(.5f, 0f);
            target.pivot = new Vector2(.5f, 0f);
            target.sizeDelta = size;
            target.anchoredPosition = offset;
        }

        private static void Center(RectTransform target, Vector2 position, Vector2 size)
        {
            target.anchorMin = target.anchorMax = new Vector2(.5f, 0f);
            target.pivot = new Vector2(.5f, .5f);
            target.sizeDelta = size;
            target.anchoredPosition = position;
        }

        private void MoveGlowTo(Actor actor)
        {
            if (glowRoot == null || actor == null) return;
            glowRoot.SetParent(actor.Body, false);
            glowRoot.SetSiblingIndex(actor.Shadow.transform.GetSiblingIndex() + 1);
            Vector2 size = actor.Figure.sizeDelta;
            glowRoot.anchorMin = glowRoot.anchorMax = new Vector2(.5f, 0f);
            glowRoot.pivot = new Vector2(.5f, 0f);
            glowRoot.anchoredPosition = Vector2.zero;
            glowRoot.sizeDelta = size;
            Center(halo.rectTransform, new Vector2(0f, size.y * .5f), new Vector2(size.x * 1.45f, size.y * 1.2f));
            for (int i = 0; i < glowLayers.Count; i++)
            {
                float angle = i * Mathf.PI * .25f;
                Image layer = glowLayers[i];
                layer.sprite = actor.FigureImage.sprite;
                layer.enabled = actor.FigureImage.sprite != null;
                SetBottom(layer.rectTransform, size, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.5f);
            }
        }

        private void StylePlate(Actor actor, bool chosen)
        {
            int level = model != null ? model.LevelOf(actor.Member) : 1;
            string name = GameModel.Members[actor.Member].Name;
            actor.Plate.sprite = chosen ? SirenStickerUi.PrimarySprite : SirenStickerUi.ButtonSprite;
            actor.Name.color = chosen ? (Color)Brand1025.Ink : SirenStickerUi.Paper;
            actor.Name.text = $"{name} <size=14>Lv.{level}</size>";
            Outline outline = actor.Name.GetComponent<Outline>();
            if (outline != null) outline.enabled = !chosen;
            bool captain = model != null && model.Save.Team.Count > 0 && model.Save.Team[0] == actor.Member;
            bool inTeam = model != null && model.IsInTeam(actor.Member);
            actor.Tag.gameObject.SetActive(captain || inTeam);
            actor.TagText.text = captain ? "队长" : "出战";
            actor.Tag.color = captain ? Hot : new Color32(137, 62, 255, 255);
        }

        private void RefreshPager()
        {
            bool paged = PageCount > 1;
            if (prev != null)
            {
                prev.gameObject.SetActive(paged);
                next.gameObject.SetActive(paged);
                prev.interactable = page > 0;
                next.interactable = page < PageCount - 1;
                SetArrowAlpha(prev, prev.interactable);
                SetArrowAlpha(next, next.interactable);
            }

            for (int i = 0; i < dots.Count; i++)
            {
                dots[i].color = i == page ? (Color)TealGlow : new Color(1f, 1f, 1f, .35f);
                dots[i].rectTransform.sizeDelta = i == page ? new Vector2(12f, 12f) : new Vector2(8f, 8f);
            }
        }

        private static void SetArrowAlpha(Button button, bool on)
        {
            foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                Color c = graphic.color;
                c.a = graphic is Text ? (on ? 1f : .3f) : (on ? .82f : .35f);
                graphic.color = c;
            }
        }

        // ------------------------------------------------------------------ paging drag

        public void OnBeginDrag(PointerEventData eventData) { dragging = true; }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
            poseDirty = true;
            if (cast == null || actors.Count <= PerPage) return;
            float travelled = targetX - cast.anchoredPosition.x;
            int nearest = Mathf.RoundToInt(-cast.anchoredPosition.x / (PerPage * slotW));
            if (Mathf.Abs(travelled) > slotW * .6f) nearest = page + (travelled > 0 ? 1 : -1);
            ShowPage(nearest);
        }

        // ------------------------------------------------------------------ motion

        private void Update()
        {
            bool still = Still;
            if (still != lastStill) poseDirty = true;
            lastStill = still;
            // Reduce-motion holds a static pose: no per-frame work until something actually changes.
            if (still && !dragging && !poseDirty) return;
            poseDirty = false;
            ApplyPose(Time.unscaledDeltaTime, false);
        }

        private void ApplyPose(float dt, bool snap)
        {
            if (cast == null) return;
            bool still = Still;
            float t = Application.isPlaying ? Time.unscaledTime : 0f;
            if (!dragging)
            {
                float x = cast.anchoredPosition.x;
                x = snap || still ? targetX : Mathf.Lerp(x, targetX, 1f - Mathf.Exp(-dt * 12f));
                if (Mathf.Abs(x - targetX) < .5f) x = targetX;
                cast.anchoredPosition = new Vector2(x, 0f);
            }

            for (int i = 0; i < actors.Count; i++)
            {
                Actor a = actors[i];
                float goal = i == selected ? 1f : 0f;
                a.Sel = snap || still ? goal : Mathf.MoveTowards(a.Sel, goal, dt * 5.5f);

                // Paged rosters: members beyond the current four fade into the wings instead of being cut,
                // and anyone fully off stage is switched off so large rosters cost nothing per frame.
                float sx = a.BaseX + cast.anchoredPosition.x;
                float lo = pad + slotW * .5f - 2f, hi = stageW - pad - slotW * .5f + 2f;
                float outside = actors.Count > PerPage ? Mathf.Max(0f, Mathf.Max(lo - sx, sx - hi)) : 0f;
                float vis = 1f - Mathf.Clamp01(outside / (slotW * .7f));
                bool onStage = vis > 0f;
                if (a.Root.gameObject.activeSelf != onStage) a.Root.gameObject.SetActive(onStage);
                if (!onStage) continue;
                EnsureFigure(a);
                a.Group.alpha = vis;
                a.Group.blocksRaycasts = vis > .6f;

                float s = Smooth(a.Sel);
                bool isSel = i == selected;
                float bob = 0f, tilt = 0f, squash = 1f;
                if (!still)
                {
                    if (isSel && practicing)
                    {
                        float hop = Mathf.Abs(Mathf.Sin(t * 8.5f));
                        bob = hop * 22f;
                        squash = 1f + (1f - hop) * .05f;
                        tilt = Mathf.Sin(t * 8.5f) * 4f;
                    }
                    else
                    {
                        bob = Mathf.Sin(t * 1.7f + a.Phase) * 3f * (1f - s) + Mathf.Abs(Mathf.Sin(t * 3.1f)) * 9f * s;
                        tilt = Mathf.Sin(t * 1.15f + a.Phase) * 1.6f * (1f - s);
                    }
                }

                float scale = Mathf.Lerp(.9f, 1.14f, s);
                float foot = FeetY - s * 10f; // the picked member steps forward
                a.Root.anchoredPosition = new Vector2(a.BaseX, 0f);
                a.Body.localScale = new Vector3(scale * (2f - squash), scale * squash, 1f);
                a.Body.anchoredPosition = new Vector2(0f, foot + bob);
                a.Pool.rectTransform.anchoredPosition = new Vector2(0f, foot);
                a.Ring.rectTransform.anchoredPosition = new Vector2(0f, foot);
                a.Ripple.rectTransform.anchoredPosition = new Vector2(0f, foot);
                a.Body.localRotation = Quaternion.Euler(0f, 0f, tilt);
                float shade = Mathf.Lerp(.66f, 1f, s);
                a.FigureImage.color = new Color(shade, shade * .97f, Mathf.Lerp(.82f, 1f, s), 1f);
                Color shadow = a.Shadow.color;
                shadow.a = Mathf.Lerp(.55f, .9f, s);
                a.Shadow.color = shadow;
                float pulse = still ? 1f : .8f + .2f * Mathf.Sin(t * 4f);
                a.Pool.color = Color.Lerp(new Color(.5f, .3f, 1f, .38f), new Color(TealGlow.r, TealGlow.g, TealGlow.b, .55f), s);
                a.Ring.color = new Color(TealGlow.r, TealGlow.g, TealGlow.b, .95f * s * pulse);
                a.Ring.rectTransform.localScale = Vector3.one * (1f + (practicing && isSel && !still ? .08f * Mathf.Sin(t * 17f) : 0f));
                float ripple = still ? 0f : Mathf.Repeat(t * (practicing ? 1.6f : .8f), 1f);
                a.Ripple.rectTransform.localScale = Vector3.one * Mathf.Lerp(.9f, 1.55f, ripple);
                a.Ripple.color = new Color(TealGlow.r, TealGlow.g, TealGlow.b, still ? 0f : .6f * s * (1f - ripple));
            }

            if (selected >= 0 && selected < actors.Count)
            {
                Actor chosen = actors[selected];
                float goalX = chosen.BaseX + cast.anchoredPosition.x;
                spotX = snap || still ? goalX : Mathf.Lerp(spotX, goalX, 1f - Mathf.Exp(-dt * 10f));
                float glowPulse = still ? 1f : .78f + .22f * Mathf.Sin(t * (practicing ? 12f : 3.4f));
                spotlight.rectTransform.anchoredPosition = new Vector2(spotX, 0f);
                spotlight.color = new Color(.55f, 1f, .98f, (practicing ? .42f : .3f) * Mathf.Lerp(.85f, 1f, glowPulse));
                float sel = Smooth(chosen.Sel);
                foreach (Image layer in glowLayers)
                    layer.color = new Color(TealGlow.r, TealGlow.g, TealGlow.b, sel * glowPulse);
                halo.color = new Color(TealGlow.r, TealGlow.g, TealGlow.b, .3f * sel * glowPulse);
                if (practicing && !still && Application.isPlaying) EmitNotes(chosen, t);
            }

            UpdateNotes(t);
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        private void EmitNotes(Actor chosen, float t)
        {
            if (t < nextNoteAt) return;
            nextNoteAt = t + .16f;
            int slot = -1;
            for (int i = 0; i < notes.Count; i++)
                if (!notes[i].gameObject.activeSelf) { slot = i; break; }
            if (slot < 0 && notes.Count < 10)
            {
                Text note = PracticeFx1101.NewText("Note", notesRoot, font, "♪", 30, TealGlow);
                PracticeFx1101.Ink(note, 1.4f);
                notes.Add(note); noteBirth.Add(0f); noteFrom.Add(Vector2.zero);
                slot = notes.Count - 1;
            }

            if (slot < 0) return;
            Text n = notes[slot];
            n.gameObject.SetActive(true);
            n.text = slot % 3 == 0 ? "♫" : "♪";
            n.color = slot % 2 == 0 ? (Color)TealGlow : Hot;
            float x = chosen.BaseX + cast.anchoredPosition.x + UnityEngine.Random.Range(-figureH * .35f, figureH * .35f);
            float y = FeetY + figureH * UnityEngine.Random.Range(.55f, .95f);
            noteFrom[slot] = new Vector2(x, y);
            noteBirth[slot] = t;
            RectTransform r = n.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(40f, 40f);
        }

        private void UpdateNotes(float t)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                Text n = notes[i];
                if (!n.gameObject.activeSelf) continue;
                float age = (t - noteBirth[i]) / .9f;
                if (age >= 1f || !practicing) { n.gameObject.SetActive(false); continue; }
                n.rectTransform.anchoredPosition = noteFrom[i] + new Vector2(Mathf.Sin(age * 6f + i) * 10f, age * 120f);
                n.rectTransform.localScale = Vector3.one * Mathf.Lerp(.6f, 1.15f, age);
                Color c = n.color;
                c.a = age < .2f ? age / .2f : 1f - (age - .2f) / .8f;
                n.color = c;
            }
        }

        private void HideNotes()
        {
            foreach (Text note in notes) if (note != null) note.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
        }
    }
}

using System.Collections.Generic;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>Construction and height-adaptive layout of the 2026-11-01 practice room.</summary>
    public sealed partial class MemberPracticePanel
    {
        private const string BackdropResourcePath = "Art/Reference038/member-detail-bg";
        private const float PageWidth = 720f, MinPageHeight = 1160f, DefaultPageHeight = 1440f;
        private const float HeaderH = 112f, InfoH = 344f, ProjectH = 160f, StartH = 100f, BottomH = 62f;
        private const float StageMaxH = 600f;
        private const float LevelBarX = 428f, LevelBarY = 124f, LevelBarW = 228f, LevelBarH = 16f;
        private const float ActionX = 166f;
        private static readonly string[] StatNames = { "声能", "律动", "气场", "共鸣" };
        private static readonly Color[] StatColors =
        {
            new Color32(28, 242, 236, 255), new Color32(255, 92, 190, 255),
            new Color32(176, 120, 255, 255), new Color32(255, 205, 96, 255),
        };

        private RectTransform page;
        private SirenPracticeScene stage;
        private RectTransform memberCard, projectCard, startRect, motionRect, doneRect;
        private Vector2 memberCardHome;
        private float pageScale = 1f;

        private RectTransform portraitHolder, portraitWindow, vinyl;
        private Image portrait, portraitGlow, raceBadge, levelFill, levelMarker, radarHost;
        private PracticeRadar1101 radar;
        private readonly List<Image> rings = new List<Image>(3);
        private readonly Text[] statValues = new Text[4];
        private readonly Image[] statFills = new Image[4];
        private Text memberName, rarityText, memberMeta, careerChipText, levelText, levelCaption, powerText;
        private Text actionText, focusText, quoteText, goldOwned, goldChipValue;
        private Image projectGlyph, specialtyChip, quoteIcon;
        private GameObject startObject;
        private Text startLabel;
        private GameObject motionObject;
        private Text reduceMotionLabel;

        private void BuildPage()
        {
            Sprite backdropSprite = ReferenceArt038.Load(BackdropResourcePath);
            Image ground = kit.NewImage("PracticeGround", transform, null, new Color32(9, 6, 22, 255));
            PanelKit.Stretch(ground.rectTransform);
            ground.raycastTarget = true;
            if (backdropSprite != null)
            {
                Image backdrop = kit.NewImage("PracticeBackdrop", transform, backdropSprite, Color.white);
                AspectRatioFitter fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backdropSprite.rect.width / backdropSprite.rect.height;
                PageAmbient.Set(gameObject, backdropSprite, .3f);
            }

            page = kit.NewRect("PracticePage", transform);
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, 1f);
            page.sizeDelta = new Vector2(PageWidth, DefaultPageHeight);

            stage = SirenPracticeScene.Build(page, kit.Font, model,
                SirenPracticeScene.OwnedRoster(model, memberIndex), memberIndex, member => SelectMember(member));
            stage.SetPracticing(false, reduceMotion);

            BuildHeader();
            BuildMemberCard();
            BuildProjectCard();

            startObject = SecondaryTasks038Chrome.Button(page, "StartPractice", "开始练习", 40,
                StartPractice, ModalPolish038ButtonStyle.Primary);
            startRect = startObject.GetComponent<RectTransform>();
            startLabel = SecondaryTasks038Chrome.LabelOf(startObject);
            if (startLabel != null)
            {
                startLabel.name = "StartPracticeLabel";
                startLabel.fontStyle = FontStyle.BoldAndItalic;
            }

            motionObject = SecondaryTasks038Chrome.Button(page, "PracticeReduceMotion", string.Empty, 22,
                ToggleReduceMotion, ModalPolish038ButtonStyle.Secondary);
            motionRect = motionObject.GetComponent<RectTransform>();
            reduceMotionLabel = SecondaryTasks038Chrome.LabelOf(motionObject);
            if (reduceMotionLabel != null) reduceMotionLabel.name = "PracticeReduceMotionLabel";
            RefreshReduceMotionLabel();

            GameObject done = SecondaryTasks038Chrome.Button(page, "PracticeDone", "完成 · 返回", 28, Close,
                ModalPolish038ButtonStyle.Secondary);
            doneRect = done.GetComponent<RectTransform>();

            BuildResult();
        }

        private void BuildHeader()
        {
            GameObject band = SecondaryTasks038Chrome.Header(kit, page, "训练室", "练习室", "PracticeBack", Close);
            Image chip = kit.NewImage("PracticeGoldChip", band.transform, PunkUi.SlantFill, new Color32(16, 11, 26, 245));
            PanelKit.PlaceTop(chip.rectTransform, 500f, 32f, 198f, 50f);
            Outline rim = kit.AddOutline(chip.gameObject, new Color32(255, 205, 96, 210), 1.6f);
            rim.useGraphicAlpha = false;
            Image coin = kit.NewImage("Coin", chip.transform, PanelKit.CurrencyIcon("gold"), Color.white);
            coin.preserveAspect = true;
            PanelKit.PlaceTop(coin.rectTransform, 10f, 4f, 42f, 42f);
            goldChipValue = Label(chip.transform, "PracticeGoldChipValue", string.Empty, 24, PanelKit.Gold,
                54f, 0f, 136f, 50f, TextAnchor.MiddleRight);
            PanelKit.EnableBestFit(goldChipValue, 16);
        }

        private void BuildMemberCard()
        {
            GameObject card = SecondaryTasks038Chrome.Plate(kit, page, "PracticeMemberCard",
                SirenStickerUi.Ink, SirenStickerUi.Violet);
            memberCard = card.GetComponent<RectTransform>();
            Transform c = card.transform;

            // Bust portrait: pink pop-art slab behind a paper-rimmed window.
            portraitHolder = kit.NewRect("PracticePortraitHolder", c);
            PanelKit.PlaceTop(portraitHolder, 16f, 16f, 206f, 312f);
            PanelKit.CenterPivot(portraitHolder);
            portraitGlow = kit.NewImage("PracticePortraitGlow", portraitHolder, kit.RadialSprite(),
                new Color(.38f, .95f, 1f, .18f));
            PanelKit.Stretch(portraitGlow.rectTransform, -40f, -40f, 40f, 40f);
            Image slab = kit.NewImage("PracticePortraitSlab", portraitHolder, null, PunkUi.Hot);
            PanelKit.Stretch(slab.rectTransform, 10f, -10f, 10f, -10f);
            slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Image frame = kit.NewImage("PracticePortraitFrame", portraitHolder, SirenStickerUi.PanelSprite, Color.white);
            frame.type = Image.Type.Sliced;
            PanelKit.Stretch(frame.rectTransform);
            Image window = kit.NewImage("PracticePortraitWindow", frame.transform, null, new Color32(30, 18, 52, 255));
            PanelKit.PlaceTop(window.rectTransform, 6f, 6f, 194f, 300f);
            window.gameObject.AddComponent<RectMask2D>();
            portraitWindow = window.rectTransform;
            Image wash = kit.NewImage("PracticePortraitWash", window.transform, kit.RadialSprite(),
                new Color32(137, 62, 255, 150));
            PanelKit.Stretch(wash.rectTransform, -60f, -40f, 60f, 80f);
            portrait = kit.NewImage("PracticePortrait", window.transform, null, Color.white);
            portrait.preserveAspect = true;
            Image fade = kit.NewImage("PracticePortraitShade", window.transform, PracticeFx1101.Fade,
                new Color32(13, 9, 22, 230));
            PanelKit.PlaceTop(fade.rectTransform, 0f, 200f, 194f, 100f);
            rarityText = Label(window.transform, "PracticeRarity", "SSR", 34, Color.white, 10f, 4f, 120f, 44f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            PracticeFx1101.Ink(rarityText, 2f);
            raceBadge = kit.NewImage("PracticeRaceBadge", window.transform, null, Color.white);
            raceBadge.preserveAspect = true;
            PanelKit.PlaceTop(raceBadge.rectTransform, 132f, 238f, 56f, 56f);
            for (int i = 0; i < 3; i++)
            {
                Image ring = kit.NewImage("PracticeRing-" + i, portraitHolder, BattleFxArt1021.Ring,
                    new Color(.47f, .95f, 1f, 0f));
                ring.material = BattleFxArt1021.Additive;
                PanelKit.PlaceTop(ring.rectTransform, 3f, 56f, 200f, 200f);
                PanelKit.CenterPivot(ring.rectTransform);
                rings.Add(ring);
            }

            // Right column.
            memberName = Label(c, "PracticeMemberName", string.Empty, 40, SirenStickerUi.Paper, 240f, 12f, 310f, 56f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(memberName, 26);
            PracticeFx1101.Ink(memberName, 2f);
            Image careerChip = kit.NewImage("PracticeCareerChip", c, SirenStickerUi.PrimarySprite, Color.white);
            careerChip.type = Image.Type.Sliced;
            PanelKit.PlaceTop(careerChip.rectTransform, 560f, 22f, 96f, 36f);
            careerChipText = Label(careerChip.transform, "Label", string.Empty, 19, Brand1025.Ink, 0f, 0f, 96f, 36f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PanelKit.EnableBestFit(careerChipText, 12);
            memberMeta = Label(c, "PracticeMemberMeta", string.Empty, 20, SirenStickerUi.Muted, 240f, 68f, 416f, 28f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(memberMeta, 14);

            levelText = Label(c, "PracticeLevel", string.Empty, 42, PanelKit.Gold, 240f, 100f, 186f, 54f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PracticeFx1101.Ink(levelText, 1.6f);
            levelCaption = Label(c, "PracticeLevelCaption", string.Empty, 15, SirenStickerUi.Muted,
                LevelBarX, 100f, LevelBarW, 20f, TextAnchor.MiddleRight, FontStyle.Bold);
            levelFill = SecondaryTasks038Chrome.Progress(kit, c, "PracticeLevelBar", LevelBarX, LevelBarY,
                LevelBarW, LevelBarH, new Color32(40, 30, 64, 255), SirenStickerUi.Cyan, out levelMarker);

            Label(c, "PracticePowerLabel", "战力", 20, SirenStickerUi.Muted, 240f, 166f, 56f, 30f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            powerText = Label(c, "PracticePower", string.Empty, 40, SirenStickerUi.Paper, 294f, 152f, 230f, 54f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            PracticeFx1101.Ink(powerText, 1.8f);
            Label(c, "PracticePowerTag", "BATTLE POWER", 14, new Color32(150, 138, 180, 255), 500f, 174f, 156f, 22f,
                TextAnchor.MiddleRight, FontStyle.BoldAndItalic);

            Image rule = kit.NewImage("PracticeStatsRule", c, null, new Color32(137, 62, 255, 170));
            PanelKit.PlaceTop(rule.rectTransform, 240f, 214f, 416f, 2f);

            radarHost = kit.NewImage("PracticeRadarPlate", c, kit.RadialSprite(), new Color32(137, 62, 255, 60));
            PanelKit.PlaceTop(radarHost.rectTransform, 236f, 222f, 112f, 112f);
            var radarNode = new GameObject("PracticeRadar", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PracticeRadar1101));
            radarNode.transform.SetParent(radarHost.transform, false);
            radar = radarNode.GetComponent<PracticeRadar1101>();
            radar.raycastTarget = false;
            radar.AxisColors = StatColors;
            PanelKit.Stretch(radar.rectTransform, 6f, 6f, -6f, -6f);

            ReferencePunkFx.StatGlyphKind[] glyphs =
            {
                ReferencePunkFx.StatGlyphKind.Wave, ReferencePunkFx.StatGlyphKind.Note,
                ReferencePunkFx.StatGlyphKind.Spark, ReferencePunkFx.StatGlyphKind.Gem,
            };
            for (int i = 0; i < 4; i++)
            {
                float y = 226f + i * 27f;
                Image glyph = kit.NewImage("PracticeStatGlyph-" + StatNames[i], c, ReferencePunkFx.StatGlyph(glyphs[i]), StatColors[i]);
                glyph.preserveAspect = true;
                PanelKit.PlaceTop(glyph.rectTransform, 362f, y + 3f, 20f, 20f);
                Label(c, "PracticeStatName-" + StatNames[i], StatNames[i], 17, SirenStickerUi.Paper, 386f, y, 46f, 26f,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                statFills[i] = kit.NewBar("PracticeStatBar-" + StatNames[i], c, 436f, y + 9f, 168f, 9f,
                    new Color32(40, 30, 64, 255), StatColors[i], 4);
                statValues[i] = Label(c, "PracticeStat-" + StatNames[i], string.Empty, 19, SirenStickerUi.Paper,
                    606f, y, 50f, 26f, TextAnchor.MiddleRight, FontStyle.Bold);
            }
        }

        private void BuildProjectCard()
        {
            GameObject card = SecondaryTasks038Chrome.Plate(kit, page, "PracticeProjectCard",
                SirenStickerUi.Ink, SirenStickerUi.Cyan);
            projectCard = card.GetComponent<RectTransform>();
            Transform c = card.transform;

            Image disc = kit.NewImage("PracticeVinyl", c, ReferenceArt038.Load("Art/Reference038/vfx-vinyl"), Color.white);
            disc.preserveAspect = true;
            PanelKit.PlaceTop(disc.rectTransform, 16f, 14f, 132f, 132f);
            PanelKit.CenterPivot(disc.rectTransform);
            vinyl = disc.rectTransform;
            disc.gameObject.AddComponent<Canvas>(); // spinning record must not re-batch the page each frame
            if (disc.sprite == null) disc.sprite = kit.RadialSprite();
            Image badge = kit.NewImage("PracticeProjectBadge", c, null, Brand1025.Teal);
            PanelKit.PlaceTop(badge.rectTransform, 112f, 100f, 40f, 40f);
            PanelKit.CenterPivot(badge.rectTransform);
            badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            kit.AddOutline(badge.gameObject, new Color32(13, 9, 22, 255), 2f);
            projectGlyph = kit.NewImage("PracticeProjectGlyph", c, null, Brand1025.Ink);
            projectGlyph.preserveAspect = true;
            PanelKit.PlaceTop(projectGlyph.rectTransform, 117f, 105f, 30f, 30f);

            Label(c, "PracticeProjectEyebrow", "练习项目 · PRACTICE", 15, PunkUi.Hot, ActionX, 12f, 260f, 24f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            actionText = Label(c, "PracticeAction", string.Empty, 36, SirenStickerUi.Paper, ActionX, 38f, 260f, 52f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PracticeFx1101.Ink(actionText, 1.8f);
            specialtyChip = kit.NewImage("PracticeSpecialtyChip", c, PracticeFx1101.Slab, PunkUi.Hot);
            Label(specialtyChip.transform, "Label", "专长", 16, SirenStickerUi.Paper, 0f, 0f, 62f, 28f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            focusText = Label(c, "PracticeFocus", string.Empty, 20, SirenStickerUi.Cyan, ActionX, 92f, 294f, 28f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(focusText, 14);
            Text noDiamond = noDiamondLabel = Label(c, "PracticeNoDiamond", "只扣星光币 · 不消耗星钻", 16, SirenStickerUi.Muted,
                ActionX, 124f, 294f, 24f, TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(noDiamond, 12);

            Image divider = kit.NewImage("PracticeProjectDivider", c, null, new Color32(137, 62, 255, 150));
            PanelKit.PlaceTop(divider.rectTransform, 470f, 20f, 2f, 120f);
            costLabel = Label(c, "PracticeCostLabel", "单次消耗", 16, SirenStickerUi.Muted, 482f, 14f, 176f, 24f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            quoteIcon = kit.NewImage("PracticeQuoteCoin", c, PanelKit.CurrencyIcon("gold"), Color.white);
            quoteIcon.preserveAspect = true;
            PanelKit.PlaceTop(quoteIcon.rectTransform, 488f, 46f, 42f, 42f);
            quoteText = Label(c, "PracticeQuoteGold", string.Empty, 34, PanelKit.Gold, 534f, 40f, 126f, 54f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            quoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PanelKit.EnableBestFit(quoteText, 14);
            PracticeFx1101.Ink(quoteText, 1.4f);
            goldOwned = Label(c, "PracticeGoldOwned", string.Empty, 16, SirenStickerUi.Muted, 482f, 104f, 176f, 26f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PanelKit.EnableBestFit(goldOwned, 11);
        }

        private Text Label(Transform parent, string name, string value, int size, Color color, float x, float y,
            float width, float height, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text text = kit.NewPlacedText(parent, value, size, color, x, y, width, height, anchor, style);
            text.name = name;
            text.supportRichText = true;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ---------------------------------------------------------------- layout

        private void Relayout()
        {
            if (page == null || doneRect == null) return;
            var host = (RectTransform)transform;
            float hostW = host.rect.width > 1f ? host.rect.width : PageWidth;
            float hostH = host.rect.height > 1f ? host.rect.height : DefaultPageHeight;
            pageScale = Mathf.Min(1f, hostW / PageWidth);
            float pageH = hostH / pageScale;
            if (pageH < MinPageHeight)
            {
                pageScale = hostH / MinPageHeight;
                pageH = MinPageHeight;
            }

            page.anchoredPosition = Vector2.zero;
            page.sizeDelta = new Vector2(PageWidth, pageH);
            page.localScale = Vector3.one * pageScale;

            float[] gaps = { 8f, 16f, 14f, 16f, 14f, 18f };
            float fixedH = HeaderH + InfoH + ProjectH + StartH + BottomH;
            foreach (float gap in gaps) fixedH += gap;
            float stageH = pageH - fixedH;
            if (stageH > StageMaxH)
            {
                float extra = (stageH - StageMaxH) / (gaps.Length - 1);
                for (int i = 1; i < gaps.Length; i++) gaps[i] += extra;
                stageH = StageMaxH;
            }

            float y = HeaderH + gaps[0];
            var stageRect = (RectTransform)stage.transform;
            PanelKit.PlaceTop(stageRect, 0f, y, PageWidth, stageH);
            stageRect.SetSiblingIndex(0);
            stage.Relayout(PageWidth, stageH);
            y += stageH + gaps[1];
            PanelKit.PlaceTop(memberCard, 24f, y, 672f, InfoH);
            memberCardHome = memberCard.anchoredPosition;
            y += InfoH + gaps[2];
            PanelKit.PlaceTop(projectCard, 24f, y, 672f, ProjectH);
            y += ProjectH + gaps[3];
            PanelKit.PlaceTop(startRect, 24f, y, 672f, StartH);
            y += StartH + gaps[4];
            PanelKit.PlaceTop(motionRect, 24f, y, 236f, BottomH);
            PanelKit.PlaceTop(doneRect, 274f, y, 422f, BottomH);
            if (resultCard != null) resultCard.localScale = Vector3.one * pageScale;
            if (portrait != null && portrait.sprite != null)
                PanelKit.FrameBustPortrait(portrait, portraitWindow, GameModel.Members[memberIndex].Id);
        }
    }
}

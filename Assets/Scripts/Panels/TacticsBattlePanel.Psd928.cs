using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private Image psdGuardArt;
        private Image psdGuardRing;
        private ChoSiren.UI.Heartbeat1025 psdGuardBeat;
        private Text psdChargeLabel;
        private const float PsdScale = 720f / 2946f;
        private RectTransform psdRoot;
        private GameObject psdReroll;
        private Image psdRerollArt;
        private BattleRerollSlide929 psdRerollSlide;
        private ChoSiren.UI.InkFlameEdge1031 psdRerollEdge;
        private bool psdRerollGlowOn;

        /// <summary>Per frame: the glow fades in/out and breathes while a reroll is usable.</summary>
        private void AdvanceRerollGlow()
        {
            if (psdRerollEdge == null || paused) return;
            psdRerollEdge.Selected = psdRerollGlowOn;
        }
        private BattleAlbumUltimate929 psdUltimate;
        private Image psdPauseGlyph;
        private AlbumPlayGlyph929 psdResumeGlyph;
        private readonly Dictionary<CellView, BattleMemberAlbum929> psdAlbums = new Dictionary<CellView, BattleMemberAlbum929>();
        private readonly Dictionary<CellView, int> psdSlots = new Dictionary<CellView, int>();
        private readonly Dictionary<CellView, Image> psdFrames = new Dictionary<CellView, Image>();
        private readonly Dictionary<CellView, Image> psdRearFrames = new Dictionary<CellView, Image>();
        private readonly Dictionary<CellView, Text> psdEnergyLabels = new Dictionary<CellView, Text>();
        private readonly Dictionary<CellView, Image> psdHealthEdges = new Dictionary<CellView, Image>();
        private readonly Dictionary<CellView, SkillIconGraphic[]> psdSkillGlyphs = new Dictionary<CellView, SkillIconGraphic[]>();
        private readonly Dictionary<CellView, ChoSiren.UI.BattleAttackBadge> psdAttackBadges = new Dictionary<CellView, ChoSiren.UI.BattleAttackBadge>();
        private static readonly float[] PsdMemberX = { 0, 740, 1488, 2202 };
        private static readonly string[] PsdPortraits = { "battle-39", "battle-37", "battle-38", "battle-40" };
        // 2026-10-19 PSD: each member card runs from its disc top (3670) to the frame foot (5051).
        private const float PsdMemberTop = 3670f;
        private const float PsdMemberHeight = 1381f;
        // Live HP / energy bars sit exactly on the PSD's bar layers (L034-L041), per slot.
        private static readonly Rect[] PsdHpBars =
        {
            new Rect(71, 4669, 527, 135), new Rect(789, 4647, 546, 142),
            new Rect(1558, 4640, 527, 135), new Rect(2264, 4645, 527, 135),
        };
        private static readonly Rect[] PsdEnergyBars =
        {
            new Rect(120, 4807, 433, 126), new Rect(849, 4791, 427, 118),
            new Rect(1605, 4788, 427, 118), new Rect(2315, 4790, 428, 112),
        };
        // Skill columns float at each card's upper right (L085-L100): 大招 / 小招 / 被动.
        private readonly Dictionary<CellView, Image[]> psdSkillColumns = new Dictionary<CellView, Image[]>();
        private readonly Dictionary<CellView, Image> psdSkillCueBackings = new Dictionary<CellView, Image>();
        private readonly Dictionary<CellView, Image> psdUltPillars = new Dictionary<CellView, Image>();
        private readonly Dictionary<CellView, UltReadyRing1020> psdUltRings = new Dictionary<CellView, UltReadyRing1020>();
        private Text psdGuardState;

        private IEnumerator LoadPsdBattle()
        {
            var loader = new PanelKit("BattleLoading");
            var shade = loader.NewPanel("BattleArtLoading", transform, new Color32(9, 7, 27, 248), 0);
            PanelKit.Stretch(shade.GetComponent<RectTransform>());
            var label = loader.NewPlacedText(shade.transform, "舞台准备中…", 24, PanelKit.White,
                70, 660, 580, 100, TextAnchor.MiddleCenter);
            string error = null;
            yield return BattlePsdArt928.Load(p => label.text = $"舞台准备中 {p:P0}", e => error = e);
            if (error != null)
            {
                label.text = error;
                var retry = loader.NewButton("RetryBattleArt", shade.transform, "重新加载", 22,
                    PanelKit.Purple, PanelKit.White, () => { Destroy(shade); StartCoroutine(LoadPsdBattle()); }, 12);
                PanelKit.PlaceTop(retry.GetComponent<RectTransform>(), 220, 790, 280, 64);
                yield break;
            }
            Destroy(shade);
            Build();
            StartCoroutine(MainLoop());
        }

        private void PsdPlace(RectTransform rect, float x, float y, float w, float h)
            => PanelKit.PlaceTop(rect, x * PsdScale, y * PsdScale, w * PsdScale, h * PsdScale);

        private Image PsdArt(string key, Transform parent = null)
        {
            var e = BattlePsdArt928.Find(key);
            var image = kit.NewImage("PSD-" + key, parent ?? psdRoot, BattlePsdArt928.Sprite(key), Color.white);
            PsdPlace(image.rectTransform, e.x, e.y, e.width, e.height);
            image.raycastTarget = false;
            return image;
        }

        private void PsdText(Text text, float x, float y, float w, float h, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter, float angle = 0)
        {
            text.transform.SetParent(psdRoot, false);
            text.gameObject.SetActive(true);
            PsdPlace(text.rectTransform, x, y, w, h);
            text.fontSize = size; text.resizeTextMaxSize = size;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = Mathf.Max(9, size - 9);
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = alignment; text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            var outline = UnityNull.Real(text.GetComponent<Outline>()) ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(3, 4, 14, 255);
            outline.effectDistance = new Vector2(1.3f, -1.3f);
        }

        private Image PsdCover(Transform parent, string name, float x, float y, float w, float h, float angle = 0)
        {
            var image = kit.NewImage(name, parent, null, new Color32(3, 4, 12, 255));
            PsdPlace(image.rectTransform, x, y, w, h);
            image.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            image.raycastTarget = false;
            return image;
        }

        private void PsdButton(GameObject button, float x, float y, float w, float h, int fontSize)
        {
            button.transform.SetParent(psdRoot, false);
            button.SetActive(true);
            foreach (Transform child in button.transform) child.gameObject.SetActive(false);
            var label = PanelKit.LabelOf(button) ?? button.GetComponentsInChildren<Text>(true).First(t => t.name == "Label");
            label.transform.SetParent(button.transform, false); label.gameObject.SetActive(true);
            PanelKit.Stretch(label.rectTransform);
            label.fontSize = fontSize; label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11; label.resizeTextMaxSize = fontSize;
            label.fontStyle = FontStyle.BoldAndItalic; label.color = Color.white;
            PsdPlace(button.GetComponent<RectTransform>(), x, y, w, h);
            button.GetComponent<Image>().color = Color.clear;
            button.GetComponent<Button>().transition = Selectable.Transition.None;
            foreach (var fx in button.GetComponents<BattlePolish038ButtonMotion>()) fx.enabled = false;
            foreach (var fx in button.GetComponents<LobbyHotspotFeedback>()) fx.enabled = false;
        }

        private RectTransform PsdButtonArt(GameObject button, string key, float hitX, float hitY)
        {
            var group = kit.NewRect("PsdButtonVisual-" + button.name, button.transform);
            PanelKit.Stretch(group); PanelKit.CenterPivot(group);
            var entry = BattlePsdArt928.Find(key);
            var art = kit.NewImage("PsdButtonArt", group, BattlePsdArt928.Sprite(key), Color.white);
            PsdPlace(art.rectTransform, entry.x - hitX, entry.y - hitY, entry.width, entry.height);
            art.raycastTarget = false;
            var label = PanelKit.LabelOf(button);
            label.transform.SetParent(group, false); label.transform.SetAsLastSibling();
            var motion = UnityNull.Real(button.GetComponent<BattlePolish038ButtonMotion>()) ?? button.AddComponent<BattlePolish038ButtonMotion>();
            motion.enabled = true; motion.Configure(group);
            return group;
        }

        private void ApplyPsdBattleLayout()
        {
            if (!BattlePsdArt928.Ready) return;
            // Existing command components and simulator bindings are retained. Only their
            // authored visual parents change; hit targets never move with illustration motion.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            // Opaque blurred stage behind the PSD: on tall phones the fitted stage leaves side
            // margins, which must show the battle's own colours, not the previous screen.
            var backdropGo = new GameObject("PsdBattleBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            backdropGo.transform.SetParent(transform, false);
            var backdrop = backdropGo.GetComponent<RawImage>();
            backdrop.texture = ChoSiren.UI.PageAmbient.BlurredTexture(BattlePsdArt928.Sprite("stage"));
            backdrop.color = backdrop.texture != null ? new Color(.55f, .52f, .6f, 1f) : new Color32(14, 8, 26, 255);
            backdrop.raycastTarget = false;
            PanelKit.Stretch(backdrop.rectTransform);
            if (backdrop.texture != null)
            {
                var fitter = backdropGo.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdrop.texture.width / (float)Mathf.Max(1, backdrop.texture.height);
            }
            psdRoot = kit.NewRect("BattlePSD928", transform);
            psdRoot.gameObject.AddComponent<RectMask2D>();
            float h = 6144 * PsdScale;
            PanelKit.PlaceTop(psdRoot, 0, (1536 - h) / 2, 720, h);
            Canvas.ForceUpdateCanvases();
            psdRoot.gameObject.AddComponent<BattlePsdViewportFit1019>().Configure(
                psdRoot, (RectTransform)transform, new Vector2(720f, h));
            Image stageArt = PsdArt("stage");
            ChoSiren.UI.PageAmbient.Set(gameObject, BattlePsdArt928.Sprite("stage"), featherEdges: stageArt.rectTransform);
            var lightRoot = kit.NewRect("AlbumStageLights", psdRoot);
            PanelKit.PlaceTop(lightRoot, 0, 190, 720, 680);
            lightRoot.gameObject.AddComponent<BattleStageLighting929>().Configure(() => paused || closing, () => speed);

            // Reuse each enemy's independent motion, focus callback and defeat animation.
            enemyStageRoot = kit.NewRect("PsdEnemyStage", psdRoot);
            PanelKit.Stretch(enemyStageRoot);
            foreach (var pair in enemyAnchors) pair.Value.SetParent(enemyStageRoot, false);
            BuildWideStage1111(stageArt); // 2026-11-11: 三敌同屏 stages pan a wider row (WideStage1111)
            // 2026-11-03: every enemy keeps its planning-deck figure from Art/Enemies (the PSD's two baked enemy
            // layers showed the old 游魂/刺客 designs and are no longer swapped in).
            if (bossPresentation != null)
            {
                bossPresentation.transform.SetParent(psdRoot, false);
                bossPresentation.gameObject.SetActive(true);
                // Boss-specific motion remains available on boss encounters; old redundant HUDs are hidden.
                foreach (string n in new[] { "BossStageCaption", "BattleInstructionPlate", "BossAnimationState" })
                    bossPresentation.transform.Find(n)?.gameObject.SetActive(false);
                var rig = bossPresentation.transform.Find("BossMotionRig") as RectTransform;
                LayoutBossStage1031(rig); // 2026-10-31: the boss fills the stage (EnemyStage1031)
                // Stage lights now belong to the shared rear layer. Keep only boss-specific cues.
                foreach (Transform child in bossPresentation.transform)
                    if (child.name.StartsWith("BossSpotlight-")) child.gameObject.SetActive(false);
            }
            foreach (var cell in cells)
            {
                cell.Root.transform.SetParent(psdRoot, false);
                SkinPsdCell(cell);
            }

            PsdArt("battle-48"); PsdArt("battle-51");
            var hpTrack = PsdArt("battle-52"); hpTrack.color = new Color32(51, 18, 70, 255);
            enemyHpFill = PsdArt("battle-52");
            enemyHpFill.type = Image.Type.Filled; enemyHpFill.fillMethod = Image.FillMethod.Horizontal;
            enemyHpFill.fillOrigin = 0;
            PsdText(phaseText, 90, 20, 600, 455, 50);
            var countdown = kit.NewPlacedText(psdRoot, battle.IsTurnBased ? "回合" : "倒计时", 21, Color.white, 0, 0, 200, 40, TextAnchor.MiddleCenter);
            psdCountdownCaption = countdown;
            PsdText(countdown, 740, 0, 510, 125, 16);
            PsdText(timerText, 700, 120, 510, 240, 38); // ends left of the art's clock icon (it covered the 0 of "/30")
            PsdText(enemyHealthCaption, 1290, 200, 730, 140, 18);
            enemyHealthCaption.color = PanelKit.Muted;
            PsdText(enemyHpText, 850, 345, 1180, 190, 26);
            PsdText(enemyDefeatedText, 2350, 430, 430, 105, 14);
            PsdButton(autoButton, 1984, 111, 363, 239, 20);

            PsdButton(speedButton, 2347, 92, 253, 238, 20);

            PsdButton(retreatButton, 2600, 54, 280, 251, 25);

            PanelKit.LabelOf(retreatButton).text = "";
            var pauseGlyph = PsdArt("pause-glyph", retreatButton.transform);
            psdPauseGlyph = pauseGlyph;
            var pauseBounds = BattlePsdArt928.Find("pause-glyph");
            PsdPlace(pauseGlyph.rectTransform, pauseBounds.x - 2600, pauseBounds.y - 54, pauseBounds.width, pauseBounds.height);
            var resumeIcon = kit.NewRect("ResumeGlyph", retreatButton.transform);
            PsdPlace(resumeIcon, 90, 65, 82, 99);
            psdResumeGlyph = resumeIcon.gameObject.AddComponent<AlbumPlayGlyph929>();
            psdResumeGlyph.color = Color.white; psdResumeGlyph.raycastTarget = false; resumeIcon.gameObject.SetActive(false);

            // A replayable, optional explanation lives in the existing rules surface. It never
            // opens automatically or changes the rolled hand, and closing it resumes play.
            var quickGuide = PunkUi.Button(kit, psdRoot, "BattleQuickGuide", "怎么玩", 20,
                OpenBattleQuickGuide, PunkButtonStyle.Ghost);
            PsdPlace(quickGuide.GetComponent<RectTransform>(), 2250, 655, 650, 330);
            PanelKit.LabelOf(quickGuide).color = PanelKit.Cyan;
            ChoSiren.UI.SirenStickerUi.SkinButton(quickGuide);
            // Not on the 2026-10-19 PSD stage; the same guide stays in the pause menu (规则).
            quickGuide.SetActive(false);

            if (guardButton != null)
            {
                // 2026-10-19 PSD: 应急守护 is its own badge on the right of the stage (L084).
                PsdButton(guardButton, 2362, 2615, 639, 696, 16);
                PanelKit.LabelOf(guardButton).gameObject.SetActive(false);
                var badge = PsdButtonArt(guardButton, "guard-badge", 2362, 2615);
                psdGuardArt = badge.Find("PsdButtonArt").GetComponent<Image>();
                // The sample subtitle is covered by the real effect and charge.
                var subtitle = kit.NewImage("GuardSubtitle", badge, null, new Color32(17, 13, 56, 255));
                subtitle.raycastTarget = false;
                PsdPlace(subtitle.rectTransform, 112, 350, 366, 66);
                psdGuardRing = kit.NewImage("GuardFill", subtitle.transform, null, new Color32(64, 240, 255, 90));
                psdGuardRing.raycastTarget = false;
                PanelKit.Stretch(psdGuardRing.rectTransform);
                psdGuardRing.type = Image.Type.Filled;
                psdGuardRing.fillMethod = Image.FillMethod.Horizontal;
                guardHint.transform.SetParent(subtitle.transform, false); guardHint.gameObject.SetActive(true);
                PanelKit.Stretch(guardHint.rectTransform);
                guardHint.rectTransform.localEulerAngles = Vector3.zero;
                guardHint.alignment = TextAnchor.MiddleCenter;
                guardHint.fontSize = 15; guardHint.fontStyle = FontStyle.Bold; guardHint.color = Color.white;
                guardHint.resizeTextForBestFit = true; guardHint.resizeTextMinSize = 11; guardHint.resizeTextMaxSize = 15;
                guardHint.horizontalOverflow = HorizontalWrapMode.Wrap;
                guardHint.verticalOverflow = VerticalWrapMode.Truncate;
                if (guardHint.GetComponent<Outline>() == null)
                    guardHint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .85f);
                // 2026-10-25 (赵金): beats like a heart while it can be used.
                psdGuardBeat = ChoSiren.UI.Heartbeat1025.Attach(psdGuardArt.rectTransform, new Color32(64, 240, 255, 230), subtitle.rectTransform);
            }

            // 2026-10-19 PSD: the reroll control is the slanted bar with the heart microphone on
            // the left of the stage (L073 smoke, L074 bar, L076 arrows, L075 die). Tick dice to
            // reroll only those (at most two since 2026-10-31); with none ticked it rerolls all six. The model enforces quota.
            psdReroll = kit.NewButton("PsdReroll", psdRoot, "", 20, Color.clear, Color.white, () =>
            {
                if (paused || closing || awaitingDiceLanding || !awaitingInput || !diceTurn.CanReroll) return;
                if (diceTurn.SelectedForRerollCount > 0) RerollDice(); else EnergyRerollDice(); // 不选 = 全部重投
            }, 0);
            const float RerollX = 0, RerollY = 2700;
            PsdPlace(psdReroll.GetComponent<RectTransform>(), RerollX, RerollY, 1000, 640);
            psdReroll.GetComponent<Button>().transition = Selectable.Transition.None;
            psdReroll.GetComponent<Image>().color = Color.clear;
            var heartVisual = PsdButtonArt(psdReroll, "reroll-bar", RerollX, RerollY);
            psdRerollArt = heartVisual.Find("PsdButtonArt").GetComponent<Image>();
            foreach (string key in new[] { "reroll-smoke", "reroll-arrows", "reroll-dice" })
            {
                var part = BattlePsdArt928.Find(key);
                var partImage = kit.NewImage("PsdReroll-" + key, heartVisual, BattlePsdArt928.Sprite(key), Color.white);
                partImage.raycastTarget = false;
                PsdPlace(partImage.rectTransform, part.x - RerollX, part.y - RerollY, part.width, part.height);
                if (key == "reroll-smoke") { partImage.color = new Color(1, 1, 1, .75f); partImage.transform.SetAsFirstSibling(); }
            }
            // 2026-10-20: a soft cyan glow that hugs the bar's outline (feathered edge, gentle
            // drift), replacing the smoke and the hard-masked ink.
            heartVisual.Find("PsdReroll-reroll-smoke")?.gameObject.SetActive(false);
            // Use the lobby navigation's alpha-derived edge, not a rectangular ink frame.
            // The bar sprite already includes the heart, so this follows the complete silhouette.
            if (psdRerollArt != null)
            {
                psdRerollEdge = ChoSiren.UI.InkFlameEdge1031.Attach(psdRerollArt, .09f);
                if (psdRerollEdge != null) psdRerollEdge.color = new Color(.3f, .9f, 1f);
            }
            var rerollCount = PanelKit.LabelOf(psdReroll);
            rerollCount.transform.SetAsLastSibling();
            // Centered in the visible part of the bar's dark band (psd x 0–417, band centre
            // y ≈ 3036) and slanted with it: the band falls ~6° to the right (left high).
            PsdPlace(rerollCount.rectTransform, 24, 276, 380, 120);
            rerollCount.alignment = TextAnchor.MiddleCenter;
            rerollCount.fontSize = 22;
            rerollCount.resizeTextForBestFit = true;
            rerollCount.resizeTextMinSize = 14; rerollCount.resizeTextMaxSize = 22;
            rerollCount.fontStyle = FontStyle.BoldAndItalic;
            PanelKit.CenterPivot(rerollCount.rectTransform);
            rerollCount.rectTransform.localEulerAngles = new Vector3(0, 0, -6.2f);
            psdRerollSlide = psdReroll.AddComponent<BattleRerollSlide929>();
            psdRerollSlide.Configure(heartVisual, () => paused || closing);
            // 2026-10-23 review ("太大"): the whole reroll badge and its glow at 80%, anchored at the screen's left edge.
            var rerollRect = psdReroll.GetComponent<RectTransform>();
            Vector2 rerollSize = rerollRect.rect.size, oldPivot = rerollRect.pivot;
            rerollRect.pivot = new Vector2(0f, .5f);
            rerollRect.anchoredPosition += Vector2.Scale(rerollRect.pivot - oldPivot, rerollSize);
            rerollRect.localScale = Vector3.one * .8f;

            // 2026-10-19 PSD dice bar (L054). Its sample words are covered by the live readings.
            PsdArt("dice-bar");
            var titleCover = kit.NewImage("DiceTitleCover", psdRoot, PunkUi.SlantFill, new Color32(214, 46, 168, 255));
            titleCover.raycastTarget = false; PsdPlace(titleCover.rectTransform, 525, 5070, 275, 96);
            PsdCover(psdRoot, "DiceHandCover", 1110, 5074, 660, 96).color = new Color32(26, 19, 74, 255);
            PsdCover(psdRoot, "DiceChargeCover", 1962, 5082, 340, 90).color = new Color32(26, 19, 74, 255);
            // Keep replacement sample text INSIDE the authored lower ribbon. The former
            // solid square cover looked like a separate black card and crossed its cut edges.
            var subtitleSlot = kit.NewRect("PsdDiceSubtitle", psdRoot);
            PsdPlace(subtitleSlot, 735, 5256, 1350, 126);
            var subCover = kit.NewImage("DiceSubCover", subtitleSlot, PunkUi.SlantFill, new Color32(13, 9, 38, 255));
            PsdPlace(subCover.rectTransform, 0, 16, 1350, 88);
            subCover.raycastTarget = false;
            var diceTitle = kit.NewPlacedText(psdRoot, "队长演出", 20, Color.white, 0, 0, 160, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            PsdText(diceTitle, 525, 5070, 275, 96, 20);
            BuildCaptainPlate1026();
            PsdText(diceHandText, 1110, 5074, 660, 96, 22);
            PsdText(diceEnergyText, 1962, 5082, 340, 90, 14);
            diceEnergyText.gameObject.SetActive(false);
            diceEnergyFill.transform.parent.SetParent(psdRoot, false);
            diceEnergyFill.transform.parent.gameObject.SetActive(true);
            PsdPlace((RectTransform)diceEnergyFill.transform.parent, 538, 5182, 1760, 62);
            diceEnergyFill.transform.parent.GetComponent<Image>().color = new Color32(12, 9, 36, 255);
            diceEnergyFill.sprite = BattlePsdArt928.Sprite("reroll-fill");
            PanelKit.Stretch(diceEnergyFill.rectTransform);
            psdChargeLabel = kit.NewPlacedText(psdRoot, string.Empty, 15, Color.white, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            psdChargeLabel.name = "RerollChargeLabel";
            PsdText(psdChargeLabel, 538, 5182, 1760, 62, 14);
            psdChargeLabel.transform.SetParent(diceEnergyFill.transform.parent, false);
            PanelKit.Stretch(psdChargeLabel.rectTransform);
            psdChargeLabel.transform.SetAsLastSibling();
            psdChargeLabel.color = Color.white;
            psdChargeLabel.resizeTextForBestFit = false;
            psdChargeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            psdChargeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            psdChargeLabel.GetComponent<Outline>().effectDistance = new Vector2(1.2f, -1.2f);
            PsdText(diceInstructionText, 735, 5256, 1350, 126, 17);
            diceInstructionText.transform.SetParent(subtitleSlot, false);
            PanelKit.Stretch(diceInstructionText.rectTransform);
            // Chinese font leading is taller than the old 88-PSD-pixel line box.
            diceInstructionText.resizeTextMinSize = 16;
            diceInstructionText.verticalOverflow = VerticalWrapMode.Overflow;
            diceInstructionText.fontStyle = FontStyle.Bold;

            dicePedestals.Clear();
            for (int i = 0; i < 6; i++)
            {
                Image pedestal = PsdArt("battle-" + (41 + i));
                pedestal.color = PedestalIdle;
                dicePedestals.Add(pedestal);
                // Resting faces come from the same 3D crystal die as the tumble, so landing is seamless.
                userDiceFaceSprites[i] = ReferenceArt038.Load("Art/Reference038/vfx-die-face-" + (i + 1))
                    ?? BattlePsdArt928.Sprite("die-" + (i + 1));
                var die = diceButtons[i]; die.transform.SetParent(psdRoot, false); die.SetActive(true);
                PsdPlace(die.GetComponent<RectTransform>(), 206 + i * 418, 5452, 416, 497);
                var label = diceHoldLabels[i]; label.transform.SetParent(psdRoot, false);
                PsdPlace(label.rectTransform, 211 + i * 418, 5990, 405, 108);
                label.fontSize = 20;
                label.resizeTextForBestFit = false;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
            // PSD order: member cards (and the 队长 plate under the captain) sit above the dice bar.
            foreach (var cell in cells)
                if (cell.Side == BattleSide.Player) cell.Root.transform.SetAsLastSibling();
            psdReroll.transform.SetAsLastSibling();
            if (guardButton != null) guardButton.transform.SetAsLastSibling();
            if (guardButton != null) BuildRelicButtons1111(guardButton.transform.parent); // 2026-11-12 功能骰
            // Pause/rules/results remain real blocking overlays above all gameplay graphics.
            pauseOverlay.transform.SetAsLastSibling(); rulesOverlay.transform.SetAsLastSibling();
            if (abilityOverlay != null) abilityOverlay.transform.SetAsLastSibling();
            var pauseCard = pauseOverlay.transform.Find("PauseDialog");
            var rules = pauseCard.Find("BattleRulesEntry");
            PanelKit.PlaceTop((RectTransform)rules, 30, 240, 152, 42);
            var ability = PunkUi.Button(kit, pauseCard, "PsdPauseAbilities", "角色能力", 16,
                OpenAbilityDetails, PunkButtonStyle.Ghost);
            PanelKit.LabelOf(ability).color = PunkUi.Cyan;
            PanelKit.PlaceTop(ability.GetComponent<RectTransform>(), 198, 240, 152, 42);
            actorGlow.transform.SetParent(psdRoot, false); actorGlow.gameObject.SetActive(true);
            actorGlow.enabled = false;
            RefreshPsdDice();
            BuildVisualBattle1101(); // 2026-11-02 visual-battle: pictures instead of captions (VisualBattle1101)
        }

        private void SkinPsdCell(CellView cell)
        {
            foreach (Transform child in cell.Root.transform) child.gameObject.SetActive(false);
            cell.Background.color = Color.clear;
            cell.Background.sprite = null;
            var frame = kit.NewImage("PsdUnitFrame", cell.Root.transform,
                BattlePsdArt928.Sprite(cell.Side == BattleSide.Player ? "member-0" : "battle-57"), Color.white);
            PanelKit.Stretch(frame.rectTransform); frame.raycastTarget = false;
            psdFrames[cell] = frame;
            cell.Outline = kit.AddOutline(frame.gameObject, new Color32(95, 219, 255, 0), 1f);
            if (cell.Side == BattleSide.Player)
            {
                var rear = kit.NewImage("PsdUnitRear", cell.Root.transform, BattlePsdArt928.Sprite("member-rear-0"), Color.white);
                rear.raycastTarget = false; rear.transform.SetAsFirstSibling(); psdRearFrames[cell] = rear;
                var disc = kit.NewImage("PsdReadyDisc", cell.Root.transform, BattlePsdArt928.Sprite("album-disc"), Color.white);
                disc.raycastTarget = false; disc.transform.SetAsFirstSibling();
                psdUltRings[cell] = UltReadyRing1020.Attach(disc.rectTransform);
                var halo = kit.NewImage("PsdReadyGlow", cell.Root.transform, kit.RadialSprite(), Color.clear);
                PsdPlace(halo.rectTransform, -45, -300, 800, 1000); halo.raycastTarget = false; halo.transform.SetAsFirstSibling();
                cell.Portrait.transform.parent.gameObject.SetActive(true);
                cell.Portrait.gameObject.SetActive(true);
                cell.Portrait.raycastTarget = false;
                var portraitArea = kit.NewRect("PsdPortraitArea", cell.Root.transform);
                PsdPlace(portraitArea, 20, 60, 668, 940);
                // No portrait mask: motion can lift complete hair/ears into the stage without
                // clipping. A dedicated small motion rig also prevents scaling around the
                // entire information card's centre and dipping into its readouts.
                Transform motion = cell.Portrait.transform.parent;
                motion.SetParent(portraitArea, false);
                PanelKit.Stretch((RectTransform)motion);
                portraitArea.SetAsLastSibling();
                frame.transform.SetAsFirstSibling();
                // 2026-10-23 review: the record slides out from UNDER the sleeve frame — glow, disc and
                // the dark sleeve back below the frame, not over it.
                rear.transform.SetAsFirstSibling(); disc.transform.SetAsFirstSibling(); halo.transform.SetAsFirstSibling();
                // the ready flame burns up from behind the record, which hides its base
                if (psdUltRings.TryGetValue(cell, out UltReadyRing1020 readyFlame) && readyFlame != null)
                    readyFlame.transform.SetSiblingIndex(disc.transform.GetSiblingIndex());
                // Keep the blue sleeve's outer rim. Its old printed value bands need an opaque
                // inner core as well as the slanted live plate, so cut corners cannot reveal them.
                var bakedCover = kit.NewImage("MemberBakedValueCover", cell.Root.transform, null,
                    new Color32(8, 4, 19, 255));
                PsdPlace(bakedCover.rectTransform, 58, 820, 590, 440);
                bakedCover.raycastTarget = false;
                // The 2026-10-19 PSD frame shows its art down to the live bars; no cover plate.
                bakedCover.gameObject.SetActive(false);
                var readout = kit.NewImage("MemberLiveReadout", cell.Root.transform, PunkUi.SlantFill,
                    new Color32(8, 4, 19, 255));
                PsdPlace(readout.rectTransform, 74, 710, 560, 550);
                readout.raycastTarget = false;
                readout.gameObject.SetActive(false);
                var energy = kit.NewPlacedText(cell.Root.transform, "", 14, Color.white, 0, 0, 150, 30, TextAnchor.MiddleCenter);
                energy.gameObject.name = "PsdEnergyReadout";
                energy.fontStyle = FontStyle.BoldAndItalic;
                energy.alignment = TextAnchor.MiddleCenter;
                psdEnergyLabels[cell] = energy;
                var hpEdge = kit.NewImage("HeadHealthPaper", cell.Root.transform, kit.RoundedSprite(8), ChoSiren.UI.SirenStickerUi.Paper);
                hpEdge.raycastTarget = false;
                psdHealthEdges[cell] = hpEdge;
                var skillDock = kit.NewImage("BattleSkillDock", cell.Root.transform, kit.RoundedSprite(12), new Color32(8, 4, 19, 225));
                PsdPlace(skillDock.rectTransform, 44, 980, 620, 132);
                skillDock.raycastTarget = false;
                var glyphs = new SkillIconGraphic[4];
                for (int skill = 0; skill < glyphs.Length; skill++)
                {
                    glyphs[skill] = SkillIconVisuals.Create(skillDock.transform, "BattleSkill-" + skill,
                        string.Empty, string.Empty, ChoSiren.UI.SirenStickerUi.Paper);
                    PsdPlace(glyphs[skill].rectTransform, 12 + skill * 148, 0, 132, 132);
                }
                psdSkillGlyphs[cell] = glyphs;
                skillDock.gameObject.SetActive(false);
                // PSD skill cues: 大招 / 小招 / dice passive, all shown at the record's top-right (one at a time),
                // over a soft dark disc so the symbol reads on any stage art.
                var cueBacking = kit.NewImage("PsdSkillCueBacking", cell.Root.transform, kit.RadialSprite(), new Color(0f, 0f, 0f, 0f));
                PsdPlace(cueBacking.rectTransform, BattlePsdSkillLayout1019.CueCenterX - 190, BattlePsdSkillLayout1019.CueCenterY - 190, 380, 380);
                cueBacking.raycastTarget = false;
                psdSkillCueBackings[cell] = cueBacking;
                var column = new Image[3];
                for (int row = 0; row < column.Length; row++)
                {
                    column[row] = kit.NewImage("PsdSkillRow-" + row, cell.Root.transform, null, Color.white);
                    column[row].preserveAspect = true;
                    column[row].gameObject.SetActive(false);
                    column[row].raycastTarget = false;
                }
                psdSkillColumns[cell] = column;
                // 2026-10-20: the old yellow cone is replaced by a GPT-made golden aura (transparent art).
                Texture2D goldTex = Resources.Load<Texture2D>("UI/UltReady/ult-ready-gold");
                Sprite gold = goldTex != null ? Sprite.Create(goldTex, new Rect(0, 0, goldTex.width, goldTex.height), new Vector2(.5f, 0f), 100f) : BattlePsdArt928.Sprite("ult-pillar");
                var pillar = kit.NewImage("PsdUltPillar", cell.Root.transform, gold, new Color(1, 1, 1, 0));
                pillar.preserveAspect = true;
                pillar.raycastTarget = false;
                pillar.gameObject.AddComponent<BattleAuthoredFlow1019>().Configure(true, () => paused || closing);
                pillar.transform.SetAsFirstSibling();
                PsdPlace(pillar.rectTransform, -40, -880, 760, 1000);
                psdUltPillars[cell] = pillar;
                var attack = ChoSiren.UI.BattleAttackBadge.Create(cell.Root.transform, diceHandText.font, () => paused || closing);
                // HP sits directly above this shared blue energy/attack strip. Keep the actual
                // attack number and its gold buff tint; an extra floating +N only crowds the bar.
                PsdPlace((RectTransform)attack.transform, 74, 840, 560, 130);
                Text attackValue = attack.transform.Find("Punch/Value").GetComponent<Text>();
                attackValue.fontSize = 18;
                attackValue.resizeTextForBestFit = true;
                attackValue.resizeTextMinSize = 16; attackValue.resizeTextMaxSize = 18;
                attackValue.horizontalOverflow = HorizontalWrapMode.Wrap;
                var attackPunch = (RectTransform)attack.transform.Find("Punch");
                attackPunch.anchorMin = new Vector2(0, .18f);
                attackPunch.anchorMax = new Vector2(.6f, .82f);
                attack.transform.Find("Punch/Plate").GetComponent<Image>().color = Color.clear;
                var sword = (RectTransform)attack.transform.Find("Punch/Sword");
                sword.sizeDelta = new Vector2(12, -4);
                sword.anchoredPosition = new Vector2(2, 0);
                attackValue.rectTransform.offsetMin = new Vector2(16, 0);
                attackValue.rectTransform.offsetMax = Vector2.zero;
                Text attackGain = attack.transform.Find("Gain").GetComponent<Text>();
                attackGain.gameObject.SetActive(false);
                psdAttackBadges[cell] = attack;
                cell.Energy.Glow.gameObject.SetActive(false);
                PsdPlace(cell.Energy.Glow.rectTransform, 10, -300, 688, 1000);
                var shield = kit.NewRect("PsdGuardRing", cell.Root.transform);
                // Its sprite frame adds a 14-canvas-pixel outer rim; include that padding
                // here so an active guard cannot rise over the numeric rows.
                // Over the portrait inside the card (cards start at the disc top since 2026-10-19).
                PsdPlace(shield, 64, 280, 580, 840); PanelKit.CenterPivot(shield); // 2026-10-20: lower, covering the whole portrait down to the HP bar
                shield.gameObject.AddComponent<BattleGuardRing929>().Configure(battle, () => cell.Unit, () => paused || closing, () => speed);
                if (cell.Energy != null) cell.Energy.enabled = false;
                var album = cell.Root.AddComponent<BattleMemberAlbum929>();
                album.Configure(cell.Portrait, disc, halo, cell.Outline, () => battle.IsUltimateReady(cell.Unit), () => paused || closing, kit);
                album.SetPortraitArea(new Rect(40 * PsdScale, 70 * PsdScale, 628 * PsdScale, 930 * PsdScale));
                psdAlbums[cell] = album;
                AttachDiscSwipe1025(cell, album);
                AttachFormationDrag1102(cell);
            }
            else
            {
                cell.Portrait.gameObject.SetActive(false);
                cell.Motion = null;
            }
            foreach (var text in new[] { cell.Name, cell.HpText, cell.Status, cell.Fallen })
            {
                text.gameObject.SetActive(true); text.transform.SetAsLastSibling();
                text.fontStyle = FontStyle.BoldAndItalic; text.color = Color.white;
                var outline = UnityNull.Real(text.GetComponent<Outline>()) ?? text.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black; outline.effectDistance = new Vector2(1.1f, -1.1f);
            }
        }

        private void PositionPsdMember(CellView cell, int slot)
        {
            slot = Mathf.Clamp(slot, 0, 3);
            psdSlots[cell] = slot;
            PsdPlace(cell.Rect, PsdMemberX[slot], PsdMemberTop, 708, PsdMemberHeight);
            var entry = BattlePsdArt928.Find("member-" + slot);
            var frame = psdFrames[cell]; frame.sprite = BattlePsdArt928.Sprite(entry.key);
            float ox = PsdMemberX[slot];
            PsdPlace(frame.rectTransform, entry.x - ox, entry.y - PsdMemberTop, entry.width, entry.height);
            var rear = psdRearFrames[cell]; var back = BattlePsdArt928.Find("member-rear-" + slot);
            rear.sprite = BattlePsdArt928.Sprite(back.key);
            PsdPlace(rear.rectTransform, back.x - ox, back.y - PsdMemberTop, back.width, back.height);
            psdAlbums[cell].SetPortrait(PsdMemberPortrait(cell.Unit, cell));
        }

        private Sprite PsdMemberPortrait(BattleUnit unit, CellView cell)
        {
            // The PSD card must use the same battle-only head-to-chest crop as the live card.
            // Reusing its alpha texture preserves transparent hair and leaves full-body member
            // details and the two separately authored homepage characters untouched.
            return unit != null ? BattlePortrait(unit.Definition.Id) : null;
        }

        private void PositionPsdEnemyCell(CellView cell, RectTransform anchor)
        {
            if (PositionEnemyPlate1031(cell)) return; // 2026-10-31: plates hang beside each enemy's head

            // 2026-10-19 PSD: with one or two foes, their name plates sit in the top corners
            // (L057 left, L058 right) instead of floating over the figures.
            if (!battle.Stage.HasBossPhases && cell.Unit != null)
            {
                var active = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u.Id != bossUnitId)
                    .OrderBy(u => u.Definition.Id == "noise-wraith" ? 0 : u.Definition.Id == "neon-scout" ? 1 : 2).ToList();
                int corner = active.IndexOf(cell.Unit);
                if (active.Count <= 2 && corner >= 0)
                {
                    var tag = BattlePsdArt928.Find(corner == 0 ? "enemy-tag-l" : "enemy-tag-r");
                    cell.Rect.SetParent(psdRoot, false);
                    cell.Rect.localScale = Vector3.one;
                    PsdPlace(cell.Rect, tag.x, tag.y, tag.width, tag.height);
                    if (psdFrames.TryGetValue(cell, out Image tagFrame)) tagFrame.sprite = BattlePsdArt928.Sprite(tag.key);
                    cell.Root.transform.SetAsLastSibling();
                    return;
                }
            }
            float width = anchor.rect.width;
            float cardWidth = Mathf.Min(width, 618 * PsdScale);
            PanelKit.PlaceTop(cell.Rect, (width - cardWidth) / 2, 0, 618 * PsdScale, 318 * PsdScale);
            cell.Rect.localScale = Vector3.one * (cardWidth / (618 * PsdScale));
        }

        private void RefreshPsdEnemyFigures()
        {
            if (LayoutEnemyFigures1031()) return; // 2026-10-31: big minions / adds (EnemyStage1031)
            if (battle.Stage.HasBossPhases)
            {
                var adds = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u.Id != bossUnitId).ToList();
                foreach (var unit in battle.Units)
                {
                    if (!enemyAnchors.TryGetValue(unit.Id, out var a)) continue;
                    bool fading = enemyMotions.TryGetValue(unit.Id, out var m) && m.IsDefeating;
                    ChoSiren.UI.BattleEnter1112.Show(a, unit.Alive || fading);
                    if (unit.Id == bossUnitId) continue;
                    int i = Mathf.Max(0, adds.IndexOf(unit));
                    // 2026-10-20: all adds stay in the top row beside the boss (two per side when
                    // there are 3+), never dropping into the reroll / guard controls below.
                    bool crowded = adds.Count > 2;
                    // 2026-10-23 review #40: the adds read too small beside the boss — about 1.4x now.
                    float width = crowded ? 150 : 210;
                    float[] xs = crowded ? new float[] { 0, 570, 120, 450 } : new float[] { 0, 510 };
                    PanelKit.PlaceTop(a, xs[Mathf.Min(i, xs.Length - 1)], 290, width, 300);
                    var motion = enemyMotions[unit.Id].GetComponent<RectTransform>();
                    motion.sizeDelta = crowded ? new Vector2(150, 205) : new Vector2(205, 265);
                    enemyMotions[unit.Id].SetLayoutOrigin(new Vector2(0, -207));
                }
                return;
            }
            var active = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u.Id != bossUnitId).OrderBy(u => u.Definition.Id == "noise-wraith" ? 0 : u.Definition.Id == "neon-scout" ? 1 : 2).ToList();
            foreach (var unit in battle.Units)
            {
                if (!enemyAnchors.TryGetValue(unit.Id, out var anchor)) continue;
                bool fading = enemyMotions.TryGetValue(unit.Id, out var m) && m.IsDefeating;
                ChoSiren.UI.BattleEnter1112.Show(anchor, unit.Alive || fading);
            }
            for (int i = 0; i < active.Count; i++)
            {
                var u = active[i]; var anchor = enemyAnchors[u.Id];
                float width = active.Count <= 2 ? 1285 : 2810f / active.Count;
                float x = active.Count == 2 ? (i == 0 ? 303 : 1451) : (2946 - width * active.Count) / 2 + i * width;
                float top = 1010;
                PsdPlace(anchor, x, top, width, 2470);
                var motion = enemyMotions[u.Id].GetComponent<RectTransform>();
                motion.anchorMin = motion.anchorMax = new Vector2(.5f, 1);
                motion.pivot = Vector2.one * .5f;
                // Uniform visible height and footing; width may only constrain unusually wide silhouettes.
                var sprite = enemyFigures[u.Id].sprite;
                float ratio = sprite != null ? sprite.rect.width / sprite.rect.height : .5f;
                float visualHeight = Mathf.Min(2100, (width - 90) / Mathf.Max(.1f, ratio));
                motion.sizeDelta = new Vector2(visualHeight * ratio, visualHeight) * PsdScale;
                enemyMotions[u.Id].SetLayoutOrigin(new Vector2(0, -(3460 - top - visualHeight / 2) * PsdScale));
            }
        }

        private void RefreshPsdCell(CellView cell)
        {
            cell.Background.color = Color.clear;
            cell.Ornament.gameObject.SetActive(false);
            cell.Name.transform.SetAsLastSibling(); cell.HpText.transform.SetAsLastSibling();
            if (cell.Side == BattleSide.Player)
            {
                // 2026-10-19 PSD: only a slanted HP bar (live number) and a blue energy bar, on
                // the authored bar layers; skills float in a column above; 队长 plate below.
                int slot = psdSlots.TryGetValue(cell, out int placed) ? placed : 0;
                float ox = PsdMemberX[slot];
                if (psdHealthEdges.TryGetValue(cell, out Image hpEdge)) hpEdge.gameObject.SetActive(false);
                if (psdAttackBadges.TryGetValue(cell, out ChoSiren.UI.BattleAttackBadge attackBadge)) attackBadge.gameObject.SetActive(false);
                if (psdEnergyLabels.TryGetValue(cell, out Text energyLabel)) energyLabel.gameObject.SetActive(false);
                Sprite barSprite = BattlePsdArt928.Sprite("bar-white");
                Rect hpBox = PsdHpBars[slot], energyBox = PsdEnergyBars[slot];

                var hpTrack = (RectTransform)cell.HpFill.transform.parent;
                hpTrack.gameObject.SetActive(true);
                PsdPlace(hpTrack, hpBox.x - ox, hpBox.y - PsdMemberTop, hpBox.width, hpBox.height);
                hpTrack.localEulerAngles = Vector3.zero;
                Image hpTrackImage = hpTrack.GetComponent<Image>();
                hpTrackImage.sprite = barSprite; hpTrackImage.color = new Color32(14, 9, 30, 240);
                PanelKit.Stretch(cell.HpFill.rectTransform);
                cell.HpFill.sprite = barSprite;
                cell.HpFill.type = Image.Type.Filled; cell.HpFill.fillMethod = Image.FillMethod.Horizontal; cell.HpFill.fillOrigin = 0;
                float hpRatio = cell.Unit.MaxHp > 0 ? cell.Unit.Hp / (float)cell.Unit.MaxHp : 0f;
                cell.HpFill.color = hpRatio <= .3f ? new Color32(255, 84, 112, 255)
                    : hpRatio <= .6f ? new Color32(255, 190, 70, 255) : new Color32(70, 222, 150, 255);
                hpTrack.SetAsLastSibling();
                PsdPlace(cell.HpText.rectTransform, hpBox.x - ox, hpBox.y - PsdMemberTop - 6, hpBox.width, hpBox.height);
                cell.HpText.fontSize = 20; cell.HpText.color = Color.white;
                cell.HpText.verticalOverflow = VerticalWrapMode.Overflow;
                cell.HpText.resizeTextForBestFit = true;
                cell.HpText.resizeTextMinSize = 15; cell.HpText.resizeTextMaxSize = 20;
                cell.HpText.rectTransform.localEulerAngles = new Vector3(0, 0, 3f);
                cell.HpText.alignment = TextAnchor.MiddleCenter;
                cell.HpText.transform.SetAsLastSibling();

                cell.Energy.Fill.transform.parent.gameObject.SetActive(true);
                var energyTrack = (RectTransform)cell.Energy.Fill.transform.parent;
                PsdPlace(energyTrack, energyBox.x - ox, energyBox.y - PsdMemberTop, energyBox.width, energyBox.height);
                energyTrack.localEulerAngles = Vector3.zero;
                Image energyTrackImage = energyTrack.GetComponent<Image>();
                energyTrackImage.sprite = barSprite; energyTrackImage.color = new Color32(14, 9, 30, 240);
                PanelKit.Stretch(cell.Energy.Fill.rectTransform);
                cell.Energy.Fill.sprite = BattlePsdArt928.Sprite("bar-energy");
                cell.Energy.Fill.type = Image.Type.Filled; cell.Energy.Fill.fillMethod = Image.FillMethod.Horizontal; cell.Energy.Fill.fillOrigin = 0;
                cell.Energy.Fill.color = Color.white;
                // The white rim stays whole while the blue inside fills (2026-10-20 request).
                Transform rim = energyTrack.Find("EnergyRim");
                if (rim == null)
                {
                    var rimImage = kit.NewImage("EnergyRim", energyTrack, BattlePsdArt928.Sprite("bar-outline"), Color.white);
                    rimImage.raycastTarget = false;
                    PanelKit.Stretch(rimImage.rectTransform);
                    rim = rimImage.transform;
                }
                rim.SetAsLastSibling();
                cell.Energy.Label.gameObject.SetActive(false);
                cell.Energy.Glow.gameObject.SetActive(false);
                energyTrack.SetAsLastSibling();
                var ready = cell.Root.transform.Find("EnergyReadyMark"); if (ready != null) ready.gameObject.SetActive(false);
                cell.Name.gameObject.SetActive(false);
                cell.Status.gameObject.SetActive(false);
                cell.ShieldTrack.SetActive(false);

                bool ultReady = cell.Unit.Alive && battle.IsUltimateReady(cell.Unit);
                if (psdUltPillars.TryGetValue(cell, out Image pillar))
                {
                    // 2026-10-20: the gold aura read as ugly; readiness is the R1 neon ring on the disc.
                    pillar.color = new Color(1, 1, 1, 0f);
                    pillar.GetComponent<BattleAuthoredFlow1019>().SetShowing(false);
                    pillar.transform.SetAsFirstSibling();
                }
                if (psdUltRings.TryGetValue(cell, out UltReadyRing1020 ring) && ring != null)
                    ring.SetReady(ultReady && !(psdAlbums.TryGetValue(cell, out BattleMemberAlbum929 flown) && flown != null && flown.Launched));
                // 2026-10-26: the captain is marked by the PSD 「队长」 plate under the first slot (CaptainPlate1026),
                // not a tag on the card.
                if (cell.CaptainBadge != null) cell.CaptainBadge.SetActive(false);
                if (cell.Side == BattleSide.Player) PickHint1024(cell);
                if (cell.Unit != null && psdSkillColumns.TryGetValue(cell, out Image[] column))
                {
                    CombatRace race = battle.RaceOf(cell.Unit);
                    if (psdSkillCueBackings.TryGetValue(cell, out Image cueBack) && cueBack != null) cueBack.transform.SetAsLastSibling();
                    for (int row = 0; row < column.Length; row++)
                    {
                        var spec = BattlePsdSkillLayout1019.For(race, row);
                        column[row].sprite = BattlePsdArt928.Sprite(spec.Key);
                        Rect bounds = BattlePsdSkillLayout1019.CueBounds(race, row);
                        PsdPlace(column[row].rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
                        PanelKit.CenterPivot(column[row].rectTransform);
                        column[row].preserveAspect = false;
                        column[row].transform.SetAsLastSibling();
                    }
                    ApplyPsdSkillCue1026(cell, column);
                }
                if (cell.ActionLabel != null)
                {
                    PsdPlace(cell.ActionLabel.rectTransform, 50, -200, 600, 120);
                    cell.ActionLabel.fontSize = 20;
                }
                if (cell.HurtLabel != null)
                {
                    PsdPlace(cell.HurtLabel.rectTransform, 50, 260, 600, 120);
                    cell.HurtLabel.fontSize = 20;
                }
                PsdPlace(cell.Fallen.rectTransform, 74, 600, 560, 160);
                cell.Fallen.fontSize = 18;
                // Ongoing effects (中毒, 持续恢复…) hang over the member's head.
                for (int i = 0; i < 4; i++)
                {
                    var icon = cell.Root.transform.Find("StatusIcon-" + i);
                    if (icon != null) icon.gameObject.SetActive(false);
                }
            }
            else
            {
                PsdPlace(cell.Name.rectTransform, 95, 55, 470, 110);
                PsdPlace(cell.HpText.rectTransform, 95, 175, 470, 95);
                cell.Name.fontSize = 19; cell.Name.resizeTextMaxSize = 19;
                cell.HpText.fontSize = 16; cell.HpText.color = Color.white;
                cell.HpText.horizontalOverflow = HorizontalWrapMode.Overflow;
                cell.HpText.verticalOverflow = VerticalWrapMode.Overflow;
                cell.HpText.resizeTextForBestFit = false;
                cell.Name.alignment = cell.HpText.alignment = TextAnchor.MiddleCenter;
                cell.Name.rectTransform.localEulerAngles = cell.HpText.rectTransform.localEulerAngles = new Vector3(0, 0, 4f);
                // The plate's pink waveform is the authored health line; the live value is the number.
                cell.HpFill.transform.parent.gameObject.SetActive(false);
                cell.HpFill.transform.parent.SetAsLastSibling();
                cell.HpText.transform.SetAsLastSibling();
                cell.Status.gameObject.SetActive(false); cell.ShieldTrack.SetActive(false);
                if (cell.CastLabel != null) PsdPlace(cell.CastLabel.rectTransform, 60, 238, 510, 75);
                if (cell.CastFill != null) PsdPlace((RectTransform)cell.CastFill.transform.parent, 75, 306, 480, 12);
            }
        }

        private void RefreshPsdHud(bool warning)
        {
            phaseText.text = battle.Stage.HasBossPhases ? $"{battle.EnemyPhase}/3" : $"{battle.CurrentWave}/{battle.TotalWaves}";
            int seconds = Mathf.Max(0, battle.Stage.TimeLimitSeconds - Mathf.CeilToInt(battleElapsed));
            timerText.text = battle.IsTurnBased ? $"{Mathf.Max(1, battle.TurnRound)}/{battle.TurnRoundLimit}"
                : $"{seconds / 60:00}:{seconds % 60:00}";
            if (battle.IsTurnBased) warning = battle.TurnRound > battle.TurnRoundLimit - 3;
            timerText.color = warning ? new Color32(255, 111, 118, 255) : Color.white;
            enemyHpFill.color = Color.white;
            enemyHealthCaption.color = PanelKit.Pink;
            foreach (var button in new[] { autoButton, speedButton, retreatButton, guardButton })
                if (button != null) button.GetComponent<Image>().color = Color.clear;
            PanelKit.LabelOf(retreatButton).text = "";
        }

        private void RefreshPsdDice()
        {
            // Preserve readable, concise live values in the PSD's original slots.
            if (diceTurn == null) return;
            int available = diceTurn.AvailableRerolls;
            diceHandText.text = awaitingDiceLanding ? "投掷中…" : $"{diceTurn.Hand.DisplayName} ×{diceTurn.DamageMultiplierPermille / 1000f:0.##}";
            bool rhythm = diceTurn.RhythmMode;
            bool chargeDone = diceTurn.EarnedRerolls >= diceTurn.BattleRerollLimit;
            // The heart button already shows ×N; the charge bar says when rerolls run out (每场最多 3 次, 2026-10-31).
            diceEnergyText.text = "";
            if (psdChargeLabel != null && !diceTurn.RhythmMode)
                psdChargeLabel.text = chargeDone ? available > 0 ? "次数已发满" : "本场重投用完"
                    : $"重投充能 {diceTurn.Energy}% · 本场剩{diceTurn.BattleRerollLimit - diceTurn.RerollsUsedInBattle}次";
            PanelKit.LabelOf(psdReroll).text = $"重投×{available}";
            string benefit = CaptainBenefitLabel();
            // 2026-10-21 骰子节奏: bonus follows the current hand, heat rises with each non-worse reroll,
            // and each wave shows its hand target (达成 = 队长演出).
            string heat = rhythm && diceTurn.Heat > 0 ? $" · 热度{diceTurn.Heat}" : "";
            string target = battle.HasDiceTarget ? (battle.DiceTargetDone ? " · 目标达成✓" : $" · 目标：{battle.DiceTargetLabel}") : "";
            diceInstructionText.text = $"队长攻击 +{diceTurn.AccumulatedBonusPermille / 10f:0.#}%" + heat + target +
                (rhythm ? "" : string.IsNullOrEmpty(benefit) ? "" : " · " + benefit) +
                (diceTurn.AccumulatedBonusPermille >= diceTurn.BonusCapPermille && !rhythm ? " · 封顶" : "");
            diceInstructionText.color = new Color32(255, 214, 236, 255);
            diceEnergyText.color = PanelKit.Cyan;
            bool rerollUsable = diceTurn.CanReroll && !closing && battle.Outcome == BattleOutcome.Ongoing;
            psdRerollGlowOn = rerollUsable;
            psdRerollSlide.SetState(diceTurn.CanReroll && !closing && battle.Outcome == BattleOutcome.Ongoing,
                awaitingInput && !paused && !awaitingDiceLanding);
            diceEnergyFill.fillAmount = chargeDone ? 0 : diceTurn.Energy / 100f;
            if (diceTurn.RhythmMode) RefreshDiceEndless1101(PanelKit.LabelOf(psdReroll)); // 2026-11-01c 骰子豆 / 加成条
            for (int i = 0; i < diceHoldLabels.Count; i++)
            {
                // Selection is shown by the die itself (lift + pink glow), not by a label.
                diceHoldLabels[i].gameObject.SetActive(false);
                if (i < diceButtons.Count)
                    DiceRerollMark1020.Attach(diceButtons[i], kit.RadialSprite()).SetSelected(!diceTurn.Held[i] && diceTurn.IsBattleSession && battle.Outcome == BattleOutcome.Ongoing); // same lift with or without a charge (2026-10-20)
            }
        }
    
        // ------------------------------------------------------------------ captain tag (2026-10-24)

        static readonly Color32 TagInk1024 = new Color32(14, 8, 20, 255);

        private Graphic Skew1024(Transform parent, string name, Color color, float x, float y, float w, float h, bool hit)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ChoSiren.UI.SkewedPlate1021));
            go.transform.SetParent(parent, false);
            var g = go.GetComponent<ChoSiren.UI.SkewedPlate1021>();
            g.color = color; g.SkewDegrees = -14f; g.raycastTarget = hit;
            PsdPlace(g.rectTransform, x, y, w, h);
            return g;
        }

        private Text TagText1024(Transform parent, string name, string text, int size, Color color)
        {
            Text t = kit.NewPlacedText(parent, text, size, color, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            t.name = name; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            PanelKit.Stretch(t.rectTransform);
            return t;
        }

        /// <summary>While choosing a captain (after tapping the PSD 「队长」 plate), every other living member shows a teal
        /// 「设为队长」 tag on her card.</summary>
        private void PickHint1024(CellView cell)
        {
            bool show = pickingCaptain1024 && cell.Unit != null && cell.Unit.Alive && cell.Unit.Id != battle.CurrentLeaderId;
            Transform hint = cell.Root.transform.Find("PickHint1025");
            if (hint == null)
            {
                if (!show) return;
                var root = kit.NewRect("PickHint1025", cell.Root.transform);
                PsdPlace(root, -34, 286, 680, 176);
                var tilt = kit.NewRect("Tilt", root);
                PanelKit.Stretch(tilt);
                tilt.localEulerAngles = new Vector3(0f, 0f, 5f);
                Skew1024(tilt, "Shadow", TagInk1024, 20, 20, 520, 150, false);
                var plate = Skew1024(tilt, "Plate", Brand1025.Teal, 0, 0, 520, 150, false);
                TagText1024(plate.transform, "Label", "设为队长", 18, Brand1025.Ink);
                hint = root;
            }
            hint.gameObject.SetActive(show);
            if (show)
            {
                hint.SetAsLastSibling();
                float pulse = BattlePresentationOptions.ReduceMotion ? 1f : 1f + .05f * Mathf.Sin(Time.unscaledTime * 8f);
                hint.localScale = Vector3.one * pulse;
            }
        }
    }
}

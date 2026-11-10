using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Practice settlement card (2026-11-01): a LEVEL UP sticker slams in, the trainee's figure
    /// stands on torn teal paper, the level flips with a white flash and sparks, power and the four
    /// stage stats roll from before to after and their +N stickers pop. Reduce-motion shows the same
    /// final numbers with a short fade. Every row keeps the "before → after" text the tests read.
    /// </summary>
    public sealed partial class MemberPracticePanel
    {
        private const float CardW = 640f, CardH = 850f;
        private static readonly string[] DeltaLabels = { "等级", "战力", "声能", "律动", "气场", "共鸣" };

        private GameObject resultGroup;
        private RectTransform resultCard, resultBanner, resultRays, levelRow;
        private CanvasGroup resultFade;
        private Text resultTitle, resultName, resultCost;
        private Image resultFigure, resultFlash;
        private readonly Text[] deltaTexts = new Text[6];
        private readonly Image[] deltaBadges = new Image[6];
        private readonly Text[] deltaBadgeTexts = new Text[6];
        private readonly Image[] statBefore = new Image[4];
        private readonly Image[] statAfter = new Image[4];
        private readonly List<Image> sparks = new List<Image>(10);
        private GameObject againObject;
        private Coroutine resultRoutine;

        private void BuildResult()
        {
            resultGroup = new GameObject("PracticeResult", typeof(RectTransform), typeof(CanvasGroup));
            resultGroup.transform.SetParent(transform, false);
            PanelKit.Stretch((RectTransform)resultGroup.transform);
            resultFade = resultGroup.GetComponent<CanvasGroup>();

            Image dim = kit.NewImage("PracticeResultDim", resultGroup.transform, null, new Color32(6, 4, 16, 222));
            PanelKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            resultCard = kit.NewRect("PracticeResultCard", resultGroup.transform);
            resultCard.anchorMin = resultCard.anchorMax = resultCard.pivot = new Vector2(.5f, .5f);
            resultCard.sizeDelta = new Vector2(CardW, CardH);
            resultCard.anchoredPosition = new Vector2(0f, 10f);

            Image rays = BattleFxArt1021.NewImage("PracticeResultRays", resultCard, PracticeFx1101.Rays,
                new Color(.35f, 1f, .95f, .14f), true);
            rays.gameObject.AddComponent<Canvas>();
            resultRays = rays.rectTransform;
            resultRays.anchoredPosition = new Vector2(0f, 230f);
            resultRays.sizeDelta = new Vector2(1180f, 1180f);

            Image slab = kit.NewImage("PracticeResultSlab", resultCard, null, PunkUi.Hot);
            PanelKit.Stretch(slab.rectTransform, 14f, -14f, 14f, -14f);
            slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.4f);
            Image face = kit.NewImage("PracticeResultFace", resultCard, SirenStickerUi.PanelSprite, Color.white);
            face.type = Image.Type.Sliced;
            PanelKit.Stretch(face.rectTransform);
            Transform c = face.transform;

            // LEVEL UP sticker overlapping the top edge.
            Image bannerBack = kit.NewImage("PracticeResultBannerBack", c, PracticeFx1101.Slab, PunkUi.Hot);
            PracticeFx1101.PlaceCenter(bannerBack.rectTransform, CardW * .5f + 8f, 22f, 420f, 92f);
            bannerBack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            Image banner = kit.NewImage("PracticeResultBanner", c, SirenStickerUi.PrimarySprite, Color.white);
            banner.type = Image.Type.Sliced;
            PracticeFx1101.PlaceCenter(banner.rectTransform, CardW * .5f, 14f, 400f, 88f);
            banner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            resultBanner = banner.rectTransform;
            Label(banner.transform, "Label", "LEVEL UP!", 50, Brand1025.Ink, 0f, 0f, 400f, 88f,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            resultTitle = Label(c, "PracticeResultTitle", string.Empty, 21, SirenStickerUi.Paper, 0f, 72f, CardW, 32f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PracticeFx1101.Ink(resultTitle, 1.4f);

            // Figure on torn teal paper with a white sticker rim.
            var paperNode = new GameObject("PracticeResultPaper", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TornPaper1025));
            paperNode.transform.SetParent(c, false);
            TornPaper1025 paper = paperNode.GetComponent<TornPaper1025>();
            paper.color = Brand1025.Teal;
            paper.raycastTarget = false;
            PracticeFx1101.PlaceCenter(paper.rectTransform, 138f, 276f, 236f, 322f);
            paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            paperNode.AddComponent<Mask>().showMaskGraphic = true;
            Image paperDots = kit.NewImage("PracticeResultPaperDots", paper.transform, CelShapes.Halftone,
                new Color(.055f, .03f, .08f, .3f));
            paperDots.type = Image.Type.Tiled;
            paperDots.pixelsPerUnitMultiplier = 2f;
            PanelKit.Stretch(paperDots.rectTransform);
            // 2026-11-10 (少爷 + 美术: 别用半Q版小人，换成设计好的立绘): the member's own portrait, cut to the
            // waist and masked by the torn paper (turned back upright so she stands straight).
            var figureRoot = kit.NewRect("PracticeResultFigureRoot", paper.transform);
            PanelKit.Stretch(figureRoot, -14f, -14f, 14f, 14f);
            figureRoot.localRotation = Quaternion.Euler(0f, 0f, 5f);
            resultFigure = kit.NewImage("PracticeResultFigure", figureRoot, null, Color.white);
            resultFigure.raycastTarget = false;
            PanelKit.Stretch(resultFigure.rectTransform);
            Image namePlate = kit.NewImage("PracticeResultNamePlate", c, SirenStickerUi.ButtonSprite, Color.white);
            namePlate.type = Image.Type.Sliced;
            PanelKit.PlaceTop(namePlate.rectTransform, 40f, 432f, 200f, 44f);
            resultName = Label(namePlate.transform, "Label", string.Empty, 22, SirenStickerUi.Paper, 0f, 0f, 200f, 44f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PracticeFx1101.Ink(resultName, 1.2f);

            // Level / power on the right.
            Label(c, "PracticeResultLevelTag", "等级 LEVEL", 18, SirenStickerUi.Muted, 274f, 112f, 220f, 26f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            levelRow = kit.NewRect("PracticeResultLevelRow", c);
            PanelKit.PlaceTop(levelRow, 274f, 138f, 344f, 84f);
            PanelKit.CenterPivot(levelRow);
            deltaTexts[0] = DeltaText(levelRow, "PracticeDelta-等级", 66, PanelKit.Gold, 0f, 0f, 262f, 84f);
            deltaBadges[0] = Badge(levelRow, "PracticeDeltaBadge-等级", 266f, 22f, 78f, 42f, out deltaBadgeTexts[0], 24);

            Label(c, "PracticeResultPowerTag", "战力 POWER", 18, SirenStickerUi.Muted, 274f, 236f, 220f, 26f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            deltaTexts[1] = DeltaText(c, "PracticeDelta-战力", 40, SirenStickerUi.Paper, 274f, 262f, 262f, 58f);
            deltaBadges[1] = Badge(c, "PracticeDeltaBadge-战力", 540f, 270f, 78f, 40f, out deltaBadgeTexts[1], 20);

            Image costRule = kit.NewImage("PracticeResultCostRule", c, null, new Color32(137, 62, 255, 150));
            PanelKit.PlaceTop(costRule.rectTransform, 274f, 340f, 344f, 2f);
            Image coin = kit.NewImage("PracticeResultCoin", c, PanelKit.CurrencyIcon("gold"), Color.white);
            coin.preserveAspect = true;
            PanelKit.PlaceTop(coin.rectTransform, 274f, 360f, 40f, 40f);
            resultCost = Label(c, "PracticeResultCost", string.Empty, 18, PanelKit.Gold, 322f, 352f, 296f, 60f,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            resultCost.horizontalOverflow = HorizontalWrapMode.Wrap;
            Label(c, "PracticeResultSynced", "舞台数值已同步到成员档案", 15, SirenStickerUi.Muted, 274f, 414f, 344f, 24f,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            // Four stage stats in a 2×2 grid.
            Label(c, "PracticeResultStatsTag", "属性成长 · STAGE STATS", 18, PunkUi.Hot, 24f, 486f, 400f, 26f,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            ReferencePunkFx.StatGlyphKind[] glyphs =
            {
                ReferencePunkFx.StatGlyphKind.Wave, ReferencePunkFx.StatGlyphKind.Note,
                ReferencePunkFx.StatGlyphKind.Spark, ReferencePunkFx.StatGlyphKind.Gem,
            };
            for (int i = 0; i < 4; i++)
            {
                float x = 24f + (i % 2) * 302f, y = 518f + (i / 2) * 102f;
                Image cell = kit.NewImage("PracticeResultStat-" + StatNames[i], c, SirenStickerUi.QuietPanelSprite, Color.white);
                cell.type = Image.Type.Sliced;
                PanelKit.PlaceTop(cell.rectTransform, x, y, 290f, 94f);
                Image glyph = kit.NewImage("Glyph", cell.transform, ReferencePunkFx.StatGlyph(glyphs[i]), StatColors[i]);
                glyph.preserveAspect = true;
                PanelKit.PlaceTop(glyph.rectTransform, 14f, 12f, 24f, 24f);
                Label(cell.transform, "Name", StatNames[i], 20, SirenStickerUi.Paper, 44f, 8f, 120f, 32f,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                deltaTexts[2 + i] = DeltaText(cell.transform, "PracticeDelta-" + StatNames[i], 30, SirenStickerUi.Paper,
                    14f, 38f, 188f, 40f);
                deltaBadges[2 + i] = Badge(cell.transform, "PracticeDeltaBadge-" + StatNames[i], 200f, 40f, 76f, 34f,
                    out deltaBadgeTexts[2 + i], 19);
                statBefore[i] = kit.NewBar("BarTrack", cell.transform, 14f, 80f, 262f, 6f,
                    new Color32(40, 30, 64, 255), new Color(StatColors[i].r, StatColors[i].g, StatColors[i].b, .35f), 3);
                statAfter[i] = kit.NewImage("BarAfter", statBefore[i].transform.parent, kit.RoundedSprite(3), StatColors[i]);
                statAfter[i].type = Image.Type.Filled;
                statAfter[i].fillMethod = Image.FillMethod.Horizontal;
                PanelKit.Stretch(statAfter[i].rectTransform);
                statAfter[i].transform.SetAsLastSibling();
            }

            againObject = SecondaryTasks038Chrome.Button(c, "PracticeAgain", "再练一次", 28, () =>
            {
                HideResult();
                StartPractice();
            }, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(againObject.GetComponent<RectTransform>(), 24f, 738f, 290f, 76f);
            GameObject ok = SecondaryTasks038Chrome.Button(c, "PracticeResultOk", "确定", 30, HideResult,
                ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(ok.GetComponent<RectTransform>(), 326f, 738f, 290f, 76f);

            // A burst of light on the level number, never a grey wash over the whole card.
            resultFlash = BattleFxArt1021.NewImage("PracticeResultFlash", c, BattleFxArt1021.Dot, new Color(1f, 1f, 1f, 0f), true);
            PracticeFx1101.PlaceCenter(resultFlash.rectTransform, 400f, 180f, 760f, 460f);
            for (int i = 0; i < 10; i++)
            {
                Image spark = BattleFxArt1021.NewImage("PracticeResultSpark-" + i, c,
                    i % 2 == 0 ? BattleFxArt1021.Star : BattleFxArt1021.Spark,
                    i % 3 == 0 ? (Color)Brand1025.Teal : PanelKit.Gold, true);
                spark.gameObject.SetActive(false);
                sparks.Add(spark);
            }

            resultGroup.SetActive(false);
        }

        private Text DeltaText(Transform parent, string name, int size, Color color, float x, float y, float w, float h)
        {
            Text text = Label(parent, name, string.Empty, size, color, x, y, w, h, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            PanelKit.EnableBestFit(text, Mathf.Max(14, size / 2));
            PracticeFx1101.Ink(text, size > 50 ? 2.2f : 1.4f);
            return text;
        }

        private Image Badge(Transform parent, string name, float x, float y, float w, float h, out Text label, int size)
        {
            Image badge = kit.NewImage(name, parent, PracticeFx1101.Slab, Brand1025.Teal);
            PanelKit.PlaceTop(badge.rectTransform, x, y, w, h);
            PanelKit.CenterPivot(badge.rectTransform);
            label = Label(badge.transform, "Label", string.Empty, size, Brand1025.Ink, 0f, 0f, w, h,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PanelKit.EnableBestFit(label, 11);
            return badge;
        }

        // ---------------------------------------------------------------- content

        private int[] Befores(MemberPracticeOutcome o) =>
            new[] { o.LevelBefore, o.PowerBefore, o.VocalBefore, o.RhythmBefore, o.PresenceBefore, o.ResonanceBefore };

        private int[] Afters(MemberPracticeOutcome o) =>
            new[] { o.LevelAfter, o.PowerAfter, o.VocalAfter, o.RhythmAfter, o.PresenceAfter, o.ResonanceAfter };

        private static string Pair(int before, long shown, int smallSize) =>
            $"<size={smallSize}><color=#A79DC4>{before:N0}</color> → </size>{shown:N0}";

        private void SetDelta(int row, int before, long shown)
        {
            int small = row == 0 ? 34 : row == 1 ? 24 : 18;
            deltaTexts[row].text = Pair(before, shown, small);
        }

        /// <summary>Writes the final numbers (no animation) and stages the card for the member.</summary>
        private void FillOutcome(MemberPracticeOutcome outcome)
        {
            if (outcome == null || resultGroup == null) return;
            MemberDefinition member = GameModel.Members[memberIndex];
            int[] before = Befores(outcome), after = Afters(outcome);
            for (int i = 0; i < 6; i++)
            {
                SetDelta(i, before[i], after[i]);
                StyleBadge(i, before[i], after[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                statBefore[i].fillAmount = Mathf.Clamp01(before[2 + i] / (float)GameModel.StageStatMax);
                statAfter[i].fillAmount = Mathf.Clamp01(after[2 + i] / (float)GameModel.StageStatMax);
            }

            resultCost.text = outcome.CostLine;
            resultTitle.text = $"练习完成 · {member.Name} 的{(plan != null ? plan.ActionName : "练习")}";
            resultName.text = $"{member.Name}  Lv.{outcome.LevelAfter}";
            Sprite figure = stage != null ? ResultFigureSprite(member) : null;
            resultFigure.sprite = figure;
            resultFigure.enabled = figure != null;
        }

        /// <summary>The designed portrait (the one the member page shows), cut to the waist for the paper frame.</summary>
        private Sprite ResultFigureSprite(MemberDefinition member)
        {
            Sprite art = string.IsNullOrEmpty(member.ResourcePath) ? null : Resources.Load<Sprite>(member.ResourcePath);
            if (art == null) return null;
            RectTransform frame = resultFigure.rectTransform;
            float aspect = frame.rect.width > 1f && frame.rect.height > 1f ? frame.rect.width / frame.rect.height : 264f / 350f;
            return PortraitFace1101.CropSprite(member.Id, art, aspect, PortraitFace1101.Framing.Half);
        }

        private void StyleBadge(int row, int before, int after)
        {
            string delta = MemberTrainingPractice.FormatDelta(before, after);
            deltaBadgeTexts[row].text = delta;
            bool up = after > before, down = after < before;
            deltaBadges[row].color = up ? (Color)Brand1025.Teal : down ? PunkUi.Hot : new Color32(52, 42, 76, 255);
            deltaBadgeTexts[row].color = up ? (Color)Brand1025.Ink : down ? SirenStickerUi.Paper : SirenStickerUi.Muted;
            deltaTexts[row].color = row == 0 ? PanelKit.Gold : up ? SirenStickerUi.Cyan : SirenStickerUi.Paper;
        }

        // ---------------------------------------------------------------- show / hide

        private void ShowResultFinal()
        {
            if (resultGroup == null || lastOutcome == null) return;
            FillOutcome(lastOutcome);
            resultGroup.SetActive(true);
            resultGroup.transform.SetAsLastSibling();
            resultFade.alpha = 1f;
            resultCard.localScale = Vector3.one * pageScale;
            resultBanner.localScale = Vector3.one;
            levelRow.localScale = Vector3.one;
            resultFlash.color = new Color(1f, 1f, 1f, 0f);
            foreach (Image spark in sparks) spark.gameObject.SetActive(false);
            for (int i = 0; i < 6; i++) deltaBadges[i].rectTransform.localScale = Vector3.one;
            RefreshQuote();
        }

        private void RevealResult()
        {
            ShowResultFinal();
            if (!Application.isPlaying) return;
            if (resultRoutine != null) StopCoroutine(resultRoutine);
            resultRoutine = StartCoroutine(RunResult1107(reduceMotion || BattlePresentationOptions.ReduceMotion
                ? FadeResultIn() : RollResultIn()));
        }

        /// <summary>Runs a reveal and then clears it, hiding the 跳过 chip once nothing is left to skip.</summary>
        private IEnumerator RunResult1107(IEnumerator reveal)
        {
            yield return reveal;
            resultRoutine = null;
            if (skipButton1107 != null) skipButton1107.SetActive(false);
        }

        private void HideResult()
        {
            if (resultRoutine != null)
            {
                StopCoroutine(resultRoutine);
                resultRoutine = null;
            }

            if (resultGroup != null) resultGroup.SetActive(false);
        }

        private IEnumerator FadeResultIn()
        {
            float t = 0f;
            while (t < .25f)
            {
                t += Time.unscaledDeltaTime;
                resultFade.alpha = Mathf.Clamp01(t / .25f);
                yield return null;
            }

            resultFade.alpha = 1f;
            resultRoutine = null;
        }

        private IEnumerator RollResultIn()
        {
            MemberPracticeOutcome outcome = lastOutcome;
            int[] before = Befores(outcome), after = Afters(outcome);
            const float total = 1.7f, levelAt = .45f;
            bool flashed = false;
            float t = 0f;
            for (int i = 0; i < 6; i++)
            {
                SetDelta(i, before[i], before[i]);
                deltaBadges[i].rectTransform.localScale = Vector3.zero;
            }

            for (int i = 0; i < 4; i++) statAfter[i].fillAmount = statBefore[i].fillAmount;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                resultFade.alpha = Mathf.Clamp01(t / .15f);
                resultCard.localScale = Vector3.one * pageScale * Mathf.Lerp(.84f, 1f, PracticeFx1101.EaseOutBack(t / .35f));
                float slam = Mathf.Clamp01((t - .1f) / .22f);
                resultBanner.localScale = Vector3.one * (t < .1f ? 0f : Mathf.Lerp(1.9f, 1f, PracticeFx1101.EaseOutCubic(slam)));

                if (t >= levelAt && !flashed)
                {
                    flashed = true;
                    SetDelta(0, before[0], after[0]);
                    ModalPolish038Chrome.PlaySuccess();
                }

                float sinceLevel = t - levelAt;
                levelRow.localScale = Vector3.one * (sinceLevel < 0f ? 1f : 1f + .28f * Mathf.Exp(-sinceLevel * 9f));
                resultFlash.color = new Color(1f, .96f, .8f, sinceLevel < 0f ? 0f : Mathf.Exp(-sinceLevel * 6f));
                PopBadge(0, sinceLevel);
                UpdateSparks(sinceLevel);

                Roll(1, before[1], after[1], t, .5f, .75f);
                for (int i = 0; i < 4; i++)
                {
                    float start = .62f + i * .1f;
                    Roll(2 + i, before[2 + i], after[2 + i], t, start, .5f);
                    float k = PracticeFx1101.EaseOutCubic((t - start) / .5f);
                    statAfter[i].fillAmount = Mathf.Lerp(statBefore[i].fillAmount,
                        Mathf.Clamp01(after[2 + i] / (float)GameModel.StageStatMax), k);
                }

                yield return null;
            }

            ShowResultFinal();
            resultRoutine = null;
        }

        private void Roll(int row, int from, int to, float t, float start, float length)
        {
            float k = PracticeFx1101.EaseOutCubic((t - start) / length);
            SetDelta(row, from, (long)Mathf.Round(Mathf.Lerp(from, to, k)));
            PopBadge(row, t - start - length);
        }

        private void PopBadge(int row, float since)
        {
            RectTransform badge = deltaBadges[row].rectTransform;
            badge.localScale = since < 0f ? Vector3.zero
                : Vector3.one * Mathf.Lerp(1.5f, 1f, PracticeFx1101.EaseOutCubic(since / .2f));
        }

        private void UpdateSparks(float since)
        {
            Vector2 origin = new Vector2(274f + 130f, -(138f + 42f));
            for (int i = 0; i < sparks.Count; i++)
            {
                Image spark = sparks[i];
                float life = since / .8f;
                bool live = since >= 0f && life < 1f;
                if (spark.gameObject.activeSelf != live) spark.gameObject.SetActive(live);
                if (!live) continue;
                float angle = i / (float)sparks.Count * Mathf.PI * 2f + .3f;
                float dist = Mathf.Lerp(20f, 190f + (i % 3) * 30f, PracticeFx1101.EaseOutCubic(life));
                RectTransform r = spark.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.anchoredPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                r.sizeDelta = i % 2 == 0 ? new Vector2(46f, 46f) : new Vector2(70f, 18f);
                r.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + life * 90f);
                Color col = spark.color;
                col.a = 1f - life;
                spark.color = col;
            }
        }

        private void UpdateResultAmbient()
        {
            if (resultRays == null || !resultGroup.activeSelf || reduceMotion || BattlePresentationOptions.ReduceMotion) return;
            resultRays.localRotation = Quaternion.Euler(0f, 0f, resultRays.localEulerAngles.z + Time.unscaledDeltaTime * 12f);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Compact practice feedback at the existing training entry. One tap commits the same
    /// gold-only level-up (GameModel.Train), then plays a 1–2 s career practice move over the
    /// member portrait and shows the readable before → after stat deltas. Reduce-motion keeps
    /// the same information with a 0.35 s fade. No diamond cost is introduced anywhere here.
    /// </summary>
    public sealed class MemberPracticePanel : MonoBehaviour
    {
        private const string StageResourcePath = "Art/TeamAI/team-stellar-stage-bg-ai-v1-20260903";
        private const string ReduceMotionKey = MemberTrainingPractice.ReduceMotionPreferenceKey;

        private PanelKit kit;
        private GameModel model;
        private int memberIndex;
        private Action onFinished;
        private Action<string> onMessage;

        private RectTransform portraitRect;
        private Image portraitGlow;
        private readonly List<Image> rings = new List<Image>(3);
        private Text actionText;
        private Text quoteText;
        private Text startLabel;
        private Button startButton;
        private Text reduceMotionLabel;
        private GameObject resultGroup;
        private Text resultHeadline;
        private Text resultCost;
        private readonly Text[] deltaTexts = new Text[6];

        private bool reduceMotion;
        private bool animating;
        private int quoteGold;
        private MemberPracticePlan plan;
        private MemberPracticeOutcome lastOutcome;

        public bool ReduceMotion => reduceMotion;
        public MemberPracticeOutcome LastOutcome => lastOutcome;
        public string LastHeadline => resultHeadline != null ? resultHeadline.text : string.Empty;
        public string LastCostLine => resultCost != null ? resultCost.text : string.Empty;
        public bool IsAnimating => animating;

        public static MemberPracticePanel Open(Transform host, GameModel gameModel, int member,
            Action finished = null, Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (member < 0 || member >= GameModel.Members.Length)
                throw new ArgumentOutOfRangeException(nameof(member));

            MemberPracticePanel existing = host.GetComponentInChildren<MemberPracticePanel>(true);
            if (existing != null) DestroyPanelObject(existing.gameObject);

            GameObject root = PanelKit.CreateOverlayRoot("MemberPracticePanel", host);
            MemberPracticePanel panel = root.AddComponent<MemberPracticePanel>();
            panel.Initialize(gameModel, member, finished, message);
            return panel;
        }

        private void Initialize(GameModel gameModel, int member, Action finished, Action<string> message)
        {
            model = gameModel;
            memberIndex = member;
            onFinished = finished;
            onMessage = message;
            reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            kit = new PanelKit("MemberPractice");
            kit.BuildBackdrop(transform);
            BuildStage();
            BuildCard();
            RefreshQuote();
        }

        private void OnDestroy()
        {
            if (kit != null) kit.Dispose();
        }

        // ---------------------------------------------------------------- construction

        private void BuildStage()
        {
            Sprite stageSprite = Resources.Load<Sprite>(StageResourcePath);
            if (stageSprite != null)
            {
                Image stage = kit.NewImage("PracticeStage", transform, stageSprite, new Color(1f, 1f, 1f, .55f));
                PanelKit.Stretch(stage.rectTransform);
                stage.preserveAspect = false;
                stage.raycastTarget = false;
            }

            Image veil = kit.NewImage("PracticeVeil", transform, null, new Color32(4, 5, 28, 96));
            PanelKit.Stretch(veil.rectTransform);
            veil.raycastTarget = false;

            kit.NewHeader(transform, "训练室", "紧凑练习", Close, "PracticeBack");
        }

        private void BuildCard()
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject card = kit.NewPanel("PracticeCard", transform, new Color32(14, 18, 48, 246), 26);
            PanelKit.PlaceTop(card.GetComponent<RectTransform>(), 30, 130, 660, 1020);
            kit.AddOutline(card, new Color32(122, 160, 224, 90), 1f);

            // Portrait slot reuses the existing live portrait; the glow is the same radial
            // sprite family used by the audition/gacha stage so no new art is required.
            portraitGlow = kit.NewImage("PracticePortraitGlow", card.transform, kit.RadialSprite(),
                new Color32(96, 150, 255, 70));
            PanelKit.PlaceTop(portraitGlow.rectTransform, 6, 10, 300, 360);

            GameObject portraitFrame = kit.NewPanel("PracticePortraitFrame", card.transform,
                new Color32(10, 14, 42, 220), 20);
            PanelKit.PlaceTop(portraitFrame.GetComponent<RectTransform>(), 28, 24, 264, 340);
            // 胸部以上取景：圆角框遮罩 + 按成员取景表定位。
            portraitFrame.AddComponent<Mask>().showMaskGraphic = true;
            Image portrait = kit.NewImage("PracticePortrait", portraitFrame.transform,
                Resources.Load<Sprite>(member.ResourcePath), Color.white);
            portraitRect = portrait.GetComponent<RectTransform>();
            Image portraitImage = portrait.GetComponent<Image>();
            PanelKit.FrameBustPortrait(portraitImage, portraitFrame.GetComponent<RectTransform>(), member.Id);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            for (int index = 0; index < 3; index++)
            {
                Image ring = kit.NewImage("PracticeRing-" + index, portraitFrame.transform, kit.RadialSprite(),
                    new Color32(120, 220, 255, 0));
                PanelKit.PlaceTop(ring.rectTransform, 22, 22, 220, 296);
                PanelKit.CenterPivot(ring.rectTransform);
                ring.raycastTarget = false;
                rings.Add(ring);
            }

            kit.NewPlacedText(card.transform, member.Name, 30, PanelKit.White,
                312, 26, 320, 44, TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(card.transform, $"{member.Career} · 等级 {model.LevelOf(memberIndex)}", 16,
                PanelKit.Pink, 312, 72, 320, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(card.transform, "练习动作", 14, PanelKit.Muted,
                312, 112, 320, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            actionText = kit.NewPlacedText(card.transform, string.Empty, 20, PanelKit.Cyan,
                312, 138, 320, 62, TextAnchor.UpperLeft, FontStyle.Bold);
            actionText.name = "PracticeAction";

            quoteText = kit.NewPlacedText(card.transform, string.Empty, 16, PanelKit.Gold,
                312, 208, 320, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            quoteText.name = "PracticeQuoteGold";
            Text noDiamond = kit.NewPlacedText(card.transform, "不消耗星钻 · 升级只扣星光币", 14,
                new Color32(120, 255, 170, 255), 312, 240, 320, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            noDiamond.name = "PracticeNoDiamond";

            GameObject start = kit.NewButton("StartPractice", card.transform, "开始练习", 20,
                new Color32(201, 92, 148, 255), PanelKit.White, StartPractice, 20);
            PanelKit.PlaceTop(start.GetComponent<RectTransform>(), 312, 282, 320, 76);
            startButton = start.GetComponent<Button>();
            startLabel = start.transform.Find("Label") != null
                ? start.transform.Find("Label").GetComponent<Text>() : null;
            if (startLabel != null) startLabel.name = "StartPracticeLabel";

            GameObject motion = kit.NewButton("PracticeReduceMotion", card.transform, string.Empty, 15,
                new Color32(38, 46, 82, 245), PanelKit.White, ToggleReduceMotion, 16);
            PanelKit.PlaceTop(motion.GetComponent<RectTransform>(), 312, 374, 320, 52);
            reduceMotionLabel = motion.transform.Find("Label") != null
                ? motion.transform.Find("Label").GetComponent<Text>() : null;
            if (reduceMotionLabel != null) reduceMotionLabel.name = "PracticeReduceMotionLabel";
            RefreshReduceMotionLabel();

            BuildResultGroup(card.transform);
            kit.NewPlacedText(card.transform,
                "练习只提升当前成员，消耗沿用现有星光币公式；升级不消耗星钻。\n完整训练室 Q 版歌舞为后续扩展，本轮先交付可玩的轻量练习反馈。",
                13, PanelKit.Muted, 28, 900, 604, 70, TextAnchor.UpperLeft);

            GameObject done = kit.NewButton("PracticeDone", transform, "完成 · 返回档案", 18,
                new Color32(70, 46, 118, 245), PanelKit.White, Close, 20);
            PanelKit.PlaceTop(done.GetComponent<RectTransform>(), 30, 1168, 660, 62);
        }

        private void BuildResultGroup(Transform parent)
        {
            resultGroup = kit.NewPanel("PracticeResult", parent, new Color32(10, 14, 40, 236), 20);
            PanelKit.PlaceTop(resultGroup.GetComponent<RectTransform>(), 28, 452, 604, 430);
            kit.AddOutline(resultGroup, new Color32(120, 220, 255, 110), 1f);
            kit.NewPlacedText(resultGroup.transform, "练习结果 · 前后对比", 15, PanelKit.White,
                18, 12, 568, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            resultHeadline = kit.NewPlacedText(resultGroup.transform, string.Empty, 19, PanelKit.Cyan,
                18, 44, 568, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            resultHeadline.name = "PracticeResultHeadline";
            PanelKit.EnableBestFit(resultHeadline, 14);

            string[] labels = { "等级", "战力", "攻击", "生命", "暴击", "速度" };
            for (int index = 0; index < deltaTexts.Length; index++)
            {
                float x = 18 + index % 2 * 292;
                float y = 88 + index / 2 * 74;
                Text line = kit.NewPlacedText(resultGroup.transform, string.Empty, 15, PanelKit.White,
                    x, y, 278, 60, TextAnchor.UpperLeft);
                line.name = "PracticeDelta-" + labels[index];
                deltaTexts[index] = line;
            }

            resultCost = kit.NewPlacedText(resultGroup.transform, string.Empty, 14,
                new Color32(120, 255, 170, 255), 18, 314, 568, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            resultCost.name = "PracticeResultCost";
            kit.NewPlacedText(resultGroup.transform, "结果按保存后的真实数值重新计算，不使用预估值。", 12,
                PanelKit.Muted, 18, 348, 568, 26, TextAnchor.MiddleLeft);
            resultGroup.SetActive(false);
        }

        // ---------------------------------------------------------------- state

        private void RefreshQuote()
        {
            bool canTrain = model.CanTrain(memberIndex, out quoteGold, out string reason);
            MemberDefinition member = GameModel.Members[memberIndex];
            plan = MemberTrainingPractice.BuildPlan(member.Career, quoteGold, reduceMotion);
            actionText.text = $"{plan.ActionName} · {plan.FocusLabel}\n节奏动效 {plan.DurationSeconds:0.##} 秒";
            quoteText.text = canTrain || quoteGold > 0
                ? $"本次消耗 星光币 {quoteGold:N0} · 持有 {model.Save.Gold:N0}"
                : reason;
            quoteText.color = canTrain ? PanelKit.Gold : PanelKit.Pink;
            if (startButton != null) startButton.interactable = canTrain;
            if (startLabel != null)
                startLabel.text = canTrain ? (lastOutcome == null ? "开始练习" : "再练一次")
                    : model.LevelOf(memberIndex) >= GameModel.MaxMemberLevel ? "已满级" : "星光币不足";
            if (plan.DiamondCost != 0)
                quoteText.text += " · 异常：出现星钻消耗";
        }

        private void RefreshReduceMotionLabel()
        {
            if (reduceMotionLabel != null)
                reduceMotionLabel.text = reduceMotion
                    ? $"减弱动画：开 · {MemberTrainingPractice.ReducedDurationSeconds:0.##} 秒"
                    : $"减弱动画：关 · {MemberTrainingPractice.StandardDurationSeconds:0.##} 秒";
        }

        public void ToggleReduceMotion()
        {
            reduceMotion = !reduceMotion;
            PlayerPrefs.SetInt(ReduceMotionKey, reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
            RefreshReduceMotionLabel();
            RefreshQuote();
        }

        /// <summary>
        /// Commits exactly one level-up through the existing gold-only API and builds the
        /// before/after outcome. Public so EditMode tests can verify economy and copy without
        /// waiting for the coroutine.
        /// </summary>
        public bool CommitTraining()
        {
            if (animating) return false;
            if (!model.CanTrain(memberIndex, out int cost, out string reason))
            {
                Notify(reason);
                RefreshQuote();
                return false;
            }

            int levelBefore = model.LevelOf(memberIndex);
            int powerBefore = model.PowerOf(memberIndex);
            ReadStats(memberIndex, out int attackBefore, out int hpBefore, out int critBefore,
                out int speedBefore);

            if (!model.Train(memberIndex, out string message))
            {
                Notify(message);
                RefreshQuote();
                return false;
            }

            int levelAfter = model.LevelOf(memberIndex);
            int powerAfter = model.PowerOf(memberIndex);
            ReadStats(memberIndex, out int attackAfter, out int hpAfter, out int critAfter,
                out int speedAfter);
            lastOutcome = MemberTrainingPractice.BuildOutcome(levelBefore, levelAfter, powerBefore, powerAfter,
                attackBefore, attackAfter, hpBefore, hpAfter, critBefore, critAfter, speedBefore, speedAfter, cost);

            ShowOutcome(lastOutcome);
            Notify(message);
            RefreshQuote();
            return true;
        }

        public void StartPractice()
        {
            if (animating) return;
            if (!CommitTraining()) return;
            StartCoroutine(PlayPracticeAnimation());
        }

        private void ShowOutcome(MemberPracticeOutcome outcome)
        {
            if (resultGroup == null) return;
            resultGroup.SetActive(true);
            resultHeadline.text = outcome.Headline;
            for (int index = 0; index < deltaTexts.Length && index < outcome.Deltas.Count; index++)
            {
                MemberProfileLine line = outcome.Deltas[index];
                deltaTexts[index].text = $"{line.Label}  {line.Value}";
                deltaTexts[index].color = line.Value.Contains("(+") ? PanelKit.Cyan : PanelKit.White;
            }

            resultCost.text = outcome.CostLine;
        }

        private IEnumerator PlayPracticeAnimation()
        {
            animating = true;
            if (startButton != null) startButton.interactable = false;
            float duration = plan != null ? plan.DurationSeconds : MemberTrainingPractice.StandardDurationSeconds;
            Vector3 baseScale = portraitRect != null ? portraitRect.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                if (portraitRect != null)
                {
                    float pulse = 1f + Mathf.Sin(t * Mathf.PI * (reduceMotion ? 1f : 3f)) * (reduceMotion ? .02f : .07f);
                    portraitRect.localScale = baseScale * pulse;
                }

                if (portraitGlow != null)
                    portraitGlow.color = new Color32(96, 150, 255, (byte)Mathf.RoundToInt(50f + 90f * Mathf.Sin(t * Mathf.PI)));

                for (int index = 0; index < rings.Count; index++)
                {
                    Image ring = rings[index];
                    if (ring == null) continue;
                    float local = Mathf.Repeat(t * (reduceMotion ? 1f : 2f) - index * .22f, 1f);
                    float scale = Mathf.Lerp(.55f, 1.55f, local);
                    ring.rectTransform.localScale = Vector3.one * scale;
                    ring.color = new Color(0.47f, 0.86f, 1f, Mathf.Clamp01(1f - local) * (reduceMotion ? .35f : .8f));
                }

                if (actionText != null)
                {
                    float flash = .75f + .25f * Mathf.Sin(t * Mathf.PI * 6f);
                    actionText.color = new Color(0.31f * flash + .35f, .86f, 1f, 1f);
                }

                yield return null;
            }

            if (portraitRect != null) portraitRect.localScale = baseScale;
            if (portraitGlow != null) portraitGlow.color = new Color32(96, 150, 255, 70);
            for (int index = 0; index < rings.Count; index++)
                if (rings[index] != null) rings[index].color = new Color(0.47f, 0.86f, 1f, 0f);
            if (actionText != null) actionText.color = PanelKit.Cyan;
            if (resultGroup != null)
            {
                resultGroup.transform.localScale = new Vector3(.97f, .97f, 1f);
                float pop = 0f;
                while (pop < .16f)
                {
                    pop += Time.unscaledDeltaTime;
                    float scale = Mathf.Lerp(.97f, 1f, Mathf.Clamp01(pop / .16f));
                    resultGroup.transform.localScale = new Vector3(scale, scale, 1f);
                    yield return null;
                }

                resultGroup.transform.localScale = Vector3.one;
            }

            animating = false;
            RefreshQuote();
        }

        private void ReadStats(int member, out int attack, out int hp, out int crit, out int speed)
        {
            CombatStats stats = model.StatsOf(member);
            attack = stats.Attack;
            hp = stats.Hp;
            crit = stats.CritPermille / 10;
            speed = stats.Speed;
        }

        private void Close()
        {
            if (animating) return;
            Action finished = onFinished;
            onFinished = null;
            DestroyPanelObject(gameObject);
            finished?.Invoke();
        }

        private static void DestroyPanelObject(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message)) onMessage?.Invoke(message);
        }
    }
}

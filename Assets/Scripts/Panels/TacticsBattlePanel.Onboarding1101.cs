using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-01 (用户: "刚刚进游戏的新手介绍文字太多了，用最少的文字方案"): the four text cards before the first fight are
    /// gone. Instead the fight stops for one line + a pointing hand at the moment each thing first matters, and the
    /// player does it right there: the first ready reroll (pick dice → press 重投), the first full card (tap her) and
    /// the first ultimate's beat. Everything else lives in 「战斗规则与设置」.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        public const string DicePickLine1101 = "点骰子选中，最多2颗";
        public const string RerollLine1101 = "点重投！不选=全部重投";
        public const string UltLine1101 = "能量满了！点她放大招";
        public const string BeatLine1101 = "光圈合拢时点 → PERFECT";

        /// <summary>Capture tests run in batch mode, where the guide is normally off.</summary>
        public static bool ForceOnboarding1101;

        /// <summary>This save started on the short guide (saves that saw the old cards never get it).</summary>
        private const string OnboardingFlag1101 = "guide-battle-1101";
        private const string DiceGuideFlag1101 = "guide-battle-1101-dice";
        private const string UltGuideFlag1101 = "guide-battle-1101-ult";
        private const string BeatGuideFlag1101 = "guide-battle-1101-beat";

        private CoachMark1101 coach1101;
        private bool OnboardingCoachOpen1101 => coach1101 != null && coach1101.IsOpen;
        /// <summary>The line on screen right now, or null (tests and QA).</summary>
        public string OnboardingLine1101 => OnboardingCoachOpen1101 ? coach1101.Line : null;

        private bool OnboardingAllowed1101 => FirstBattleTutorialEnabled && Application.isPlaying &&
            (!Application.isBatchMode || ForceOnboarding1101) && model != null && psdRoot != null &&
            battle != null && battle.IsTurnBased;

        private bool OnboardingPending1101(string step) =>
            OnboardingAllowed1101 && model.HasStoryFlag(OnboardingFlag1101) && !model.HasStoryFlag(step);

        /// <summary>Battle start: nothing to read. Only marks a fresh save as being on the short guide.</summary>
        private void BeginOnboarding1101()
        {
            if (!OnboardingAllowed1101) return;
            if (!model.HasStoryFlag(BattleTutorialFlag))
            {
                model.FirstTime(OnboardingFlag1101);
                model.FirstTime(BattleTutorialFlag);
            }
            if (OnboardingPending1101(BeatGuideFlag1101)) StartCoroutine(BeatHint1101());
        }

        /// <summary>Between two turns: the first ready reroll / the first full card pauses the fight for one step.</summary>
        private IEnumerator OnboardingBeat1101()
        {
            if (!OnboardingAllowed1101 || closing || battle.Outcome != BattleOutcome.Ongoing) yield break;
            // The opening roll grants no rerolls. Guide the first pip the team actually charges.
            if (OnboardingPending1101(DiceGuideFlag1101) && diceTurn != null && diceTurn.CanReroll &&
                (!diceTurn.RhythmMode || diceTurn.EarnedRerolls > 0))
            {
                float until = Time.unscaledTime + .8f; // the bar is sliding in
                while (!closing && (awaitingDiceLanding || dicePresentations.Exists(item => item.Busy) ||
                    psdRerollSlide != null && psdRerollSlide.Reveal < .95f) && Time.unscaledTime < until) yield return null;
                yield return DiceGuide1101();
            }
            if (closing || battle.Outcome != BattleOutcome.Ongoing) yield break;
            if (OnboardingPending1101(UltGuideFlag1101) && tappedUltimateId < 0)
            {
                CellView ready = cells.FirstOrDefault(c => c.Side == BattleSide.Player && c.Unit != null && c.Unit.Alive &&
                    c.Rect != null && battle.IsUltimateReady(c.Unit));
                if (ready != null) yield return UltGuide1101(ready);
            }
        }

        private IEnumerator DiceGuide1101()
        {
            var dice = new List<RectTransform>();
            foreach (GameObject die in diceButtons) if (die != null && die.activeInHierarchy) dice.Add((RectTransform)die.transform);
            if (dice.Count > 0)
            {
                // The hand taps a die worth changing (the same pick 自动 would make).
                bool[] mask = DiceAutoPlanner1031.ChooseRerollMask(diceTurn.Values, DiceTurn.MaxDicePerReroll, false);
                int pick = System.Array.IndexOf(mask, true);
                RectTransform aim = dice[Mathf.Clamp(pick, 0, dice.Count - 1)];
                yield return Coach1101(DicePickLine1101, dice.ToArray(), aim, () => diceTurn.SelectedForRerollCount > 0);
            }
            if (closing) yield break;
            int revision = diceTurn.Revision;
            RectTransform bar = psdRerollArt != null ? psdRerollArt.rectTransform : (RectTransform)psdReroll.transform;
            yield return Coach1101(RerollLine1101, new[] { bar }, bar, () => diceTurn.Revision != revision || !diceTurn.CanReroll);
            model.FirstTime(DiceGuideFlag1101);
            while (!closing && (awaitingDiceLanding || dicePresentations.Exists(item => item.Busy))) yield return null;
        }

        private IEnumerator UltGuide1101(CellView cell)
        {
            BattleUnit unit = cell.Unit;
            yield return Coach1101(UltLine1101, new[] { cell.Rect }, cell.Rect,
                () => tappedUltimateId >= 0 || !battle.IsUltimateReady(unit));
            model.FirstTime(UltGuideFlag1101);
        }

        private IEnumerator Coach1101(string line, RectTransform[] targets, RectTransform aim, System.Func<bool> done)
        {
            bool over = false;
            coach1101 = CoachMark1101.Show((RectTransform)transform, kit.Font, line, targets, aim,
                () => closing || done(), _ => over = true);
            while (!closing && !over) yield return null;
            coach1101 = null;
        }

        /// <summary>The first ultimate show carries one line under its rings; it leaves with the show.</summary>
        private IEnumerator BeatHint1101()
        {
            while (!closing && OnboardingPending1101(BeatGuideFlag1101))
            {
                if (ultimateShow != null && ultimateShow.IsActive && ultimateShow.Root != null)
                {
                    RectTransform plate = CoachMark1101.Caption(ultimateShow.Root, kit.Font, BeatLine1101, 30);
                    plate.name = "BeatHint1101";
                    plate.anchoredPosition = new Vector2(0f, -400f);
                    while (!closing && ultimateShow != null && ultimateShow.IsActive) yield return null;
                    if (plate != null) Destroy(plate.gameObject);
                    model.FirstTime(BeatGuideFlag1101);
                    yield break;
                }
                yield return null;
            }
        }
    }
}

using System.Collections;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Turn-based battle (2026-10-20 request, after 天命之子): one unit acts at a time in speed
    /// order. On a member's turn a P5-style command bar offers 普攻 / 技能 / 大招; tapping an enemy
    /// picks the target. Enemies act on their own turns, telegraphing specials a turn ahead.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>Default battle mode. Realtime-only PlayMode fixtures may switch it off.</summary>
        public static bool UseTurnBased = true;
        private const float TurnBeatSeconds = .4f;

        private RectTransform turnCommandBar, turnBanner;
        private Text turnBannerText, psdCountdownCaption;
        private Button turnBasicButton, turnSkillButton, turnUltButton;
        private Text turnSkillNote, turnUltNote;
        private TurnCommand? turnChoice;
        private float turnBannerClock = -1;

        private IEnumerator TurnLoop()
        {
            awaitingInput = true;
            RefreshRealtimeCommands();
            RefreshDiceUi();
            while (!closing && EntryPresentationCovered) yield return null;
            if (closing) yield break;
            WarmUltimateClips();
            Sfx("dice", .8f);
            while (!closing && (EntryPresentationCovered || awaitingDiceLanding ||
                dicePresentations.Exists(item => item.Busy))) yield return null;
            yield return WaitBattleDelay(.6f);
            BeginOnboarding1101(); // 2026-11-01: no text cards up front; one-line coach steps when each thing first matters
            // Boss stages open with a 首领登场 card (2026-10-21).
            if (battle.Stage.HasBossPhases && !bossEntranceShown1102 && psdWave1019 != null)
            {
                BattleUnit boss = battle.FindUnit(bossUnitId);
                if (psdWave1019.PlayBoss(boss != null ? boss.Definition.Name : null)) Sfx("cut", .8f);
                while (!closing && psdWave1019.IsShowing) yield return null;
            }
            chargeClockStarted1102 = true;
            while (!closing && battle.Outcome == BattleOutcome.Ongoing)
            {
                while (!closing && (paused || awaitingDiceLanding)) yield return null;
                yield return OnboardingBeat1101();
                // A tapped full card casts before anyone else acts (天命之子, 赵金 2026-10-24).
                yield return CastTappedUltimate();
                if (closing || battle.Outcome != BattleOutcome.Ongoing) break;
                BattleUnit actor = battle.NextTurnActor();
                DrainTurnLog();
                // A new wave's card plays out before anyone acts (2026-10-21).
                while (!closing && psdWave1019 != null && psdWave1019.IsShowing) yield return null;
                if (actor == null || closing) break;
                ShowTurnActor(actor);
                if (actor.Side == BattleSide.Enemy)
                {
                    yield return WaitBattleDelay(.35f);
                    while (!closing && paused) yield return null;
                    if (battle.CanParry(actor)) yield return Parry1111(actor); // 2026-11-11 第三章弹反
                    battle.EnemyTurn();
                }
                else
                {
                    // 2026-10-24: no command menu any more. Attacks and skills are automatic in both modes;
                    // 自动 also plays the dice and the ultimates, 手动 leaves rerolls and ultimates to the player's taps.
                    if (autoMode && battle.AutoUseRelic()) { DrainTurnLog(); RefreshAllCells(); } // 2026-11-12 功能骰
                    if (autoMode) yield return AutoReroll1031();
                    yield return WaitBattleDelay(autoMode ? .25f : .4f);
                    yield return CastTappedUltimate();
                    if (closing) yield break;
                    if (battle.TurnActor == actor && battle.Outcome == BattleOutcome.Ongoing)
                    {
                        if (autoMode && battle.HoldsUltimateSpotlight(actor))
                        {
                            // Auto still shows the character's ultimate; tapping the beat still scores.
                            bool done = false;
                            bool cast = false;
                            bool began = BeginUltimateShow(actor, grade => { cast = battle.TryTurnUltimate(grade); done = true; });
                            if (began) while (!closing && !done && battle.Outcome == BattleOutcome.Ongoing) yield return null;
                            if (!cast && battle.TurnActor == actor) battle.AutoTurn(false);
                        }
                        else battle.AutoTurn(false);
                    }
                }
                DrainTurnLog();
                RefreshAllCells(); RefreshRealtimeCommands(); RefreshDiceUi();
                yield return WaitBattleDelay(TurnBeatSeconds);
                yield return WaitUltimateFinale();
                ClearTurnHighlight();
            }
            HideTurnCommands();
            if (closing) yield break;
            awaitingInput = false;
            RefreshDiceUi();
            if (bossPresentation != null) bossPresentation.PlayOutcome(battle.Outcome == BattleOutcome.Victory);
            yield return WaitBattleDelay(0.88f);
            ShowResult();
        }

        private int tappedUltimateId = -1;
        private ChoSiren.UI.ParryPrompt1111 parryPrompt1111;
        /// <summary>Tests: answer every parry prompt with this grade instead of waiting for a tap.</summary>
        public static UltimateTimingGrade? ParryAutoGrade1111;

        /// <summary>第三章: the charged special is coming — the ring prompt arms the parry with the player's timing.</summary>
        private IEnumerator Parry1111(BattleUnit actor)
        {
            if (ParryAutoGrade1111.HasValue) { battle.ArmParry(actor, ParryAutoGrade1111.Value); yield break; }
            if (parryPrompt1111 == null) parryPrompt1111 = ChoSiren.UI.ParryPrompt1111.Create(transform, kit.Font);
            bool done = false;
            UltimateTimingGrade grade = UltimateTimingGrade.Miss;
            Sfx("cut", .7f);
            parryPrompt1111.Play(() => paused || closing, g => { grade = g; done = true; });
            while (!closing && !done) yield return null;
            if (closing) { parryPrompt1111.Cancel(); yield break; }
            battle.ArmParry(actor, grade);
            if (grade != UltimateTimingGrade.Miss) Sfx("block", 1f);
        }

        /// <summary>The member whose full card was tapped casts now, between turns (any mode).</summary>
        private IEnumerator CastTappedUltimate()
        {
            int id = tappedUltimateId;
            if (id < 0 && autoMode) id = battle.NextAutoUltimate()?.Id ?? -1;
            tappedUltimateId = -1;
            if (id < 0 || closing || battle.Outcome != BattleOutcome.Ongoing) yield break;
            BattleUnit unit = battle.FindUnit(id);
            if (unit == null || !battle.IsUltimateReady(unit)) yield break;
            bool done = false, cast = false;
            bool began = BeginUltimateShow(unit, grade => { cast = battle.TryCastUltimateNow(unit, grade); done = true; });
            if (!began) cast = battle.TryCastUltimateNow(unit, UltimateTimingGrade.Miss);
            else while (!closing && !done && battle.Outcome == BattleOutcome.Ongoing) yield return null;
            if (!cast) yield break;
            DrainTurnLog();
            RefreshAllCells(); RefreshRealtimeCommands(); RefreshDiceUi();
            yield return WaitBattleDelay(TurnBeatSeconds);
            yield return WaitUltimateFinale();
        }

        /// <summary>The ultimate's 终曲 is a moment of its own: nobody acts until it has played out (capped at 3 s).</summary>
        private IEnumerator WaitUltimateFinale()
        {
            float limit = Time.unscaledTime + 3f;
            while (!closing && fx1023 != null && fx1023.UltimatePlaying && Time.unscaledTime < limit) yield return null;
        }

        /// <summary>Tests and batch runs skip the first-battle guide (it waits for taps).</summary>
        public static bool FirstBattleTutorialEnabled = true;
        /// <summary>Set once a save has met a battle guide (the 2026-10-24 four cards, or since 11-01 the coach steps).</summary>
        private const string BattleTutorialFlag = "guide-battle-v2";

        private void DrainTurnLog()
        {
            int previousActor = -1;
            while (logCursor < battle.Log.Count)
            {
                BattleEvent entry = battle.Log[logCursor++];
                if (entry.Kind == BattleEventKind.RoundStart) { ShowRoundBanner(entry.Amount); continue; }
                PresentEvent(entry, previousActor);
                previousActor = entry.ActorId;
            }
            battleElapsed = battle.ElapsedMilliseconds / 1000f;
        }

        // ------------------------------------------------------------------ view

        private void BuildTurnUi()
        {
            if (psdRoot == null || turnCommandBar != null) return;
            turnCommandBar = kit.NewRect("TurnCommands", psdRoot);
            PsdPlace(turnCommandBar, 330, 3270, 2290, 330);
            turnBasicButton = TurnButton("普攻", "icon-claw-white", new Color32(255, 255, 255, 255), 0, TurnCommand.Basic, out _);
            turnSkillButton = TurnButton("技能", "icon-sword-zig", new Color32(70, 230, 255, 255), 1, TurnCommand.Skill, out turnSkillNote);
            turnUltButton = TurnButton("大招", "icon-star-burst", new Color32(255, 70, 170, 255), 2, TurnCommand.Ultimate, out turnUltNote);
            turnCommandBar.gameObject.SetActive(false);

            turnBanner = kit.NewRect("TurnBanner", psdRoot);
            PsdPlace(turnBanner, 0, 2420, 2946, 230);
            var bar = kit.NewImage("Bar", turnBanner, null, new Color32(10, 6, 20, 235));
            PanelKit.Stretch(bar.rectTransform); bar.raycastTarget = false;
            var stripe = kit.NewImage("Stripe", turnBanner, null, new Color32(255, 46, 147, 255));
            stripe.raycastTarget = false;
            stripe.rectTransform.anchorMin = new Vector2(0, 0); stripe.rectTransform.anchorMax = new Vector2(1, 0);
            stripe.rectTransform.pivot = new Vector2(.5f, 0); stripe.rectTransform.sizeDelta = new Vector2(0, 7);
            turnBannerText = kit.NewPlacedText(turnBanner, "", 30, Color.white, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            PanelKit.Stretch(turnBannerText.rectTransform);
            turnBannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            turnBanner.localEulerAngles = new Vector3(0, 0, -4f);
            turnBanner.gameObject.SetActive(false);
            if (psdCountdownCaption != null) psdCountdownCaption.text = "回合";
        }

        // 2026-10-24 review #21: a visible 换队长 control — since 10-26 the PSD 「队长」 plate under the first slot (tap it, then a card).
        private bool pickingCaptain1024;

        private void ToggleCaptainPick1024()
        {
            if (paused || closing || battle == null || !battle.IsTurnBased) return;
            kit.PlayClick();
            pickingCaptain1024 = !pickingCaptain1024;
            RefreshCaptainSwapButton1024();
            if (pickingCaptain1024) Notify("点一位成员，让她当队长");
        }

        private void RefreshCaptainSwapButton1024() => RefreshAllCells();

        private Button TurnButton(string label, string icon, Color accent, int index, TurnCommand command, out Text note)
        {
            const float width = 700, gap = 95;
            var plateRect = kit.NewRect("Turn-" + label, turnCommandBar);
            PsdPlace(plateRect, index * (width + gap), 0, width, 330);
            plateRect.localEulerAngles = new Vector3(0, 0, -6f);
            var shadow = kit.NewImage("Shadow", plateRect, PunkUi.SlantFill, accent);
            PanelKit.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(6, -6); shadow.rectTransform.offsetMax = new Vector2(6, -6);
            shadow.raycastTarget = false;
            var plate = kit.NewImage("Plate", plateRect, PunkUi.SlantFill, new Color32(8, 4, 18, 255));
            PanelKit.Stretch(plate.rectTransform);
            plate.raycastTarget = true;
            var button = plateRect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (paused || closing || !button.interactable) return;
                kit.PlayClick();
                turnChoice = command;
            });
            var glyph = kit.NewImage("Icon", plateRect, BattlePsdArt928.Sprite(icon), Color.white);
            glyph.preserveAspect = true; glyph.raycastTarget = false;
            PsdPlace(glyph.rectTransform, 30, 45, 240, 240);
            var title = kit.NewPlacedText(plateRect, label, 30, accent, 0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            PsdPlace(title.rectTransform, 280, 30, 400, 190);
            title.raycastTarget = false;
            // NotoSansSC's line box is ~1.45em; never let Truncate drop the whole word.
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            note = kit.NewPlacedText(plateRect, "", 15, new Color32(220, 214, 240, 255), 0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
            PsdPlace(note.rectTransform, 285, 205, 400, 90);
            note.raycastTarget = false;
            note.verticalOverflow = VerticalWrapMode.Overflow;
            return button;
        }

        private void ShowTurnCommands(BattleUnit actor)
        {
            BuildTurnUi();
            if (turnCommandBar == null) return;
            RefreshTurnCommands(actor);
            turnCommandBar.gameObject.SetActive(true);
            turnCommandBar.SetAsLastSibling();
            if (pauseOverlay != null) pauseOverlay.transform.SetAsLastSibling();
        }

        private void RefreshTurnCommands(BattleUnit actor)
        {
            if (turnCommandBar == null || actor == null) return;
            bool skill = battle.CanUseTurnSkill(actor);
            int rounds = battle.TurnSkillRoundsLeft(actor);
            turnSkillButton.interactable = skill;
            turnSkillNote.text = skill ? MemberSkillNames1103.Name(actor.Definition.Id, battle.RaceOf(actor), false) : $"{rounds} 回合后可用";
            SetTurnButtonDim(turnSkillButton, !skill);
            bool ult = battle.IsUltimateReady(actor);
            turnUltButton.interactable = ult;
            turnUltNote.text = ult ? MemberSkillNames1103.Name(actor.Definition.Id, battle.RaceOf(actor), true) : $"能量 {battle.EnergyOf(actor)}%";
            SetTurnButtonDim(turnUltButton, !ult);
            if (ult && !BattlePresentationOptions.ReduceMotion)
                turnUltButton.transform.localScale = Vector3.one * (1f + .04f * Mathf.Sin(Time.unscaledTime * 7f));
            else turnUltButton.transform.localScale = Vector3.one;
        }

        private static void SetTurnButtonDim(Button button, bool dim)
        {
            var group = UnityNull.Real(button.GetComponent<CanvasGroup>()) ?? button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = dim ? .45f : 1f;
        }

        private void HideTurnCommands()
        {
            if (turnCommandBar != null) turnCommandBar.gameObject.SetActive(false);
        }

        private void ShowTurnActor(BattleUnit actor)
        {
            BuildTurnUi();
            ClearTurnHighlight();
            CellView cell = FindCell(actor);
            if (actor.Side == BattleSide.Player && cell != null)
            {
                // The acting member's card comes to the front; 2026-10-31 (赵金): the frame no longer scales 1.06x —
                // her bust acts out inside it when she casts (HeroLunge1031).
                if (psdRoot == null) cell.Rect.localScale = Vector3.one * 1.06f;
                cell.Root.transform.SetAsLastSibling();
                turnHighlighted = cell;
            }
            // 2026-10-20: like 天命之子 and most turn RPGs, no "XX 的回合" caption; the acting
            // member's card steps up instead.
        }

        private CellView turnHighlighted;
        private void ClearTurnHighlight()
        {
            if (turnHighlighted != null) turnHighlighted.Rect.localScale = Vector3.one;
            turnHighlighted = null;
        }

        private void ShowRoundBanner(int round)
        {
            Notify($"第 {round} 回合");
        }

        /// <summary>The slanted name banner slides in and out quickly so it never hides the stage.</summary>
        private void AdvanceTurnBanner()
        {
            if (turnBanner == null || turnBannerClock < 0) return;
            if (!paused) turnBannerClock += Time.unscaledDeltaTime;
            float t = turnBannerClock;
            float x = t < .18f ? Mathf.Lerp(-760, 0, 1 - Mathf.Pow(1 - t / .18f, 3)) : t > 1.0f ? Mathf.Lerp(0, 760, (t - 1f) / .18f) : 0;
            turnBanner.anchoredPosition = new Vector2(x, turnBanner.anchoredPosition.y);
            if (t > 1.18f) { turnBanner.gameObject.SetActive(false); turnBannerClock = -1; }
        }
    }
}

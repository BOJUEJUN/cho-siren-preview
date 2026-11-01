using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 赵金: 手动 never rerolls by itself; 自动 rerolls at most two dice of a weak hand, with a readable gap,
    /// and never more than three times a battle. Runs the real turn loop of the battle panel.
    /// </summary>
    public sealed class DiceManualAuto1031PlayModeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        bool had, oldHad, tutorial, turnBased;
        string before, old;

        [SetUp]
        public void Setup()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            before = PlayerPrefs.GetString(GameModel.SaveKey); old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            tutorial = TacticsBattlePanel.FirstBattleTutorialEnabled; turnBased = TacticsBattlePanel.UseTurnBased;
            TacticsBattlePanel.FirstBattleTutorialEnabled = false;
            TacticsBattlePanel.UseTurnBased = true;
            // A live battle trips the known Fx1023 particle-layer RectMask2D exception (not dice related, also in
            // the 0.9.15 baseline); these tests judge the dice only.
            LogAssert.ignoreFailingMessages = true;
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.DestroyImmediate(app.gameObject);
            host = new GameObject("Dice1031", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1536);
        }

        [TearDown]
        public void Cleanup()
        {
            LogAssert.ignoreFailingMessages = true; // same known Fx1023 exception while the live panel is destroyed
            Object.DestroyImmediate(host);
            TacticsBattlePanel.FirstBattleTutorialEnabled = tutorial;
            TacticsBattlePanel.UseTurnBased = turnBased;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        TacticsBattlePanel Open(bool auto, string stage = "stage-1-5")
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            var model = new GameModel();
            // Level 1 against a boss: the fight lasts long enough to watch several rounds and rerolls.
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = 1;
            BattleSimulator battle = model.StartStageBattle(stage, 1031UL, out string message);
            Assert.That(battle, Is.Not.Null, message);
            TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
            Assert.That(battle.IsTurnBased, Is.True);
            typeof(TacticsBattlePanel).GetField("autoMode", Private).SetValue(panel, auto);
            typeof(TacticsBattlePanel).GetField("speed", Private).SetValue(panel, 3);
            return panel;
        }

        /// <summary>Gives the dice a weak hand (一对) so 自动 would want to reroll it.</summary>
        static void ForceWeakHand(DiceTurn dice)
        {
            var values = (int[])typeof(DiceTurn).GetField("values", Private).GetValue(dice);
            int[] weak = { 6, 1, 6, 2, 4, 5 }; // 一对 (no 3 → no straight)
            for (int i = 0; i < values.Length; i++) values[i] = weak[i];
            typeof(DiceTurn).GetMethod("RefreshHand", Private).Invoke(dice, null);
            Assert.That(DiceAutoPlanner1031.IsWeakHand(dice.Hand), Is.True);
        }

        static IEnumerator WaitLanding(TacticsBattlePanel panel)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   ((bool)typeof(TacticsBattlePanel).GetField("awaitingDiceLanding", Private).GetValue(panel) ||
                    panel.GetComponentsInChildren<DiceRollPresentation>(true).Any(r => r.Busy))) yield return null;
        }

        [UnityTest]
        public IEnumerator ManualModeKeepsTheSameDiceWhileRoundsPass()
        {
            TacticsBattlePanel panel = Open(false);
            BattleSimulator battle = panel.Battle;
            yield return WaitLanding(panel);
            ForceWeakHand(battle.BattleDice);
            battle.BattleDice.GainEnergy(300); // charged: 自动 would roll now
            yield return WaitLanding(panel);
            int[] values = battle.BattleDice.Values.ToArray();
            int revision = battle.BattleDice.Revision, round = battle.TurnRound;
            float until = Time.realtimeSinceStartup + 14f;
            while (Time.realtimeSinceStartup < until && battle.Outcome == BattleOutcome.Ongoing &&
                   battle.TurnRound < round + 3) yield return null;
            Assert.That(battle.TurnRound, Is.GreaterThan(round), "rounds must pass during the observation");
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision), "手动下不点重投，骰子不能自己重投");
            Assert.That(battle.BattleDice.Values.ToArray(), Is.EqualTo(values));
            Assert.That(battle.BattleDice.UsedRerolls, Is.Zero);
            Assert.That(panel.AutoRerollCount1031, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AutoModeRerollsAtMostTwoDiceWithAReadableGap()
        {
            TacticsBattlePanel panel = Open(true);
            BattleSimulator battle = panel.Battle;
            DiceTurn dice = battle.BattleDice;
            yield return WaitLanding(panel);
            ForceWeakHand(dice);
            dice.GainEnergy(300);
            var starts = new List<float>();
            int revision = dice.Revision, used = dice.RerollsUsedInBattle;
            float until = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < until && battle.Outcome == BattleOutcome.Ongoing && starts.Count < 3)
            {
                if (dice.Revision != revision)
                {
                    revision = dice.Revision;
                    Assert.That(dice.RerollsUsedInBattle, Is.EqualTo(used + 1), "every dice change is a counted reroll");
                    used = dice.RerollsUsedInBattle;
                    Assert.That(dice.Rolled.Count(r => r), Is.InRange(1, DiceTurn.MaxDicePerReroll));
                    starts.Add(Time.realtimeSinceStartup);
                    // Keep 自动 interested: a weak hand again.
                    yield return WaitLanding(panel);
                    if (!DiceAutoPlanner1031.IsWeakHand(dice.Hand)) { ForceWeakHand(dice); revision = dice.Revision; }
                }
                yield return null;
            }
            Assert.That(starts.Count, Is.GreaterThanOrEqualTo(2), "自动 should reroll a weak hand");
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.GreaterThanOrEqualTo(TacticsBattlePanel.AutoRerollGapSeconds1031),
                    "两次自动重投之间要留出看清的间隔");
            Assert.That(dice.RerollsUsedInBattle, Is.LessThanOrEqualTo(DiceTurn.MaxRerollsPerBattle));
        }

        [UnityTest]
        public IEnumerator SwitchingToManualDuringTheAutoPreviewRollsNothing()
        {
            TacticsBattlePanel panel = Open(true);
            BattleSimulator battle = panel.Battle;
            DiceTurn dice = battle.BattleDice;
            yield return WaitLanding(panel);
            ForceWeakHand(dice);
            dice.GainEnergy(300);
            yield return WaitLanding(panel);
            int revision = dice.Revision;
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && dice.SelectedForRerollCount == 0) yield return null;
            Assert.That(dice.SelectedForRerollCount, Is.InRange(1, 2), "自动 first lifts the dice it will roll");
            Assert.That(dice.Revision, Is.EqualTo(revision));
            typeof(TacticsBattlePanel).GetMethod("ToggleAuto", Private).Invoke(panel, null);
            Assert.That(panel.AutoMode, Is.False);
            float settle = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(dice.Revision, Is.EqualTo(revision), "切到手动后不能再自动掷出");
            Assert.That(dice.SelectedForRerollCount, Is.Zero, "自动的预选被撤回");
        }

        [UnityTest]
        public IEnumerator ManualTapOnAThirdDieIsRefused()
        {
            TacticsBattlePanel panel = Open(false);
            DiceTurn dice = panel.Battle.BattleDice;
            yield return WaitLanding(panel);
            yield return null;
            typeof(TacticsBattlePanel).GetField("awaitingInput", Private).SetValue(panel, true);
            var toggle = typeof(TacticsBattlePanel).GetMethod("ToggleDie", Private);
            toggle.Invoke(panel, new object[] { 0 });
            toggle.Invoke(panel, new object[] { 1 });
            toggle.Invoke(panel, new object[] { 2 });
            Assert.That(dice.SelectedForRerollCount, Is.EqualTo(2));
            Assert.That(dice.Held[0] || dice.Held[1], Is.False);
            Assert.That(dice.Held[2], Is.True);
            int revision = dice.Revision;
            dice.GainEnergy(100);
            typeof(TacticsBattlePanel).GetMethod("RerollDice", Private).Invoke(panel, null);
            Assert.That(dice.Revision, Is.EqualTo(revision + 1));
            Assert.That(dice.Rolled.Count(r => r), Is.EqualTo(2));
        }
    }
}

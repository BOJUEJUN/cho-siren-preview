using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>Auto-plays stages in turn mode at the recommended level and reports the pace.</summary>
    public sealed class TurnBattlePacing1020Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        [Test]
        public void AutoTurnBattleAtRecommendedLevelEndsInAboutAMinute()
        {
            var report = new List<string>();
            int wins = 0, played = 0, finishedInsideLimit = 0, shortest = int.MaxValue;
            foreach (string stageId in new[] { "stage-1-1", "stage-1-3", "stage-1-5", "stage-1-9", "stage-1-10", "stage-2-3" })
            {
                var model = new GameModel(() => Now);
                var stage = model.Tactics.Stages.FirstOrDefault(s => s.Id == stageId);
                if (stage == null) continue;
                for (int i = 0; i < model.Save.MemberLevels.Count; i++)
                    model.Save.MemberLevels[i] = Mathf.Max(model.Save.MemberLevels[i], stage.RecommendedLevel);
                foreach (var s in model.Tactics.Stages) model.Save.ClearedStages.Add(new StageClear { Id = s.Id, Stars = 3 });
                BattleSimulator battle = model.StartStageBattle(stageId, 44, out string error);
                Assert.That(battle, Is.Not.Null, stageId + " " + error);
                var races = new Dictionary<string, string>();
                foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
                battle.EnableRealtime(races, null, stage.RerollLimit, false);
                battle.EnableTurnBased();
                int actions = 0, playerActions = 0, ults = 0;
                while (battle.Outcome == BattleOutcome.Ongoing && actions < 2000)
                {
                    BattleUnit actor = battle.NextTurnActor();
                    if (actor == null) break;
                    if (actor.Side == BattleSide.Player) playerActions++;
                    int before = battle.LastTeamUltimateRound;
                    if (actor.Side == BattleSide.Player && battle.IsUltimateReady(actor)) ults++;
                    battle.AutoTurn();
                    actions++;
                }
                played++;
                if (battle.Outcome == BattleOutcome.Victory) wins++;
                if (battle.Outcome != BattleOutcome.Ongoing && battle.TurnRound <= battle.TurnRoundLimit + 1) finishedInsideLimit++;
                shortest = Math.Min(shortest, actions);
                // ~0.9 s per action on screen (beat + attack motion).
                report.Add($"{stageId} lv{stage.RecommendedLevel} {battle.Outcome} rounds={battle.TurnRound} actions={actions} ≈{actions * .9f:0}s rerollsEarned={battle.BattleDice.EarnedRerolls} ults={ults}");
            }
            Debug.Log("TURN-PACING\n" + string.Join("\n", report));
            TestContext.WriteLine(string.Join("\n", report));
            Assert.That(played, Is.GreaterThan(0));
            // 2026-11-01 (赵金数值批次): the recommended level is now a ≈60% fight with the gear of that point (Balance1101Tests);
            // without gear, one seed per stage is not a win guarantee any more. What this pacing check keeps: every fight
            // ends inside its round limit and none is over in a handful of actions.
            Assert.That(wins, Is.GreaterThanOrEqualTo(1), "推荐等级下自动战斗不应一场都赢不了");
            Assert.That(finishedInsideLimit, Is.EqualTo(played), "战斗必须在回合上限内结束");
            Assert.That(shortest, Is.GreaterThanOrEqualTo(15), "不能几次行动就结束");
        }

        [Test]
        public void TimedUltimateInAutoModeSpendsTheBar()
        {
            // 2026-10-20 bug: auto mode set AutoCastUltimates, which rejected GOOD/PERFECT casts —
            // the clip played every turn while the bar never emptied.
            var model = new GameModel(() => Now);
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = Mathf.Max(model.Save.MemberLevels[i], 10);
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 44, out string error);
            Assert.That(battle, Is.Not.Null, error);
            var races = new Dictionary<string, string>();
            foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
            battle.EnableRealtime(races, null, 3, false);
            battle.EnableTurnBased();
            battle.AutoCastUltimates = true; // what the panel's 自动 toggle sets
            int casts = 0;
            for (int n = 0; n < 400 && battle.Outcome == BattleOutcome.Ongoing; n++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Player && battle.IsUltimateReady(actor))
                {
                    Assert.That(battle.TryTurnUltimate(UltimateTimingGrade.Good), Is.True, "自动 GOOD 必须真正释放大招。");
                    Assert.That(battle.EnergyOf(actor), Is.EqualTo(0), "放完大招能量必须清空。");
                    casts++;
                    continue;
                }
                battle.AutoTurn();
            }
            Assert.That(casts, Is.LessThanOrEqualTo(4));
        }
    }
}

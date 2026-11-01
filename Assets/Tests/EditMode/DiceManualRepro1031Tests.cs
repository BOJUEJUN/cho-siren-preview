using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 赵金: "手动的时候，只要你不点重投，骰子就不会重投". A 手动 turn battle (attacks automatic,
    /// no reroll taps) must keep the same six dice round after round.
    /// </summary>
    public sealed class DiceManualRepro1031Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        [Test]
        public void ManualTurnBattleNeverRerollsDiceWithoutAPlayerTap()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = 1;
            model.Save.StoryProgress = 100;
            foreach (var s in model.Tactics.Stages) model.Save.ClearedStages.Add(new StageClear { Id = s.Id, Stars = 3 });
            BattleSimulator battle = model.StartStageBattle("stage-1-5", 1031, out string error);
            Assert.That(battle, Is.Not.Null, error);
            battle.EnableTurnBased();
            int[] opening = battle.BattleDice.Values.ToArray();
            int revision = battle.BattleDice.Revision;
            var changes = new List<string>();
            for (int guard = 0; guard < 400 && battle.Outcome == BattleOutcome.Ongoing && battle.TurnRound < 6; guard++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                battle.AutoTurn(false); // what the panel does in 手动: attacks only, no dice, no ultimates
                if (battle.BattleDice.Revision != revision)
                {
                    changes.Add($"round {battle.TurnRound}: {string.Join(",", battle.BattleDice.Values)}");
                    revision = battle.BattleDice.Revision;
                }
            }
            Assert.That(battle.TurnRound, Is.GreaterThanOrEqualTo(3), "the fixture must cover several rounds");
            Assert.That(changes, Is.Empty, "dice changed without a reroll tap:\n" + string.Join("\n", changes));
            Assert.That(battle.BattleDice.Values.ToArray(), Is.EqualTo(opening));
            Assert.That(battle.BattleDice.UsedRerolls, Is.Zero);
        }
    }
}

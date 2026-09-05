using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    /// <summary>Verifies shipped chapter data against the actual realtime rules, not the old turn fixtures.</summary>
    public sealed class DiceBattleIntegrationTests
    {
        [Test]
        public void StageOneOneRunsDicePoweredBattleToSettlementReadyVictory()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));

            StageDefinition stage = repository.Tactics.FindStage("stage-1-1");
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Chapter, Is.EqualTo("第 01 章"));
            Assert.That(repository.Tactics.Stages.Exists(item =>
                item.Id.StartsWith("stage-7-", StringComparison.Ordinal)), Is.False,
                "旧第七章编号只能由存档迁移层识别，不能重新进入 tactics 数据");

            var party = new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 40 },
                new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 40 },
                new PlayerUnitSetup { UnitId = "wubai", Row = 1, Col = 1, Level = 40 },
                new PlayerUnitSetup { UnitId = "yeying", Row = 2, Col = 0, Level = 40 },
            };
            foreach (PlayerUnitSetup member in party) member.Level = 1;
            var battle = new BattleSimulator(repository.Tactics, stage, party,
                new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4 }));
            battle.EnableRealtime(GameModel.Members.ToDictionary(m => m.Id, m => m.Race), "xingli", stage.RerollLimit);
            DiceTurn dice = battle.BattleDice;
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.Straight));
            Assert.That(dice.Hand.MultiplierPermille, Is.EqualTo(1450));

            Assert.That(battle.CharacterDamageDealt, Is.Zero, "掷骰本身不造成独立伤害");
            battle.AdvanceRealtime(1000);
            Assert.That(battle.CharacterDamageDealt, Is.GreaterThan(0));
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing),
                "一级新手战不能在首次普攻后立即结束");
            Assert.That(dice.BattleRerollLimit, Is.EqualTo(2));

            BattleOutcome outcome = battle.AutoPlay();
            Assert.That(outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.Log[battle.Log.Count - 1].Kind, Is.EqualTo(BattleEventKind.Finished));
            Assert.That(battle.Log.Where(e => e.Kind == BattleEventKind.Damage && e.ActorId <= 4)
                .All(e => e.SkillId.StartsWith("rt-")), Is.True);
            Assert.That(battle.StarRating(), Is.InRange(1, 3), "胜利后已具备合法结算星级");
        }
    }
}

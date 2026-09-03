using System;
using System.Collections.Generic;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    /// <summary>Verifies that shipped chapter-one data accepts a real dice-powered action.</summary>
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
            var battle = new BattleSimulator(repository.Tactics, stage, party,
                new ScriptedRandom(new[] { 999 }));

            var dice = new DiceTurn(new ScriptedRandom(new[] { 0 }, new[] { 0, 1, 2, 3, 4 }));
            dice.Begin();
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.Straight));
            Assert.That(dice.Hand.MultiplierPermille, Is.EqualTo(3000));

            BattleUnit actor = battle.CurrentActor;
            BattleUnit target = battle.UnitAt(BattleSide.Enemy, 1, 1);
            SkillDefinition strike = battle.LookupSkill("strike");
            Assert.That(actor.Definition.Id, Is.EqualTo("feiyin"), "队伍中绯音速度最高，应先行动");
            Assert.That(target, Is.Not.Null);

            int hpBefore = target.Hp;
            int expectedDamage = battle.PreviewDamage(actor, strike, target, false,
                dice.Hand.MultiplierPermille);
            Assert.That(battle.TryAct(new BattleAction
            {
                ActorId = actor.Id,
                SkillId = strike.Id,
                Row = target.Row,
                Col = target.Col,
                PowerMultiplierPermille = dice.Hand.MultiplierPermille
            }, out string error), Is.True, error);

            Assert.That(hpBefore - target.Hp, Is.EqualTo(expectedDamage));
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing),
                "一次顺子攻击后应继续推进剩余单位的行动");
            Assert.That(battle.CurrentActor, Is.Not.Null);

            BattleOutcome outcome = battle.AutoPlay();
            Assert.That(outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.Log[battle.Log.Count - 1].Kind, Is.EqualTo(BattleEventKind.Finished));
            Assert.That(battle.StarRating(), Is.InRange(1, 3), "胜利后已具备合法结算星级");
        }
    }
}

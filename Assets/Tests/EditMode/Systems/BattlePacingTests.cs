using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class BattlePacingTests
    {
        private const int TypicalDiceMultiplierPermille = 2500;
        private const int MinExpectedPlayerActions = 6;
        private const int MaxExpectedPlayerActions = 11;

        [Test]
        public void AllTenChapterOneBattlesStayNearTheOneMinuteInteractionBudget()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            Assert.That(repository.Tactics.Stages.Count, Is.EqualTo(GameModel.ChapterOneStageCount));

            var actionCounts = new List<int>();
            for (int index = 0; index < repository.Tactics.Stages.Count; index++)
            {
                StageDefinition stage = repository.Tactics.Stages[index];
                int playerActions = RunTypicalBattle(repository.Tactics, stage);
                actionCounts.Add(playerActions);
            }

            var failures = new List<string>();
            for (int index = 0; index < actionCounts.Count; index++)
            {
                StageDefinition stage = repository.Tactics.Stages[index];
                int actionCount = actionCounts[index];
                TestContext.WriteLine($"{stage.Id}: {actionCount} 次玩家操作");
                if (actionCount < MinExpectedPlayerActions || actionCount > MaxExpectedPlayerActions)
                {
                    failures.Add($"{stage.Id} {stage.Name}: {actionCount} 次（目标 {MinExpectedPlayerActions}–{MaxExpectedPlayerActions} 次）");
                }
            }

            Assert.That(failures, Is.Empty,
                "下列关卡不在约一分钟的操作预算内:\n" + string.Join("\n", failures));
        }

        private static int RunTypicalBattle(TacticsManifest manifest, StageDefinition stage)
        {
            var battle = new BattleSimulator(manifest, stage, DefaultParty(),
                new ScriptedRandom(new[] { 999 }));
            int playerActions = 0;
            int guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && guard++ < 300)
            {
                BattleUnit actor = battle.CurrentActor;
                Assert.That(actor, Is.Not.Null);
                BattleAction action = EnemyAi.Choose(battle, actor);
                Assert.That(action, Is.Not.Null);
                if (actor.Side == BattleSide.Player)
                {
                    action.PowerMultiplierPermille = TypicalDiceMultiplierPermille;
                    playerActions++;
                }

                Assert.That(battle.TryAct(action, out string error), Is.True, error);
            }

            Assert.That(guard, Is.LessThan(300), $"{stage.Name} 战斗没有在确定性上限内结束");
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory),
                $"{stage.Name} 默认四人、典型 2500‰ 骰型应可获胜");
            Assert.That(battle.EnemyPhase, Is.EqualTo(3), $"{stage.Name} 应完整进入第三阶段");
            Assert.That(battle.Log.Where(item => item.Kind == BattleEventKind.PhaseChanged)
                .Select(item => item.Phase), Is.EqualTo(new[] { 2, 3 }));
            return playerActions;
        }

        private static List<PlayerUnitSetup> DefaultParty()
        {
            return new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 68 },
                new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 64 },
                new PlayerUnitSetup { UnitId = "wubai", Row = 2, Col = 0, Level = 59 },
                new PlayerUnitSetup { UnitId = "yeying", Row = 0, Col = 1, Level = 57 },
            };
        }
    }
}

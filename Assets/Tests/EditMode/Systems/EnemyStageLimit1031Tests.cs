using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-31 (赵金): at most two minions on stage; the rest queue and step in when one falls.</summary>
    public sealed class EnemyStageLimit1031Tests
    {
        static readonly Dictionary<string, string> Races = new Dictionary<string, string> { { "none", "" } };

        static BattleSimulator Create(string encounter, params (int wave, int bossPhase)[] enemies)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            manifest.Units.Add(new UnitDefinition { Id = "none", Name = "测试成员", MaxHp = 100000,
                Attack = 100, Defense = 0, Speed = 100, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "boss", Name = "测试Boss", MaxHp = 5000000,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "测试敌人", MaxHp = 1000000,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            var spawns = new List<EnemySpawn>();
            for (int i = 0; i < enemies.Length; i++)
                spawns.Add(new EnemySpawn { UnitId = encounter == "boss" && i == 0 ? "boss" : "enemy",
                    Row = i % 3, Col = i / 3, Wave = enemies[i].wave, BossPhase = enemies[i].bossPhase });
            var stage = new StageDefinition { Id = "limit-test", Name = "同屏测试", EncounterType = encounter,
                TimeLimitSeconds = 90, ThreeStarSeconds = 75, Enemies = spawns };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage, new[] { new PlayerUnitSetup { UnitId = "none", Level = 1 } },
                new SeededRandom(1031));
            battle.EnableRealtime(Races, "none", stage.RerollLimit);
            return battle;
        }

        static int Alive(BattleSimulator battle) => battle.Units.Count(u => u.Side == BattleSide.Enemy && u.Alive && !battle.IsStageBoss(u));

        [Test]
        public void ThreeEnemyWaveKeepsTwoOnStageAndTheThirdStepsInWhenOneFalls()
        {
            BattleSimulator battle = Create("elite", (0, 0), (1, 0), (1, 0), (1, 0));
            battle.FindUnit(2).Hp = 1;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.CurrentWave, Is.EqualTo(2));
            Assert.That(Alive(battle), Is.EqualTo(2), "一波三只时最多两只同时在场");
            Assert.That(battle.QueuedEnemyIds.Count, Is.EqualTo(1));
            BattleUnit waiting = battle.FindUnit(battle.QueuedEnemyIds[0]);
            Assert.That(waiting.Spawned, Is.False);
            Assert.That(battle.FocusEnemy(waiting.Id), Is.False, "排队中的敌人不能被选中");
            battle.AdvanceRealtime(3000);
            Assert.That(waiting.Hp, Is.EqualTo(waiting.MaxHp), "排队中的敌人不能受伤");
            Assert.That(battle.Log.Any(e => e.ActorId == waiting.Id && e.Kind == BattleEventKind.Damage), Is.False);

            BattleUnit onStage = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
            onStage.Hp = 1;
            Assert.That(battle.FocusEnemy(onStage.Id), Is.True);
            while (onStage.Alive && battle.ElapsedMilliseconds < 30000) battle.AdvanceRealtime(25);
            Assert.That(waiting.Spawned, Is.True, "倒下一只后，排队的敌人立刻补位");
            Assert.That(Alive(battle), Is.EqualTo(2));
            Assert.That(battle.QueuedEnemyIds, Is.Empty);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
        }

        [Test]
        public void VictoryWaitsForQueuedEnemies()
        {
            BattleSimulator battle = Create("normal", (0, 0), (0, 0), (0, 0));
            Assert.That(Alive(battle), Is.EqualTo(2));
            Assert.That(battle.QueuedEnemyIds.Count, Is.EqualTo(1));
            foreach (BattleUnit u in battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive).ToList()) u.Hp = 1;
            int guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && battle.QueuedEnemyIds.Count > 0 && guard++ < 4000)
                battle.AdvanceRealtime(25);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing), "还有排队的敌人时不能判胜");
            Assert.That(battle.Units.Count(u => u.Side == BattleSide.Enemy && u.Spawned), Is.EqualTo(3));
        }

        [Test]
        public void BossDoesNotCountAgainstTheMinionLimit()
        {
            BattleSimulator battle = Create("boss", (0, 0), (0, 0), (0, 0), (1, 2), (1, 2));
            BattleUnit boss = battle.FindUnit(2);
            Assert.That(battle.IsStageBoss(boss), Is.True);
            Assert.That(boss.Alive, Is.True);
            Assert.That(Alive(battle), Is.EqualTo(2), "Boss 不占小怪名额");
            boss.Hp = boss.MaxHp * 55 / 100;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.EnemyPhase, Is.GreaterThanOrEqualTo(2));
            Assert.That(Alive(battle), Is.LessThanOrEqualTo(BattleSimulator.MaxActiveMinions),
                "Boss 阶段增援也受同屏两只限制");
            Assert.That(battle.QueuedEnemyIds.Count + Alive(battle), Is.EqualTo(4));
        }
    }
}

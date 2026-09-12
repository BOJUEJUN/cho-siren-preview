using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>
    /// v0.3.5 candidate: per-character ultimate energy and the player-facing reroll economy.
    /// Every assertion reads the battle model; the presentation layer is covered separately.
    /// </summary>
    public sealed class BattleEnergyUltimateTests
    {
        [Test]
        public void EnergyStartsEmptyAndGrowsFromAliveTimeAndRealHits()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("charm", enemyHp: 100000000, encounter: "normal");
            battle.AutoCastUltimates = false;
            BattleUnit player = battle.FindUnit(1);
            Assert.That(battle.EnergyOf(player), Is.Zero, "开局不能预充能量");
            Assert.That(battle.EnergyOf(battle.FindUnit(2)), Is.Zero, "敌人没有大招能量");

            battle.AdvanceRealtime(1000);
            Assert.That(battle.EnergyOf(player), Is.GreaterThanOrEqualTo(BattleSimulator.UnitEnergyPerSecond),
                "存活一秒至少获得每秒能量");
            battle.AdvanceRealtime(24000);
            Assert.That(battle.EnergyOf(player), Is.EqualTo(BattleSimulator.MaxUnitEnergy),
                "时间与命中合计后封顶 100");
            Assert.That(battle.EnergyOf(player), Is.LessThanOrEqualTo(BattleSimulator.MaxUnitEnergy));
        }

        [Test]
        public void ZeroTimeAndFinishedBattleNeverAddEnergy()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("charm", enemyHp: 10, attack: 10000,
                encounter: "normal");
            battle.AutoCastUltimates = false;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            int energy = battle.EnergyOf(battle.FindUnit(1));

            battle.AdvanceRealtime(0);
            Assert.That(battle.EnergyOf(battle.FindUnit(1)), Is.EqualTo(energy), "暂停帧（0 毫秒）不涨能量");
            battle.AdvanceRealtime(5000);
            Assert.That(battle.EnergyOf(battle.FindUnit(1)), Is.EqualTo(energy), "战斗结束后不涨能量");
            Assert.That(battle.ElapsedMilliseconds, Is.LessThan(2000), "结束后的时间不能继续推进");
        }

        [Test]
        public void ManualModeWaitsForTheTapAndDeductsEnergyExactlyOnce()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("charm", enemyHp: 100000000, encounter: "normal");
            battle.AutoCastUltimates = false;
            BattleUnit player = battle.FindUnit(1);
            battle.AdvanceRealtime(30000);
            Assert.That(battle.IsUltimateReady(player), Is.True);
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.ActionStarted && e.SkillId == "rt-charm-big"),
                Is.False, "手动战斗绝不自动放大招");

            Assert.That(battle.TryCastUltimate(player.Id), Is.True);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted &&
                e.SkillId == "rt-charm-big"), Is.EqualTo(1));
            Assert.That(battle.EnergyOf(player), Is.LessThan(BattleSimulator.MaxUnitEnergy), "释放后能量被扣除");
            Assert.That(battle.TryCastUltimate(player.Id), Is.False, "能量不足时不能连放");
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted &&
                e.SkillId == "rt-charm-big"), Is.EqualTo(1), "连点不能重复扣除或重复释放");
        }

        [Test]
        public void AutoModeReleasesCaptainFirstThroughTheSameAuthoritativePath()
        {
            BattleSimulator battle = CreateTwoMemberBattle(out BattleUnit captain, out BattleUnit ally);
            battle.AutoCastUltimates = true;
            BattleEvent firstBig = null;
            for (int step = 0; step < 2400 && firstBig == null &&
                 battle.Outcome == BattleOutcome.Ongoing; step++)
            {
                battle.AdvanceRealtime(25);
                firstBig = battle.Log.FirstOrDefault(e => e.Kind == BattleEventKind.ActionStarted &&
                    e.SkillId != null && e.SkillId.EndsWith("-big"));
            }

            Assert.That(firstBig, Is.Not.Null, "自动战斗满能量必须释放大招");
            Assert.That(firstBig.ActorId, Is.EqualTo(captain.Id), "自动释放按队长优先");
            Assert.That(battle.EnergyOf(ally), Is.LessThanOrEqualTo(BattleSimulator.MaxUnitEnergy));
        }

        [Test]
        public void SwitchingAutoModePreservesEnergyAndCannotDoubleCast()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("charm", enemyHp: 100000000, encounter: "normal");
            battle.AutoCastUltimates = false;
            BattleUnit player = battle.FindUnit(1);
            battle.AdvanceRealtime(26000);
            int beforeSwitch = battle.EnergyOf(player);
            Assert.That(beforeSwitch, Is.EqualTo(BattleSimulator.MaxUnitEnergy));

            battle.AutoCastUltimates = true;
            battle.AdvanceRealtime(25);
            int casts = battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.SkillId == "rt-charm-big");
            Assert.That(casts, Is.EqualTo(1), "切到自动后只释放一次");
            int afterCast = battle.EnergyOf(player);
            Assert.That(afterCast, Is.LessThan(BattleSimulator.MaxUnitEnergy));

            battle.AutoCastUltimates = false;
            battle.AdvanceRealtime(25);
            Assert.That(battle.EnergyOf(player), Is.GreaterThanOrEqualTo(afterCast), "切回手动不丢已积累的能量");
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.SkillId == "rt-charm-big"),
                Is.EqualTo(1), "切换模式不能重复释放");
        }

        [Test]
        public void FirstChapterRerollBudgetIsThreeAndChargesGrantProgressively()
        {
            var normal = new StageDefinition { Id = "budget-normal", EncounterType = "normal" };
            Assert.That(normal.RerollLimit, Is.EqualTo(3), "首章普通战体验预算为 3 次重投");
            BattleSimulator battle = RealtimeBattleTests.Create("charm", encounter: "normal");
            DiceTurn dice = battle.BattleDice;
            Assert.That(dice.BattleRerollLimit, Is.EqualTo(3));
            Assert.That(dice.Energy, Is.Zero, "开局没有伤害充能");
            Assert.That(dice.RerollsRemaining, Is.Zero, "重投次数靠充能发放，开局为 0");
            Assert.That(dice.CanReroll, Is.False);

            dice.GainEnergy(DiceTurn.MaxEnergy);
            Assert.That(dice.EarnedRerolls, Is.EqualTo(1), "充满一条发一次重投");
            Assert.That(dice.RerollsRemaining, Is.EqualTo(1));
            Assert.That(dice.RerollAll(out string error), Is.True, error);
            Assert.That(dice.UsedRerolls, Is.EqualTo(1));
            Assert.That(dice.RerollsRemaining, Is.EqualTo(0));

            dice.GainEnergy(DiceTurn.MaxEnergy * 3);
            Assert.That(dice.EarnedRerolls, Is.EqualTo(3), "一条充能可连续补发欠的额度");
            Assert.That(dice.Energy, Is.Zero, "额度发满后充能不再积累");
            dice.GainEnergy(DiceTurn.MaxEnergy);
            Assert.That(dice.EarnedRerolls, Is.EqualTo(3), "本场最多发满 3 次");
        }

        [Test]
        public void RerollChargeCarriesOverAndResetsAfterEachGrant()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(7), 3, true);
            dice.Begin();
            dice.GainEnergy(50);
            Assert.That(dice.Energy, Is.EqualTo(50), "充能条随伤害逐点积累");
            Assert.That(dice.EarnedRerolls, Is.Zero);
            dice.GainEnergy(60);
            Assert.That(dice.EarnedRerolls, Is.EqualTo(1), "跨次积累充满一条发一次");
            Assert.That(dice.Energy, Is.EqualTo(10), "发放后余量继续累积，从零开始充下一条");
        }

        [Test]
        public void SelectiveRerollKeepsUntickedDiceAndSpendsOneQuota()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(3), 3, true);
            dice.Begin();
            Assert.That(dice.Held.All(held => held), Is.True, "开局全部保留，避免误触重投整手");
            dice.GainEnergy(DiceTurn.MaxEnergy); // 充能满发 1 次重投
            Assert.That(dice.RerollUnheld(out _), Is.False, "没有点选骰子时不能重投");
            Assert.That(dice.UsedRerolls, Is.Zero, "非法选择不消耗额度");

            dice.ToggleHold(0);
            dice.ToggleHold(1);
            int keptThird = dice.Values[2], keptFourth = dice.Values[3], keptFifth = dice.Values[4];
            Assert.That(dice.SelectedForRerollCount, Is.EqualTo(2));
            Assert.That(dice.RerollUnheld(out string error), Is.True, error);
            Assert.That(dice.Values[2], Is.EqualTo(keptThird), "没点选的骰子必须保留");
            Assert.That(dice.Values[3], Is.EqualTo(keptFourth));
            Assert.That(dice.Values[4], Is.EqualTo(keptFifth));
            Assert.That(dice.UsedRerolls, Is.EqualTo(1), "一次操作只扣一次额度");
            Assert.That(dice.SelectedForRerollCount, Is.Zero, "重投后的新骰面默认全部保留");
        }

        [Test]
        public void RepeatedClicksCannotSpendTheSameOperationTwice()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(3), 3, true);
            dice.Begin();
            dice.GainEnergy(DiceTurn.MaxEnergy); // 充能满发 1 次重投
            for (int index = 0; index < DiceRules.DiceCount; index++) dice.ToggleHold(index);
            Assert.That(dice.RerollUnheld(out _), Is.True);
            int used = dice.UsedRerolls;
            Assert.That(dice.RerollUnheld(out _), Is.False, "重投后默认全部保留，重复点击不能再次扣额度");
            Assert.That(dice.UsedRerolls, Is.EqualTo(used));
            dice.GainEnergy(DiceTurn.MaxEnergy); // 再充一条发第 2 次
            Assert.That(dice.RerollAll(out _), Is.True);
            Assert.That(dice.UsedRerolls, Is.EqualTo(used + 1), "全部重投是另一次操作，只扣一次");
        }

        [Test]
        public void HandoverPreservesUltimateEnergyWithoutRefilling()
        {
            BattleSimulator battle = CreateTwoMemberBattle(out BattleUnit captain, out BattleUnit ally);
            battle.AutoCastUltimates = false;
            battle.AdvanceRealtime(8000);
            int allyEnergy = battle.EnergyOf(ally);
            Assert.That(allyEnergy, Is.GreaterThan(0));

            Kill(battle, captain.Id);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(ally.Id));
            Assert.That(battle.EnergyOf(ally), Is.EqualTo(allyEnergy), "接任不刷新能量");
        }

        private static BattleSimulator CreateTwoMemberBattle(out BattleUnit captain, out BattleUnit ally)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            foreach (string id in new[] { "captain", "ally" })
                manifest.Units.Add(new UnitDefinition
                {
                    Id = id, Name = id, MaxHp = 100000, Attack = 100, Defense = 0,
                    Speed = 100, CritPermille = 0, SkillIds = new List<string> { "strike" }
                });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "木桩", MaxHp = 100000000, Attack = 1, Defense = 0,
                Speed = 10, CritPermille = 0, SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "energy-party", Name = "能量测试", TurnLimit = 20, EncounterType = "normal",
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } }
            };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage, new[]
            {
                new PlayerUnitSetup { UnitId = "captain" },
                new PlayerUnitSetup { UnitId = "ally", Row = 1, Col = 0 },
            }, new SeededRandom(847));
            battle.EnableRealtime(new Dictionary<string, string>
            {
                { "captain", "魅族" }, { "ally", "魅族" }
            }, "captain", stage.RerollLimit);
            captain = battle.FindUnit(1);
            ally = battle.FindUnit(2);
            return battle;
        }

        private static void Kill(BattleSimulator battle, int unitId)
        {
            MethodInfo damage = typeof(BattleSimulator).GetMethod("ApplyActualDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(damage, Is.Not.Null);
            damage.Invoke(battle, new object[] { battle.Units.Last(), battle.FindUnit(unitId),
                "test-lethal", int.MaxValue, false, 1000 });
        }
    }
}

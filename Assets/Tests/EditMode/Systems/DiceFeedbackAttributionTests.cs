using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Dice-to-damage attribution and shield absorption reporting. These fields feed the battle
    /// feedback layer, so they must be captured when the hit is produced, not read back later.
    /// </summary>
    public sealed class DiceFeedbackAttributionTests
    {
        [Test]
        public void PlayerDamageEventsCarryTheMultiplierUsedForThatHit()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("none", attack: 100);
            DiceTurn dice = battle.BattleDice;
            int opening = dice.DamageMultiplierPermille;
            Assert.That(opening, Is.GreaterThanOrEqualTo(1000));

            battle.AdvanceRealtime(3000);
            BattleEvent first = battle.Log.First(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1);
            Assert.That(first.DiceMultiplierPermille, Is.EqualTo(opening),
                "首次伤害必须带开局骰型的累计倍率，不能在表现层读取后来的骰型。");
            BattleEvent enemyHit = battle.Log.First(e => e.Kind == BattleEventKind.Damage && e.ActorId == 2);
            Assert.That(enemyHit.DiceMultiplierPermille, Is.EqualTo(1000), "敌方伤害没有骰子归因。");

            dice.GainEnergy(DiceTurn.MaxEnergy);
            Assert.That(dice.EnergyRerollAll(out string error), Is.True, error);
            int afterReroll = dice.DamageMultiplierPermille;
            Assert.That(afterReroll, Is.GreaterThanOrEqualTo(opening + 50), "重投至少 +5%。");

            int cursor = battle.Log.Count;
            battle.AdvanceRealtime(3000);
            BattleEvent next = battle.Log.Skip(cursor)
                .First(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1);
            Assert.That(next.DiceMultiplierPermille, Is.EqualTo(afterReroll),
                "重投后的伤害必须使用新累计倍率；旧伤害事件不能被改写。");
        }

        [Test]
        public void PoisonKeepsItsOwnFormulaAndDoesNotInheritTheDiceMultiplier()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("demon",
                rolls: new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4 }));
            battle.AdvanceRealtime(5000);

            BattleEvent basic = battle.Log.First(e => e.Kind == BattleEventKind.Damage &&
                e.SkillId == "rt-basic" && e.ActorId == 1);
            Assert.That(basic.DiceMultiplierPermille, Is.GreaterThan(1000),
                "顺子开局应给普攻带上骰子累计倍率。");
            BattleEvent poison = battle.Log.FirstOrDefault(e => e.Kind == BattleEventKind.Damage &&
                e.SkillId == "rt-poison");
            Assert.That(poison, Is.Not.Null, "魔族普攻应叠毒并在之后跳伤。");
            Assert.That(poison.DiceMultiplierPermille, Is.EqualTo(1000),
                "毒伤按层数计算，不能再乘一次骰子倍率。");
        }

        [Test]
        public void ShieldAbsorptionIsReportedSeparatelyFromHpLoss()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("none", attack: 100);
            BattleUnit enemy = battle.FindUnit(2);
            enemy.Hp = 1000000;
            enemy.Shield = 25;
            battle.AdvanceRealtime(1000);

            BattleEvent hit = battle.Log.First(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1);
            Assert.That(hit.AbsorbedAmount, Is.EqualTo(25), "护盾只吸收还能吸收的部分。");
            Assert.That(hit.Amount, Is.GreaterThan(25), "测试必须产生穿透护盾的真实伤害。");
            Assert.That(hit.HpLostAmount, Is.EqualTo(hit.Amount - 25));
            Assert.That(enemy.Shield, Is.Zero);
        }

        [Test]
        public void FullyAbsorbedHitReportsZeroHpLossAndKeepsHp()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("none", attack: 100);
            BattleUnit enemy = battle.FindUnit(2);
            enemy.Hp = 1000000;
            enemy.Shield = 100000;
            battle.AdvanceRealtime(1000);

            BattleEvent hit = battle.Log.First(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1);
            Assert.That(hit.AbsorbedAmount, Is.EqualTo(hit.Amount));
            Assert.That(hit.HpLostAmount, Is.Zero);
            Assert.That(enemy.Hp, Is.EqualTo(1000000), "全吸收不能扣血，反馈也不能显示掉血数字。");
        }

        [Test]
        public void LegacyTurnDamageAlsoCarriesActionMultiplierAndAbsorption()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "普攻", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000, Cooldown = 0, CanCrit = false
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "hero", Name = "我方", MaxHp = 1000, Attack = 200, Defense = 0, Speed = 100,
                CritPermille = 0, SkillIds = new List<string> { "strike" }
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = 3000, Attack = 1, Defense = 0, Speed = 1,
                CritPermille = 0, SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "legacy-attribution", Name = "旧回合归因", TurnLimit = 5,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } }
            };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage,
                new[] { new PlayerUnitSetup { UnitId = "hero", Level = 1 } }, new ScriptedRandom(new[] { 999 }));
            BattleUnit enemy = battle.FindUnit(2);
            enemy.Shield = 10;

            Assert.That(battle.TryAct(new BattleAction
            {
                ActorId = 1, SkillId = "strike", Row = 0, Col = 0, PowerMultiplierPermille = 1450
            }, out string message), Is.True, message);

            BattleEvent hit = battle.Log.First(e => e.Kind == BattleEventKind.Damage);
            Assert.That(hit.DiceMultiplierPermille, Is.EqualTo(1450),
                "旧回合接口同样要把当次骰型倍率写进事件。");
            Assert.That(hit.AbsorbedAmount, Is.EqualTo(10));
            Assert.That(hit.HpLostAmount, Is.EqualTo(hit.Amount - 10));
        }

        [Test]
        public void DiceBonusCapsAtTwoTimesAndReportsNoFurtherGain()
        {
            var picks = new int[40];
            for (int index = 0; index < picks.Length; index++) picks[index] = 4;
            DiceTurn dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 999 }, picks), 5, false);
            dice.Begin();
            Assert.That(dice.AccumulatedBonusPermille, Is.EqualTo(250), "五条开局 +25%。");

            for (int index = 0; index < 5; index++)
            {
                dice.GainEnergy(DiceTurn.MaxEnergy);
                Assert.That(dice.EnergyRerollAll(out string error), Is.True, error);
            }

            Assert.That(dice.AccumulatedBonusPermille, Is.EqualTo(DiceTurn.MaxBattleBonusPermille));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(2000));
            Assert.That(dice.LastBonusGainPermille, Is.Zero, "封顶后必须显示 0 增益，不能假装继续增长。");
            Assert.That(dice.RerollsRemaining, Is.Zero);
        }

        [Test]
        public void DiceMultiplierIsAppliedExactlyOnce()
        {
            BattleSimulator high = RealtimeBattleTests.Create("none", attack: 100,
                rolls: new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 }));
            BattleSimulator straight = RealtimeBattleTests.Create("none", attack: 100,
                rolls: new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4 }));
            int multiplier = straight.BattleDice.DamageMultiplierPermille;
            Assert.That(multiplier, Is.GreaterThan(1000), "顺子开局必须提供大于 1 的倍率。");

            high.AdvanceRealtime(1000);
            straight.AdvanceRealtime(1000);
            int neutral = high.Log.First(e => e.Kind == BattleEventKind.Damage &&
                e.ActorId == 1 && e.SkillId == "rt-basic").Amount;
            int boosted = straight.Log.First(e => e.Kind == BattleEventKind.Damage &&
                e.ActorId == 1 && e.SkillId == "rt-basic").Amount;

            Assert.That(boosted, Is.EqualTo((int)((long)neutral * multiplier / 1000)),
                "伤害必须只乘一次骰子倍率；乘两次会让顺子收益被平方。");
        }

        [Test]
        public void CaptainBenefitReadoutMatchesTheCombatRules()
        {
            var threeKind = new[] { 0, 0, 0, 1, 2 };
            BattleSimulator mermaid = RealtimeBattleTests.Create("mermaid",
                rolls: new ScriptedRandom(new[] { 999 }, threeKind));
            BattleSimulator demon = RealtimeBattleTests.Create("demon",
                rolls: new ScriptedRandom(new[] { 999 }, threeKind));
            BattleSimulator charm = RealtimeBattleTests.Create("charm",
                rolls: new ScriptedRandom(new[] { 999 }, threeKind));
            BattleSimulator elf = RealtimeBattleTests.Create("elf",
                rolls: new ScriptedRandom(new[] { 999 }, threeKind));

            Assert.That(mermaid.CaptainShieldPermille, Is.EqualTo(120), "三条人鱼护盾 12%。");
            Assert.That(demon.CaptainPoisonLayers, Is.EqualTo(3), "三条魔族叠毒 3 层。");
            Assert.That(charm.CaptainComboCount, Is.EqualTo(3), "三条魅族追击 3 次。");
            Assert.That(elf.CaptainPiercePermille, Is.EqualTo(200), "三条血精灵穿甲 20%。");
            BattleSimulator none = RealtimeBattleTests.Create("none");
            Assert.That(none.CaptainShieldPermille, Is.Zero);
            Assert.That(none.CaptainComboCount, Is.Zero);
        }

        [Test]
        public void TimedEffectsExposeRemainingTimeAndExpire()
        {
            BattleSimulator battle = RealtimeBattleTests.Create("none");
            BattleUnit ally = battle.FindUnit(1);
            Assert.That(battle.TryTacticalGuard(), Is.True);

            string guarded = battle.RealtimeStatus(ally);
            Assert.That(guarded, Does.Contain("守护50%"), "守护生效时必须持续可见。");
            Assert.That(guarded, Does.Contain("s"), "状态必须带剩余时间。");

            battle.AdvanceRealtime(3500);
            Assert.That(battle.RealtimeStatus(ally), Does.Not.Contain("守护50%"), "到期必须清除，不能残留。");
        }
    }
}

using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>Whole-encounter limits: normal 2, elite/boss/world 3 (2/3/4/5 before 2026-10-31); never more than 3.</summary>
    public sealed class BattleRerollContract1019Tests
    {
        [TestCase("normal", 2)]
        [TestCase("elite", 3)]
        [TestCase("boss", 3)] // 2026-10-31 赵金: 每局上限三次
        [TestCase("world", 3)]
        public void AllRerollInputsShareOneBudgetDespiteExcessDamageAndRepeatedBegin(string encounter, int limit)
        {
            var stage = new StageDefinition { EncounterType = encounter };
            var dice = DiceTurn.ForBattle(new SeededRandom(19), stage.RerollLimit, true);
            dice.Begin();
            Assert.That(dice.RerollsRemaining, Is.Zero, "开场掷骰不额外发放重投次数");
            int successful = 0;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // Even large real damage, charging and repeated initialization cannot refill a spent budget.
                dice.RecordDamage(int.MaxValue, true, 2000);
                dice.GainEnergy(int.MaxValue);
                dice.Begin();
                dice.SetBattleSelectiveReroll(true);
                int revision = dice.Revision;
                bool rerolled;
                if (attempt % 3 == 0)
                {
                    if (dice.Held[5]) dice.ToggleHold(5);
                    rerolled = dice.RerollUnheld(out _);
                }
                else if (attempt % 3 == 1) rerolled = dice.RerollAll(out _);
                else rerolled = dice.EnergyRerollAll(out _);
                if (rerolled) successful++;
                Assert.That(dice.Revision, Is.EqualTo(revision + (rerolled ? 1 : 0)));
            }
            Assert.That(successful, Is.EqualTo(limit), "自选、全部和旧入口共用同一整场额度");
            Assert.That(dice.UsedRerolls, Is.EqualTo(limit));
            Assert.That(dice.EarnedRerolls, Is.EqualTo(limit));
            Assert.That(dice.RerollsRemaining, Is.Zero);
            Assert.That(dice.FreeRerolls, Is.Zero, "伤害、击杀和充能都不能凭空发免费补救");
            Assert.That(dice.Energy, Is.Zero);
            Assert.That(dice.CanReroll, Is.False);
        }

        [TestCase("normal", 2)]
        [TestCase("elite", 3)]
        [TestCase("boss", 3)] // 2026-10-31 赵金: 每局上限三次
        [TestCase("world", 3)]
        public void BloodElfRescueIsOneSeparateAllowanceAndCannotLoop(string encounter, int limit)
        {
            var stage = new StageDefinition { EncounterType = encounter };
            var dice = DiceTurn.ForBattle(new SeededRandom(20), stage.RerollLimit, true);
            dice.Begin();
            dice.GainEnergy(int.MaxValue);
            for (int i = 0; i < limit; i++) Assert.That(dice.RerollAll(out _), Is.True);
            Assert.That(dice.CanReroll, Is.False);
            dice.GrantFreeReroll();
            Assert.That(dice.FreeRerolls, Is.EqualTo(1));
            // The rescue still never passes the three-a-battle cap.
            bool rescueFits = limit < DiceTurn.MaxRerollsPerBattle;
            Assert.That(dice.RerollAll(out _), Is.EqualTo(rescueFits));
            int[] faces = dice.Values.ToArray();
            int revision = dice.Revision;
            for (int i = 0; i < 20; i++)
            {
                dice.GrantFreeReroll();
                dice.GainEnergy(int.MaxValue);
                dice.Begin();
                Assert.That(dice.RerollAll(out _), Is.False, "同一场补救不能因消费、换人或新骰型再次发放");
            }
            Assert.That(dice.UsedRerolls, Is.EqualTo(limit), "免费补救不能多扣普通额度");
            Assert.That(dice.FreeRerolls, Is.EqualTo(rescueFits ? 0 : 1));
            Assert.That(dice.RerollsUsedInBattle, Is.LessThanOrEqualTo(DiceTurn.MaxRerollsPerBattle));
            Assert.That(dice.Values, Is.EqualTo(faces));
            Assert.That(dice.Revision, Is.EqualTo(revision));
        }
    }
}

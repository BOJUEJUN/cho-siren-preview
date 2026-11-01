using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class BattlePsdSkillColumnEdge1019Tests
    {
        [TestCase(CombatRace.Charm)]
        [TestCase(CombatRace.BloodElf)]
        [TestCase(CombatRace.Demon)]
        [TestCase(CombatRace.Mermaid)]
        public void AllThreeSkillKindsFormOneUnclippedHorizontalRowAboveTheirOwnCard(CombatRace race)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                float previousRight = 0;
                for (int row = 0; row < 3; row++)
                {
                    Rect native = BattlePsdSkillLayout1019.For(race, row).Native;
                    Rect placed = BattlePsdSkillLayout1019.LocalBounds(race, row, slot);
                    Assert.That(placed.xMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(placed.xMax, Is.LessThanOrEqualTo(708), "No symbol borrows the next actor's head space");
                    Assert.That(placed.yMax, Is.LessThan(0), "Skills stay above the disc and actual face");
                    Assert.That(placed.center.y, Is.EqualTo(-120).Within(.001f));
                    Assert.That(placed.xMin, Is.GreaterThanOrEqualTo(previousRight));
                    Assert.That(placed.width / placed.height, Is.EqualTo(native.width / native.height).Within(.0001f));
                    Assert.That(Mathf.Max(placed.width, placed.height), Is.EqualTo(200).Within(.001f));
                    previousRight = placed.xMax;
                }
            }
        }

        [Test]
        public void FormationChangesKeepAllSymbolsInTheirOwnMembersLocalHeadRow()
        {
            foreach (CombatRace race in new[] { CombatRace.Charm, CombatRace.BloodElf, CombatRace.Demon, CombatRace.Mermaid })
                for (int row = 0; row < 3; row++)
                    for (int slot = 1; slot < 4; slot++)
                        Assert.That(BattlePsdSkillLayout1019.LocalBounds(race, row, slot),
                            Is.EqualTo(BattlePsdSkillLayout1019.LocalBounds(race, row, 0)));
        }

        [Test]
        public void HorizontalLayoutKeepsBloodElfActualSmallSkillAndWholeCompoundPassive()
        {
            Assert.That(BattlePsdSkillLayout1019.For(CombatRace.BloodElf, 1).Key, Is.EqualTo("icon-gold-shield"));
            Assert.That(BattlePsdSkillLayout1019.For(CombatRace.BloodElf, 2).Key, Is.EqualTo("symbol-elf-passive"));
            Assert.That(BattlePsdSkillLayout1019.For(CombatRace.BloodElf, 2).Native.size,
                Is.EqualTo(new Vector2(416, 218)), "Layout changes do not recut source art");
        }
    }
}

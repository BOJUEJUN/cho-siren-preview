using ChoSiren.Systems.Dice;
using ChoSiren.UI;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class DiceComboShowTests
    {
        [Test] public void BiggerCombosGetBiggerShowsAndHighCardGetsNone()
        {
            Assert.AreEqual(0, DiceComboShow.TierOf((int)DicePattern.HighPoint));
            Assert.AreEqual(1, DiceComboShow.TierOf((int)DicePattern.Pair));
            Assert.AreEqual(2, DiceComboShow.TierOf((int)DicePattern.Straight));
            Assert.AreEqual(3, DiceComboShow.TierOf((int)DicePattern.FourKind));
            Assert.AreEqual(4, DiceComboShow.TierOf((int)DicePattern.SixKind));
            Assert.AreEqual(4, DiceComboShow.TierOf((int)DicePattern.SixStraight));
        }
    }
}

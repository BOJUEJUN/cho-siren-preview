using ChoSiren.Systems.Dice;
using ChoSiren.UI;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class DiceFxStyleTests
    {
        [Test]
        public void EachHandBurnsInItsOwnStyle()
        {
            Assert.That(DiceFxStyle.For(DicePattern.Pair, new[] { 3, 3, 1, 5, 6, 2 }, 0).Color, Is.EqualTo(DiceFxStyle.Pink));
            Assert.That(DiceFxStyle.For(DicePattern.ThreeKind, new[] { 2, 2, 2, 5, 6, 1 }, 0).Color, Is.EqualTo(DiceFxStyle.Magenta));

            DiceFxStyle straight = DiceFxStyle.For(DicePattern.Straight, new[] { 1, 2, 3, 4, 5, 5 }, 2);
            Assert.That(straight.Color, Is.EqualTo(DiceFxStyle.Cyan));
            Assert.That(straight.Electric, Is.True);

            DiceFxStyle four = DiceFxStyle.For(DicePattern.FourKind, new[] { 6, 6, 6, 6, 1, 2 }, 0);
            Assert.That(four.Sparkle, Is.True);
            Assert.That(four.Rainbow, Is.False);

            DiceFxStyle five = DiceFxStyle.For(DicePattern.FiveKind, new[] { 4, 4, 4, 4, 4, 1 }, 0);
            Assert.That(five.Rainbow && five.Sparkle, Is.True);
            Assert.That(five.Scale, Is.GreaterThan(four.Scale));
        }

        [Test]
        public void GroupedHandsColourEachGroupSeparately()
        {
            int[] twoPair = { 5, 5, 2, 2, 6, 1 };
            Assert.That(DiceFxStyle.For(DicePattern.TwoPair, twoPair, 0).Color, Is.EqualTo(DiceFxStyle.Pink));
            Assert.That(DiceFxStyle.For(DicePattern.TwoPair, twoPair, 2).Color, Is.EqualTo(DiceFxStyle.Violet));

            int[] fullHouse = { 3, 3, 3, 6, 6, 1 };
            Assert.That(DiceFxStyle.For(DicePattern.FullHouse, fullHouse, 0).Color, Is.EqualTo(DiceFxStyle.Gold));
            Assert.That(DiceFxStyle.For(DicePattern.FullHouse, fullHouse, 3).Color, Is.EqualTo(DiceFxStyle.Pink));
        }
    }
}

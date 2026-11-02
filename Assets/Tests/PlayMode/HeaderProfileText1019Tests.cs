using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class HeaderProfileText1019Tests
    {
        [Test]
        public void ActualChineseFontFitsAllThreeDistinctHeaderRowsWithoutVerticalOverflow()
        {
            var kind = typeof(ChoSirenApp).GetNestedType("HeaderPolish038Layout", BindingFlags.NonPublic);
            Assert.That(kind, Is.Not.Null);
            object layout = kind.GetMethod("For", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { "Lobby" });
            Rect name = (Rect)kind.GetField("Name").GetValue(layout);
            Rect level = (Rect)kind.GetField("Level").GetValue(layout);
            Rect power = (Rect)kind.GetField("Power").GetValue(layout);
            Assert.That(name.yMax, Is.LessThanOrEqualTo(level.yMin));
            Assert.That(level.yMax, Is.LessThanOrEqualTo(power.yMin));
            Rect profile = (Rect)kind.GetField("Profile").GetValue(layout);
            Rect avatar = (Rect)kind.GetField("Avatar").GetValue(layout);
            Assert.That(name.xMax, Is.LessThan(profile.width - 12f), "Keep the enlarged name inside its plaque.");
            Assert.That(avatar.width, Is.GreaterThanOrEqualTo(48f), "The profile picture must remain recognisable on a phone.");
            Check("经纪人", 22, name); Check("LV.100", 16, level); Check("战力 1,234,567", 18, power);
        }
        static void Check(string copy, int pointSize, Rect bounds)
        {
            var go = new GameObject("Measured header text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            try
            {
                var text = go.GetComponent<UnityEngine.UI.Text>();
                text.font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = pointSize; text.fontStyle = FontStyle.Bold; text.text = copy;
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
                ((RectTransform)go.transform).sizeDelta = bounds.size;
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(bounds.height + .1f), copy + " must fit the production font's true line height.");
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(bounds.width + .1f), copy + " must not reach under the frame decoration.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

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
            Assert.That(name.xMax, Is.LessThan(153), "Keep the name clear of the plaque's right-hand triangle decoration.");
            Check("经纪人", 17, name); Check("LV.100", 12, level); Check("战力 1,234,567", 15, power);
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

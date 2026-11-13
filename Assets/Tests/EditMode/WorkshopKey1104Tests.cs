using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-05 创意工坊 AI 换装: the model's green backdrop is keyed out in game and framed like her still.</summary>
    public sealed class WorkshopKey1104Tests
    {
        private static readonly Color32 Backdrop = new Color32(92, 224, 150, 255);

        /// <summary>A 60x100 card: soft-green backdrop, a white figure (20..39 x 10..89), a green bow inside her outline,
        /// a darker green floor shadow under her feet.</summary>
        private static Texture2D Card()
        {
            var t = new Texture2D(60, 100, TextureFormat.RGBA32, false);
            var px = new Color32[60 * 100];
            for (int y = 0; y < 100; y++)
                for (int x = 0; x < 60; x++)
                {
                    Color32 c = Backdrop;
                    if (y >= 6 && y < 10 && x >= 14 && x < 46) c = new Color32(60, 170, 110, 255); // shadow
                    if (x >= 20 && x < 40 && y >= 10 && y < 90) c = new Color32(240, 240, 245, 255);
                    if (x >= 26 && x < 34 && y >= 40 && y < 48) c = new Color32(40, 160, 60, 255); // green bow
                    px[y * 60 + x] = c;
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        [Test]
        public void BackdropAndShadowGoFigureAndGreenBowStay()
        {
            Texture2D keyed = WorkshopAi1104.KeyGreen(Card());
            Color32 At(int x, int y) => keyed.GetPixel(x, y);
            Assert.That(At(2, 2).a, Is.Zero, "backdrop corner");
            Assert.That(At(55, 50).a, Is.Zero, "backdrop side");
            Assert.That(At(16, 8).a, Is.Zero, "floor shadow");
            Assert.That(At(30, 60).a, Is.EqualTo(255), "figure");
            Assert.That(At(30, 44).a, Is.EqualTo(255), "green bow inside her outline");
            Color32 rim = At(20, 50);
            Assert.That(rim.g, Is.LessThanOrEqualTo(Mathf.Max(rim.r, rim.b)), "edge despilled");
        }

        [Test]
        public void ResultIsFramedLikeTheStill()
        {
            Texture2D keyed = WorkshopAi1104.KeyGreen(Card());
            // Her still: 120x200 image, figure 40 wide 160 tall standing at (40,20).
            Texture2D fitted = WorkshopAi1104.FitToStill(keyed, new RectInt(40, 20, 40, 160), new Vector2Int(120, 200));
            RectInt fig = WorkshopAi1104.OpaqueBounds(fitted.GetPixels32(), fitted.width, fitted.height);
            float k = 80f / 160f;
            Assert.That(fitted.width, Is.EqualTo(60));
            Assert.That(fitted.height, Is.EqualTo(100));
            Assert.That(fig.height, Is.EqualTo(80));
            Assert.That(fig.y, Is.EqualTo(Mathf.RoundToInt(20 * k)));
            Assert.That(fig.x + fig.width * .5f, Is.EqualTo(60 * k).Within(1f));
        }

        [Test]
        public void AnOutfitCostsTenDiamondsOnlyWhenAffordable()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            var model = new GameModel(() => new System.DateTime(2026, 11, 5, 12, 0, 0, System.DateTimeKind.Utc), new InMemorySaveStore());
            model.Save.Gold = 1200; model.Save.Diamonds = 300; // Explicitly fund this paid-action fixture.
            int start = model.Balance(ChoSiren.Systems.Economy.CurrencyIds.Diamond);
            Assert.That(model.CanPayAiDress1105, Is.True);
            Assert.That(model.PayAiDress1105(), Is.True);
            Assert.That(model.Balance(ChoSiren.Systems.Economy.CurrencyIds.Diamond), Is.EqualTo(start - GameModel.AiDressDiamondCost1105));
            model.Save.Diamonds = GameModel.AiDressDiamondCost1105 - 1;
            Assert.That(model.CanPayAiDress1105, Is.False);
            Assert.That(model.PayAiDress1105(), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(GameModel.AiDressDiamondCost1105 - 1));
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
        }

        /// <summary>Real model output (when the QA sample exists): writes the keyed PNG for inspection.</summary>
        [Test]
        public void KeysARealSampleWhenPresent()
        {
            string path = System.Environment.GetEnvironmentVariable("WORKSHOP_KEY_SAMPLE");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) Assert.Ignore("no sample");
            var raw = new Texture2D(2, 2);
            raw.LoadImage(File.ReadAllBytes(path));
            float t = Time.realtimeSinceStartup;
            Texture2D keyed = WorkshopAi1104.KeyGreen(raw);
            Debug.Log($"[KEY1105] {raw.width}x{raw.height} keyed in {(Time.realtimeSinceStartup - t) * 1000f:0} ms");
            File.WriteAllBytes(Path.ChangeExtension(path, ".keyed.png"), keyed.EncodeToPNG());
            Assert.That(keyed.GetPixel(4, 4).a, Is.Zero);
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01c 无限骰子: real-frame captures of the new reroll HUD (骰子豆 pips, the next-pip ring, the 骰子加成 meter)
    /// at the phone (1080×2288) and Mac-window (1080×2160) ratios, written to Builds/qa-logs/zj-1101c/dice-endless/.
    /// Also checks the HUD follows the dice: pips = stored rerolls, ring = next-pip progress, no "本场剩N次" words.
    /// </summary>
    public sealed class DiceEndless1101CaptureTests
    {
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1101c", "dice-endless"));

        [UnityTest]
        public IEnumerator CapturePhoneRatio1080x2288() => Capture(1080, 2288, "phone");

        [UnityTest]
        public IEnumerator CaptureMacWindowRatio1080x2160() => Capture(1080, 2160, "mac");

        static IEnumerator Capture(int width, int height, string tag)
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("DiceEndlessCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("DiceEndlessCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24);
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-2", 1101UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines(); // Keep battle actions from changing the explicit HUD states below.
                DiceTurn dice = battle.BattleDice;
                Assert.That(dice.RhythmMode, Is.True, "回合制 = 骰子豆");
                yield return Wait(1.0f);

                // 1) Opening dice are dealt once, no spendable rerolls, the control stays retracted.
                Refresh(panel);
                yield return Wait(.6f);
                AssertHud(panel, dice);
                Assert.That(dice.AvailableRerolls, Is.Zero);
                Assert.That(dice.Energy, Is.Zero);
                Assert.That(dice.Revision, Is.EqualTo(1));
                Assert.That(dice.Values, Has.All.InRange(1, 6));
                Assert.That(panel.GetComponentInChildren<BattleRerollSlide929>().Reveal, Is.LessThan(.01f));
                Save(camera, rt, $"dice-endless-{tag}-1-open.png");

                // 2) All pips spent, the ring two thirds of the way to the next one.
                while (dice.CanReroll) Assert.That(dice.RerollAll(out string error), Is.True, error);
                dice.ChargeForAction1101(0); dice.ChargeForAction1101(0);
                Refresh(panel);
                yield return Wait(.6f);
                AssertHud(panel, dice);
                Assert.That(dice.AvailableRerolls, Is.Zero);
                Assert.That(panel.GetComponentInChildren<BattleRerollSlide929>().Reveal, Is.LessThan(.01f), "No rerolls: retract the whole bar.");
                Assert.That(panel.GetComponentInChildren<BattleRerollSlide929>().GetComponentInChildren<CanvasGroup>().alpha, Is.Zero);
                Assert.That(panel.GetComponentInChildren<BattleRerollSlide929>().GetComponent<Button>().interactable, Is.False, "没豆子按不动");
                Save(camera, rt, $"dice-endless-{tag}-2-charging.png");

                // 3) Store full (3) and the 骰子加成 at its cap: gold ring, gold meter, "MAX".
                for (int guard = 0; guard < 80 && !dice.BonusCapped; guard++)
                {
                    for (int i = 0; i < 3; i++) dice.ChargeForAction1101(0);
                    dice.RerollAll(out _);
                }
                for (int i = 0; i < 9; i++) dice.ChargeForAction1101(0);
                Assert.That(dice.BonusCapped, Is.True, "加成封顶");
                Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls));
                Refresh(panel);
                yield return Wait(.8f);
                AssertHud(panel, dice);
                Save(camera, rt, $"dice-endless-{tag}-3-full-capped.png");
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        static void AssertHud(TacticsBattlePanel panel, DiceTurn dice)
        {
            var reroll = panel.GetComponentInChildren<BattleRerollSlide929>();
            Assert.That(reroll.GetComponentsInChildren<Text>(true).Any(t => t.text == $"重投×{dice.AvailableRerolls}"), Is.True);
            Assert.That(reroll.GetComponentsInChildren<ChoSiren.UI.DiceEndlessHud1101>(true), Is.Empty);
            Assert.That(reroll.GetComponentsInChildren<PsdHomeAlbumInkMotion>(true), Is.Empty, "No rectangular ink frame.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.That(reroll.GetComponentInChildren<ChoSiren.UI.InkFlameEdge1031>(true), Is.Not.Null, "Use the lobby's silhouette edge.");
            foreach (Text text in panel.GetComponentsInChildren<Text>())
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                Assert.That(text.text, Does.Not.Contain("本场剩"), text.name);
                Assert.That(text.text, Does.Not.Contain("重投用完"), text.name);
                Assert.That(text.text, Does.Not.Contain("重投充能"), text.name);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static void Refresh(TacticsBattlePanel panel)
        {
            foreach (string name in new[] { "RefreshBattleHud", "RefreshPsdDice" })
                typeof(TacticsBattlePanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(panel, null);
        }

        static void Save(Camera camera, RenderTexture rt, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(OutDir); File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex);
        }
    }
}

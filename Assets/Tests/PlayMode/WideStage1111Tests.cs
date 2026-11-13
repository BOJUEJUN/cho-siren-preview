using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-11 (玩家反馈 "一屏三个怪，镜头锁定目标"): a 三敌同屏 stage lines three minions up on a stage 1.5× the screen;
    /// the camera pans to the aimed enemy, the backdrop follows at half speed and an off-screen enemy shows a chevron.
    /// Captures into Builds/qa-logs/chapter20-1111/.
    /// </summary>
    public sealed class WideStage1111Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float PsdScale = 720f / 2946f;

        [UnityTest]
        public IEnumerator ThreeMinionsLineUpAndTheCameraPansToTheTarget()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("WideStage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("WideStageCamera", typeof(Camera)).GetComponent<Camera>();
            var events = new GameObject("WideStageEvents", typeof(EventSystem));
            var target = new RenderTexture(1080, 2288, 24);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/chapter20-1111"));
            Directory.CreateDirectory(output);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;

                var data = new GameSave { StoryProgress = 100, CapFloorMigrated1111 = true };
                for (int n = 1; n <= 12; n++) data.ClearedStages.Add(new StageClear { Id = "stage-1-" + n, Stars = 3 });
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-13", 1111UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                // Nobody falls while we look: the fight plays on, but the row stays three strong.
                foreach (BattleUnit enemy in battle.Units.Where(u => u.Side == BattleSide.Enemy)) enemy.MaxHp = enemy.Hp = 10_000_000;
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                    typeof(TacticsBattlePanel).GetField(field, Private)?.SetValue(panel, false);
                // Battle actions keep playing; only our aims move the camera while we look.
                typeof(TacticsBattlePanel).GetField("HoldWideCamera1111", Private).SetValue(panel, true);
                yield return new WaitForSecondsRealtime(1.2f);

                Assert.That(Get<bool>(panel, "WideStage1111"), Is.True, "1-13 is a wide stage");
                var minions = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u.Spawned).ToList();
                Assert.That(minions.Count, Is.EqualTo(3));
                var slots = minions.Select(u => Call<int>(panel, "WideSlotOf1111", u.Id)).OrderBy(s => s).ToArray();
                Assert.That(slots, Is.EqualTo(new[] { 0, 1, 2 }), "one enemy per slot");
                var pan = panel.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WidePan1111");
                var chevronLeft = panel.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WideChevronLeft1111");
                var chevronRight = panel.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WideChevronRight1111");
                var back = panel.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WideStageBackArt1111");

                string[] names = { "left", "middle", "right" };
                float[] expected = { 0f, 2946f * .25f, 2946f * .5f };
                float previousBack = float.NaN;
                for (int slot = 0; slot < 3; slot++)
                {
                    BattleUnit unit = minions.First(u => Call<int>(panel, "WideSlotOf1111", u.Id) == slot);
                    typeof(TacticsBattlePanel).GetMethod("AimWideCamera1111", Private).Invoke(panel, new object[] { unit });
                    yield return new WaitForSecondsRealtime(1.0f);
                    float camera1111 = Get<float>(panel, "WideCamera1111");
                    Assert.That(camera1111, Is.EqualTo(expected[slot]).Within(2f), names[slot] + " pan");
                    Assert.That(pan.anchoredPosition.x, Is.EqualTo(-expected[slot] * PsdScale).Within(1f), names[slot] + " row moved");
                    // The backdrop moves the same way but half as far (parallax), never past its edges.
                    if (!float.IsNaN(previousBack)) Assert.That(back.anchoredPosition.x, Is.LessThan(previousBack), names[slot] + " backdrop follows");
                    previousBack = back.anchoredPosition.x;
                    Assert.That(back.anchoredPosition.x, Is.InRange(-180.5f, .5f));
                    Assert.That(chevronLeft.gameObject.activeSelf, Is.EqualTo(slot == 2), names[slot] + ": left chevron only when the left enemy is off screen");
                    Assert.That(chevronRight.gameObject.activeSelf, Is.EqualTo(slot == 0), names[slot] + ": right chevron only when the right enemy is off screen");
                    Capture(camera, target, Path.Combine(output, $"wide-{slot + 1}-{names[slot]}.png"));
                }

                // The pan from the left to the right-hand enemy is animated, not a cut.
                BattleUnit left = minions.First(u => Call<int>(panel, "WideSlotOf1111", u.Id) == 0);
                BattleUnit right = minions.First(u => Call<int>(panel, "WideSlotOf1111", u.Id) == 2);
                typeof(TacticsBattlePanel).GetMethod("AimWideCamera1111", Private).Invoke(panel, new object[] { left });
                yield return new WaitForSecondsRealtime(1.0f);
                typeof(TacticsBattlePanel).GetMethod("AimWideCamera1111", Private).Invoke(panel, new object[] { right });
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(Get<float>(panel, "WideCamera1111"), Is.InRange(1f, 2946f * .5f - 1f), "the pan is animated, not a cut");
                yield return new WaitForSecondsRealtime(.9f);
                Assert.That(Get<float>(panel, "WideCamera1111"), Is.EqualTo(2946f * .5f).Within(2f));

                // Battle actions aim it: our strike looks at its target, an enemy's turn looks at that enemy.
                typeof(TacticsBattlePanel).GetField("HoldWideCamera1111", Private).SetValue(panel, false);
                BattleUnit hero = battle.Units.First(u => u.Side == BattleSide.Player && u.Alive);
                typeof(TacticsBattlePanel).GetMethod("AimForAction1111", Private).Invoke(panel, new object[] { hero, left });
                Assert.That(Get<float>(panel, "WideCameraGoal1111"), Is.EqualTo(0f).Within(1f), "our strike on the left enemy");
                typeof(TacticsBattlePanel).GetMethod("AimForAction1111", Private).Invoke(panel, new object[] { right, hero });
                Assert.That(Get<float>(panel, "WideCameraGoal1111"), Is.EqualTo(2946f * .5f).Within(1f), "the right enemy's turn");
                Object.DestroyImmediate(panel.gameObject);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
                target.Release(); Object.DestroyImmediate(target);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }

        private static T Get<T>(object panel, string property) =>
            (T)typeof(TacticsBattlePanel).GetProperty(property, Private).GetValue(panel);

        private static T Call<T>(object panel, string method, params object[] args) =>
            (T)typeof(TacticsBattlePanel).GetMethod(method, Private).Invoke(panel, args);

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); shot.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}

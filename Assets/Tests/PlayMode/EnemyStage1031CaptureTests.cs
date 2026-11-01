using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 (赵金 enemy-stage): real-frame captures of the boss and two-minion stages, written to
    /// Builds/qa-logs/zj-1031/enemy-stage/ for visual review against refs/07 and refs/08.
    /// </summary>
    public sealed class EnemyStage1031CaptureTests
    {
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1031", "enemy-stage"));

        [UnityTest]
        public IEnumerator CaptureBossStage() => Capture("stage-1-5", "enemy-stage-boss");

        [UnityTest]
        public IEnumerator CaptureThroneBossWithTwoAdds() => Capture("stage-1-10", "enemy-stage-boss-adds");

        [UnityTest]
        public IEnumerator CaptureTwoMinionStage() => Capture("stage-1-1", "enemy-stage-minions");

        static IEnumerator Capture(string stageId, string file)
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("EnemyStageCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("EnemyStageCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle(stageId, 1031UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                // Statuses our side put on the enemies, so their plates show the icons.
                var clocks = typeof(BattleSimulator).GetField("performerClocks", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(battle) as IDictionary;
                foreach (BattleUnit enemy in battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive))
                {
                    object clock = clocks[enemy.Id];
                    clock.GetType().GetField("ArmorBrokenUntil").SetValue(clock, battle.ElapsedMilliseconds + battle.RoundsToMilliseconds(1));
                    clock.GetType().GetField("PoisonStacks").SetValue(clock, 3);
                }
                Invoke(panel, "RefreshAllCells"); Invoke(panel, "RefreshBattleHud");
                float until = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until) yield return null;
                // A taunt bubble on the first enemy, if the bark layer is present.
                MethodInfo bark = typeof(TacticsBattlePanel).GetMethod("ForceEnemyBark1031", BindingFlags.Instance | BindingFlags.NonPublic);
                BattleUnit first = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
                bark?.Invoke(panel, new object[] { first, "enter" });
                until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Save(camera, rt, file + ".png");
                DumpAt(panel, camera, file, new Vector2(95, 355), new Vector2(150, 355));
                // Mid-hit frame: a critical skill hit on the first enemy.
                MethodInfo hit = typeof(TacticsBattlePanel).GetMethod("DebugEnemyHit1031", BindingFlags.Instance | BindingFlags.NonPublic);
                if (hit != null)
                {
                    hit.Invoke(panel, new object[] { first, true, 1 });
                    yield return null;
                    Save(camera, rt, file + "-hit.png");
                    until = Time.realtimeSinceStartup + .14f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    Save(camera, rt, file + "-hit2.png");
                    // The second enemy (minion stage) or the boss's add, struck from the other side.
                    BattleUnit other = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u != first).FirstOrDefault();
                    if (other != null)
                    {
                        until = Time.realtimeSinceStartup + .8f;
                        while (Time.realtimeSinceStartup < until) yield return null;
                        hit.Invoke(panel, new object[] { other, false, 0 });
                        yield return null;
                        Save(camera, rt, file + "-hit-basic.png");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Lists every visible graphic under the given points (canvas pixels from the top-left), for layout review.</summary>
        static void DumpAt(TacticsBattlePanel panel, Camera camera, string file, params Vector2[] points)
        {
            var lines = new List<string>();
            foreach (Vector2 p in points)
            {
                Vector2 screen = new Vector2(p.x, 1536f - p.y);
                lines.Add($"@ {p}");
                foreach (Graphic g in panel.GetComponentsInChildren<Graphic>())
                {
                    if (!g.enabled || g.color.a <= .01f || !RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, screen, camera)) continue;
                    var path = g.name; var t = g.transform.parent;
                    for (int i = 0; i < 4 && t != null; i++, t = t.parent) path = t.name + "/" + path;
                    lines.Add("  " + path);
                }
            }
            Directory.CreateDirectory(OutDir);
            File.WriteAllLines(Path.Combine(OutDir, file + "-dump.txt"), lines);
        }

        static void Invoke(TacticsBattlePanel panel, string name) => typeof(TacticsBattlePanel)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(panel, null);

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

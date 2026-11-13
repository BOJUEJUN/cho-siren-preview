using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-12 (少爷: "这个角色的大招动画怎么没有了？"): plays 夜莺's ultimate show in a real battle and records,
    /// frame by frame, whether her clip or the still portrait is on screen. Writes Builds/qa-logs/ult-1112/.
    /// </summary>
    public sealed class UltimateShow1112CaptureTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "ult-1112"));

        [UnitySetUp] public IEnumerator CleanScene()
        {
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(app.gameObject);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator YeyingUltimatePlaysHerClip([Values(false, true)] bool auto)
        {
            string before = PlayerPrefs.GetString(GameModel.SaveKey); bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            int reduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            BattlePresentationOptions.ReduceMotion = false;
            var host = new GameObject("Ult1112", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("Ult1112Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            LogAssert.ignoreFailingMessages = true;
            var log = new StringBuilder();
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 1112UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                yield return Wait(1.5f);
                panel.StopAllCoroutines();
                typeof(TacticsBattlePanel).GetField("autoMode", Hidden).SetValue(panel, auto);
                BattleUnit unit = battle.Units.FirstOrDefault(u => u.Side == BattleSide.Player && u.Definition.Id == "yeying")
                    ?? battle.Units.First(u => u.Side == BattleSide.Player);
                log.AppendLine("team: " + string.Join(",", battle.Units.Where(u => u.Side == BattleSide.Player).Select(u => u.Definition.Id)));
                log.AppendLine("caster: " + unit.Definition.Id + " auto=" + auto);
                bool done = false;
                bool began = (bool)typeof(TacticsBattlePanel).GetMethod("BeginUltimateShow", Hidden).Invoke(panel,
                    new object[] { unit, new System.Action<UltimateTimingGrade>(g => { done = true; log.AppendLine("grade " + g); }) });
                log.AppendLine("began " + began);
                var show = (BattleUltimateShow1020)typeof(TacticsBattlePanel).GetField("ultimateShow", Hidden).GetValue(panel);
                var t = typeof(BattleUltimateShow1020);
                float start = Time.realtimeSinceStartup; int shot = 0;
                while (!done && Time.realtimeSinceStartup - start < 14f)
                {
                    var video = (RawImage)t.GetField("video", Hidden).GetValue(show);
                    var portrait = (Image)t.GetField("portrait", Hidden).GetValue(show);
                    var player = (VideoPlayer)t.GetField("player", Hidden).GetValue(show);
                    log.AppendLine($"{Time.realtimeSinceStartup - start:F2}s video={video.enabled} portrait={portrait.enabled} " +
                        $"waiting={t.GetField("waitingForClip", Hidden).GetValue(show)} prepared={player?.isPrepared} playing={player?.isPlaying} " +
                        $"time={player?.time:F2} cut={t.GetField("cutClock", Hidden).GetValue(show)} clock={t.GetField("clock", Hidden).GetValue(show)}");
                    Save(camera, rt, $"{(auto ? "auto" : "manual")}-{shot++:00}.png");
                    yield return Wait(.5f);
                }
            }
            finally
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, (auto ? "auto" : "manual") + ".txt"), log.ToString());
                BattlePresentationOptions.ReduceMotion = reduce == 1;
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                Object.Destroy(host); Object.Destroy(camera.gameObject); rt.Release();
            }
        }

        static IEnumerator Wait(float seconds) { for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;) yield return null; }

        static void Save(Camera camera, RenderTexture rt, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            try { Canvas.ForceUpdateCanvases(); } catch (MissingComponentException) { }
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(OutDir); File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex);
        }
    }
}

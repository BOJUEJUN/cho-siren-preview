using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 (audio): real-frame captures of the ultimate grade bursts over a real battle and of a
    /// 阵亡台词 subtitle on a member card, written to Builds/qa-logs/zj-1031/audio/ for visual review.
    /// </summary>
    public sealed class Audio1031CaptureTests
    {
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1031", "audio", "captures"));

        [UnityTest]
        public IEnumerator CaptureGradeBurstsAndDefeatSubtitle()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            bool reduce = BattlePresentationOptions.ReduceMotion;
            var host = new GameObject("Audio1031Capture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("Audio1031Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            try
            {
                BattlePresentationOptions.ReduceMotion = false;
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 1031UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                yield return Wait(1f);

                // A member's 阵亡台词 subtitle on her card.
                BattleUnit member = battle.Units.First(u => u.Side == BattleSide.Player && u.Definition.Id == "wubai" ||
                    u.Side == BattleSide.Player && TacticsBattlePanel.DefeatSubtitles1031.ContainsKey(u.Definition.Id));
                typeof(TacticsBattlePanel).GetMethod("ShowVoiceSubtitle1031", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(panel, new object[] { member.Definition.Id, BattleVoiceDirector.Cue.Defeat, 1, 2.2f });
                yield return Wait(.35f);
                Save(camera, rt, "defeat-subtitle.png");

                // The four judgement bursts over the real battle, mid-animation (pop settled, flash fading).
                var layer = (RectTransform)panel.transform;
                Font font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                foreach (UltimateGradeTier1031 tier in new[] { UltimateGradeTier1031.Perfect, UltimateGradeTier1031.Great,
                    UltimateGradeTier1031.Good, UltimateGradeTier1031.Miss })
                {
                    var burst = UltimateGradeBurst1031.Play(layer, font, tier, () => false);
                    yield return Wait(.12f);
                    Save(camera, rt, "grade-" + tier.ToString().ToLowerInvariant() + "-pop.png");
                    yield return Wait(.25f);
                    Save(camera, rt, "grade-" + tier.ToString().ToLowerInvariant() + ".png");
                    if (burst != null) Object.Destroy(burst.gameObject);
                    yield return null;
                }
            }
            finally
            {
                BattlePresentationOptions.ReduceMotion = reduce;
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
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

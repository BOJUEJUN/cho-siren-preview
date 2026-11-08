using System.Collections;
using System.IO;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-07 会议 (镜头拉远拉近): an ultimate's landing pushes the battle in, pulls back fast and settles home.</summary>
    public sealed class UltimateCamera1107Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator UltimateImpactPushesInThenReturnsHome()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("UltCam", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("UltCamCamera", typeof(Camera)).GetComponent<Camera>();
            var events = new GameObject("UltCamEvents", typeof(EventSystem));
            var target = new RenderTexture(1080, 2288, 24);
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-1", 1107UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                    typeof(TacticsBattlePanel).GetField(field, Private)?.SetValue(panel, false);
                yield return new WaitForSecondsRealtime(.5f);
                var root = (RectTransform)typeof(TacticsBattlePanel).GetField("psdRoot", Private).GetValue(panel);
                Assert.That(root, Is.Not.Null);
                Vector3 homeScale = root.localScale, homePos = root.localPosition;
                typeof(TacticsBattlePanel).GetMethod("UltimateImpact", Private).Invoke(panel, new object[] { "测试大招", Color.cyan });
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(root.localScale.x / homeScale.x, Is.GreaterThan(1.08f), "the camera pushes in on the landing");
                Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/ui-1107")));
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); shot.Apply(); RenderTexture.active = previous;
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/ui-1107/ultimate-push.png")), shot.EncodeToPNG());
                Object.Destroy(shot);
                yield return new WaitForSecondsRealtime(.9f);
                Assert.That(root.localScale.x, Is.EqualTo(homeScale.x).Within(.001f), "back to the home framing");
                Assert.That(Vector3.Distance(root.localPosition, homePos), Is.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
                target.Release(); Object.DestroyImmediate(target);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }
    }
}

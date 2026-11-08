using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-08 (少爷: "注意角色 UI 遮挡问题，可以大但是 UI 问题太大了"): every boss fills the stage but its visible
    /// figure stays under the top HUD (round, timer, HP bar) and its feet stay on the member cards.
    /// Captures into Builds/qa-logs/boss-fit-1108/.
    /// </summary>
    public sealed class BossFit1108Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float PsdScale = 720f / 2946f;

        [UnityTest]
        public IEnumerator BossesStayBetweenTopHudAndMemberCards()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("BossFit", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("BossFitCamera", typeof(Camera)).GetComponent<Camera>();
            var events = new GameObject("BossFitEvents", typeof(EventSystem));
            var target = new RenderTexture(1080, 2288, 24);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/boss-fit-1108"));
            Directory.CreateDirectory(output);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                foreach (string stage in new[] { "stage-1-5", "stage-1-10", "stage-2-5" })
                {
                    var data = new GameSave { StoryProgress = 100 };
                    for (int n = 1; n <= 10; n++) data.ClearedStages.Add(new StageClear { Id = "stage-1-" + n, Stars = 3 });
                    for (int n = 1; n <= 4; n++) data.ClearedStages.Add(new StageClear { Id = "stage-2-" + n, Stars = 3 });
                    PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
                    var model = new GameModel();
                    var battle = model.StartStageBattle(stage, 1108UL, out string message);
                    Assert.That(battle, Is.Not.Null, stage + ": " + message);
                    var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                    foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                        typeof(TacticsBattlePanel).GetField(field, Private)?.SetValue(panel, false);
                    // The chapter-one entrance movie covers the stage; drop it so the battle layout is what we capture.
                    yield return null;
                    panel.StopAllCoroutines();
                    var movie = panel.transform.Find("BossEntrance1102");
                    if (movie != null) Object.DestroyImmediate(movie.gameObject);
                    typeof(TacticsBattlePanel).GetField("bossEntrancePlaying1102", Private)?.SetValue(panel, false);
                    yield return new WaitForSecondsRealtime(.6f);
                    var root = (RectTransform)typeof(TacticsBattlePanel).GetField("psdRoot", Private).GetValue(panel);
                    Image boss = panel.GetComponentsInChildren<Image>(true).First(i => i.name == "BossPortrait");
                    Assert.That(boss.gameObject.activeInHierarchy, Is.True, stage);
                    // Visible top/bottom of the art in PSD-root units (y down from the top).
                    Vector3[] corners = new Vector3[4];
                    boss.rectTransform.GetWorldCorners(corners);
                    float rootTop = root.rect.yMax;
                    float imageTop = rootTop - root.InverseTransformPoint(corners[1]).y;
                    float imageBottom = rootTop - root.InverseTransformPoint(corners[0]).y;
                    Rect visible = AlphaRows(boss.sprite);
                    float visTop = imageTop + (imageBottom - imageTop) * visible.yMin;
                    float visBottom = imageTop + (imageBottom - imageTop) * visible.yMax;
                    Capture(camera, target, Path.Combine(output, stage + ".png"));
                    Assert.That(visTop, Is.GreaterThanOrEqualTo(735f * PsdScale - 10f), stage + ": the boss must not rise into the top HUD");
                    Assert.That(visBottom, Is.LessThanOrEqualTo(3690f * PsdScale + 10f), stage + ": the boss's feet stay on the member cards");
                    Assert.That(visBottom - visTop, Is.GreaterThan((3670f - 735f) * PsdScale * .8f), stage + ": the boss still fills the stage");
                    Object.DestroyImmediate(panel.gameObject);
                    yield return null;
                }
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
                target.Release(); Object.DestroyImmediate(target);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }

        /// <summary>First and last image rows (as fractions from the top) with clearly visible pixels.</summary>
        private static Rect AlphaRows(Sprite sprite)
        {
            Texture2D source = sprite.texture;
            var rt = RenderTexture.GetTemporary(source.width / 4, source.height / 4, 0);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var small = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); small.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            Color32[] px = small.GetPixels32();
            int first = -1, last = -1;
            for (int y = small.height - 1; y >= 0; y--)
            {
                bool row = false;
                for (int x = 0; x < small.width && !row; x++) row = px[y * small.width + x].a > 40;
                if (!row) continue;
                if (first < 0) first = y;
                last = y;
            }
            int h = small.height;
            Object.Destroy(small);
            if (first < 0) return new Rect(0, 0, 1, 1);
            float top = (h - 1 - first) / (float)h, bottom = (h - last) / (float)h;
            return Rect.MinMaxRect(0, top, 1, bottom);
        }

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

using System.Collections;
using System.IO;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class WebReview1102Tests
    {
        [UnityTest]
        public IEnumerator MapUsesApprovedDiscAndLoadsSelectedStageScene()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey); string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("MapCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("MapCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(858, 1834, 24);
            try
            {
                var fixture = new GameSave { StoryProgress = 100 };
                for (int i = 1; i <= 7; i++) fixture.ClearedStages.Add(new StageClear { Id = "stage-1-" + i, Stars = 3 });
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(fixture));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var map = LevelMapPanel.Open(host.transform, new GameModel());
                float until = Time.realtimeSinceStartup + 3;
                while (Time.realtimeSinceStartup < until) yield return null;
                var disc = map.GetComponentsInChildren<Image>(true).First(i => i.name == "HalfVinyl1102");
                Assert.That(disc.sprite, Is.SameAs(Resources.Load<Sprite>("Art/LevelMap1102/vinyl")));
                Assert.That(disc.rectTransform.rect.width, Is.EqualTo(disc.rectTransform.rect.height));
                var scene = map.GetComponentInChildren<StageSceneArt1102>().GetComponent<RawImage>();
                Assert.That(scene.texture, Is.Not.Null, "streamed stage image must load");
                var previous = scene.texture;
                map.GetComponentsInChildren<Button>().First(b => b.name == "Level-1-2").onClick.Invoke();
                until = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.That(scene.texture, Is.Not.SameAs(previous), "each stage uses its own scene");
                map.GetComponentsInChildren<Button>().First(b => b.name == "Level-1-8").onClick.Invoke();
                until = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < until) yield return null;
                Canvas.ForceUpdateCanvases(); camera.Render();
                var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                var before = RenderTexture.active; RenderTexture.active = rt;
                capture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); capture.Apply(); RenderTexture.active = before;
                string path = Path.GetFullPath("Builds/qa-logs/web-review-1102/map.png"); Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, capture.EncodeToPNG()); Object.DestroyImmediate(capture);
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }

        [UnityTest]
        public IEnumerator ScoringDiceShowForegroundFlamesAndUnscoredDiceDoNot()
        {
            var die = new GameObject("Die", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)die.transform; rect.sizeDelta = new Vector2(90, 90);
                var aura = DiceScoreAura.Attach(rect, Resources.Load<Font>("Fonts/NotoSansSC-Subset"), () => false);
                aura.Show(true, false, 6, 0, new DiceFxStyle(Color.cyan, 1));
                yield return null; yield return null;
                var fx = die.GetComponentInChildren<DiceScoreFire1102>();
                Assert.That(fx.Visible, Is.True);
                Assert.That(fx.transform.GetSiblingIndex(), Is.EqualTo(die.transform.childCount - 1));
                Assert.That(fx.GetComponentsInChildren<RawImage>().All(i => !i.raycastTarget), Is.True);
                aura.Show(false, true, 2, 0, new DiceFxStyle(Color.cyan, 1));
                yield return null;
                Assert.That(fx.Visible, Is.False);
            }
            finally { Object.DestroyImmediate(die); }
        }
    }
}

using System.Collections;
using System.IO;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class FormationDrag1102Tests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator DragFollowsFingerThenSwapsFormationWithoutChangingCaptain()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("FormationDragCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("FormationDragCamera", typeof(Camera)).GetComponent<Camera>();
            var events = new GameObject("FormationDragEvents", typeof(EventSystem));
            var target = new RenderTexture(720, 1536, 24);
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-1", 1102UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                    typeof(TacticsBattlePanel).GetField(field, Private).SetValue(panel, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                int captain = battle.CurrentLeaderId;
                int first = battle.FormationOrder1102[0], second = battle.FormationOrder1102[1];
                GameObject firstCard = Card(panel, battle.FindUnit(first));
                GameObject secondCard = Card(panel, battle.FindUnit(second));
                var firstRect = (RectTransform)firstCard.transform;
                var secondRect = (RectTransform)secondCard.transform;
                Vector2 home = firstRect.anchoredPosition;
                Vector2 secondHome = secondRect.anchoredPosition;
                Vector2 from = RectTransformUtility.WorldToScreenPoint(camera, firstRect.TransformPoint(firstRect.rect.center));
                Vector2 to = RectTransformUtility.WorldToScreenPoint(camera, secondRect.TransformPoint(secondRect.rect.center));
                var pointer = new PointerEventData(events.GetComponent<EventSystem>())
                {
                    pressPosition = from, position = from + Vector2.right * 20,
                    pointerPressRaycast = new RaycastResult { module = host.GetComponent<GraphicRaycaster>() }
                };
                ExecuteEvents.Execute(firstCard, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = Vector2.Lerp(from, to, .6f);
                ExecuteEvents.Execute(firstCard, pointer, ExecuteEvents.dragHandler);
                Assert.That(Vector2.Distance(firstRect.anchoredPosition, home), Is.GreaterThan(10f), "卡片应跟随手指移动，而非仅松开后换位。");
                Save(camera, target, "formation-drag-mid.png");
                pointer.position = to;
                ExecuteEvents.Execute(firstCard, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(firstCard, pointer, ExecuteEvents.endDragHandler);
                Assert.That(battle.FormationOrder1102[0], Is.EqualTo(second));
                Assert.That(battle.FormationOrder1102[1], Is.EqualTo(first));
                Assert.That(battle.CurrentLeaderId, Is.EqualTo(captain));
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(Vector2.Distance(firstRect.anchoredPosition, secondHome), Is.LessThan(1.5f));
                Assert.That(Vector2.Distance(secondRect.anchoredPosition, home), Is.LessThan(1.5f));
                Save(camera, target, "formation-drag-end.png");
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
                target.Release(); Object.DestroyImmediate(target);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }

        static GameObject Card(TacticsBattlePanel panel, BattleUnit unit)
        {
            object cell = typeof(TacticsBattlePanel).GetMethod("FindCell", Private, null, new[] { typeof(BattleUnit) }, null)
                .Invoke(panel, new object[] { unit });
            return (GameObject)cell.GetType().GetField("Root").GetValue(cell);
        }

        static void Save(Camera camera, RenderTexture target, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture before = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            RenderTexture.active = before;
            string path = Path.GetFullPath("Builds/qa-logs/review-polish-1102/" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
        }
    }
}

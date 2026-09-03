using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Coordinate-level regression coverage for the portrait lobby composition.
    /// These checks intentionally avoid screenshots and text preferred-size metrics so the
    /// results do not depend on GPU output, DPI, or the font installed on the test machine.
    /// </summary>
    public sealed class LobbyLayoutRegressionTests
    {
        private const string SaveKey = "ChoSiren.Save.v1";
        private const float PositionTolerance = 0.5f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;

            new GameObject("CHO-SIREN Lobby Layout App").AddComponent<ChoSirenApp>();
            yield return null;

            Assert.That(Object.FindAnyObjectByType<ChoSirenApp>(), Is.Not.Null,
                "The app did not bootstrap for the lobby layout regression test.");
            RequireRect("LobbyCards");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator CanvasUsesApprovedPortraitReferenceResolution()
        {
            CanvasScaler scaler = Object.FindAnyObjectByType<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "The runtime UI requires a CanvasScaler.");
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution.x, Is.EqualTo(720f).Within(PositionTolerance),
                "The approved portrait design width is 720 pixels.");
            Assert.That(scaler.referenceResolution.y, Is.EqualTo(1536f).Within(PositionTolerance),
                "The approved portrait design height is 1536 pixels.");
            Assert.That(scaler.referenceResolution.y, Is.GreaterThan(scaler.referenceResolution.x),
                "The lobby must remain a portrait composition.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyContentEntriesAreUniqueLightweightHotspots()
        {
            RectTransform[] hotspots =
            {
                RequireButtonRect("闪耀舞台"),
                RequireButtonRect("冒险剧本"),
                RequireButtonRect("任务"),
            };

            for (int index = 0; index < hotspots.Length; index++)
            {
                Rect rect = RectInParent(hotspots[index]);
                Assert.That(rect.width, Is.LessThanOrEqualTo(210f + PositionTolerance),
                    $"{hotspots[index].name} 应为轻量热点，不能恢复成宽卡片。");
                Assert.That(rect.height, Is.LessThanOrEqualTo(150f + PositionTolerance),
                    $"{hotspots[index].name} 应为轻量热点，不能恢复成高卡片。");
                Assert.That(hotspots[index].GetComponent<Image>().color.a, Is.LessThanOrEqualTo(0.01f),
                    $"{hotspots[index].name} 的点击层必须透明，让视频舞台保持完整。");
                Assert.That(hotspots[index].GetComponent<Mask>(), Is.Null,
                    $"{hotspots[index].name} 不应使用会形成实体卡片的遮罩。");
                FindText(hotspots[index], hotspots[index].name);
            }

            for (int first = 0; first < hotspots.Length; first++)
            for (int second = first + 1; second < hotspots.Length; second++)
                Assert.That(RectInParent(hotspots[first]).Overlaps(RectInParent(hotspots[second])), Is.False,
                    $"{hotspots[first].name} 与 {hotspots[second].name} 不应重叠。");

            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                    .Count(button => button.name == "LiveOnStage"), Is.EqualTo(1),
                "首页只能有一个开始演出主入口。");
            Assert.That(GameObject.Find("闪耀舞台计划"), Is.Null, "首页入口应使用短标签“闪耀舞台”。");
            Assert.That(GameObject.Find("每日签到"), Is.Null, "签到应合并进任务面板，不应重复占据首页入口。");
            Assert.That(GameObject.Find("直播间"), Is.Null, "演出只能保留一个主入口。");
            Assert.That(GameObject.Find("商城抽卡"), Is.Null, "底部导航功能不应在首页重复出现。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StageCallToActionCopyStaysInsideItsHitRect()
        {
            RectTransform stage = RequireRect("LiveOnStage");
            Text title = FindText(stage, "开始演出");
            Text subtitle = FindText(stage, "舞台已就绪");

            Rect stageRect = RectInParent(stage);
            Assert.That(stageRect.center.x, Is.EqualTo(0f).Within(PositionTolerance),
                "开始演出应居中成为首页唯一主操作。");

            AssertContained(stage, title.rectTransform, "开始演出");
            AssertContained(stage, subtitle.rectTransform, "舞台已就绪");
            Assert.That(title.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(title.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            Assert.That(subtitle.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(subtitle.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TopBarControlsHaveAccessibleNonOverlappingHitAreas()
        {
            RectTransform mail = RequireButtonRect("Mail");
            RectTransform music = RequireButtonRect("Music");
            RectTransform settings = RequireButtonRect("Settings");
            RectTransform[] controls = { mail, music, settings };

            foreach (RectTransform control in controls)
            {
                Rect hitRect = RectInParent(control);
                Assert.That(hitRect.width, Is.GreaterThanOrEqualTo(40f - PositionTolerance),
                    $"{control.name} 的点击热区宽度至少应为 40 设计像素。");
                Assert.That(hitRect.height, Is.GreaterThanOrEqualTo(44f - PositionTolerance),
                    $"{control.name} 的点击热区高度至少应为 44 设计像素。");
            }

            for (int first = 0; first < controls.Length; first++)
            {
                for (int second = first + 1; second < controls.Length; second++)
                {
                    Rect a = RectInParent(controls[first]);
                    Rect b = RectInParent(controls[second]);
                    Assert.That(a.Overlaps(b), Is.False,
                        $"{controls[first].name} 与 {controls[second].name} 的点击热区不应重叠。");
                }
            }

            yield return null;
        }

        private static void AssertContained(RectTransform parent, RectTransform child, string label)
        {
            Rect parentRect = parent.rect;
            Rect childRect = RectRelativeTo(parent, child);
            Assert.That(childRect.xMin, Is.GreaterThanOrEqualTo(parentRect.xMin - PositionTolerance),
                $"{label} 的左侧溢出 LiveOnStage 热区。");
            Assert.That(childRect.xMax, Is.LessThanOrEqualTo(parentRect.xMax + PositionTolerance),
                $"{label} 的右侧溢出 LiveOnStage 热区。");
            Assert.That(childRect.yMin, Is.GreaterThanOrEqualTo(parentRect.yMin - PositionTolerance),
                $"{label} 的底部溢出 LiveOnStage 热区。");
            Assert.That(childRect.yMax, Is.LessThanOrEqualTo(parentRect.yMax + PositionTolerance),
                $"{label} 的顶部溢出 LiveOnStage 热区。");
        }

        private static Rect RectInParent(RectTransform rect)
        {
            RectTransform parent = rect.parent as RectTransform;
            Assert.That(parent, Is.Not.Null, $"{rect.name} must have a RectTransform parent.");
            return RectRelativeTo(parent, rect);
        }

        private static Rect RectRelativeTo(RectTransform coordinateSpace, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 first = coordinateSpace.InverseTransformPoint(corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 point = coordinateSpace.InverseTransformPoint(corners[index]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Text FindText(RectTransform parent, string value)
        {
            Text result = parent.GetComponentsInChildren<Text>(true)
                .SingleOrDefault(candidate => candidate.text == value);
            Assert.That(result, Is.Not.Null, $"Expected text '{value}' under {parent.name} was not found.");
            return result;
        }

        private static RectTransform RequireButtonRect(string objectName)
        {
            RectTransform result = RequireRect(objectName);
            Button button = result.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, $"{objectName} must expose a Button component.");
            Assert.That(button.isActiveAndEnabled, Is.True, $"{objectName} must be active and enabled.");
            Assert.That(button.targetGraphic, Is.Not.Null, $"{objectName} must have a target graphic.");
            Assert.That(button.targetGraphic.raycastTarget, Is.True,
                $"{objectName} must receive UI raycasts across its hit area.");
            return result;
        }

        private static RectTransform RequireRect(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            Assert.That(result, Is.Not.Null, $"Expected active UI object '{objectName}' was not found.");
            RectTransform rect = result.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, $"{objectName} must have a RectTransform.");
            return rect;
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null) Object.Destroy(objects[index].gameObject);
            }
        }
    }
}

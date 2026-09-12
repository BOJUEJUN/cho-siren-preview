using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BustPortraitDiagTests
    {
        [UnityTest]
        public IEnumerator RenderMembersScreen()
        {
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
            // 必须销毁整个 gameObject——只销毁组件会留下带死 sprite 的孤儿 UI 树污染后续测试。
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(app.gameObject);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(es.gameObject);
            yield return null;
            new GameObject("DiagApp").AddComponent<ChoSirenApp>();
            float timeout = Time.realtimeSinceStartup + 30f;
            while (GameObject.Find("StartupLoading") != null && Time.realtimeSinceStartup < timeout)
                yield return null;
            for (int i = 0; i < 90; i++) yield return null; // 等淡出动画结束
            Object.Destroy(GameObject.Find("StartupLoading")); // 诊断环境无视频回调，直接移除

            var navBtn = GameObject.Find("Nav-members");
            Debug.Log("DIAG nav=" + (navBtn == null ? "NULL" : "found active=" + navBtn.activeInHierarchy));
            navBtn.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 30; i++) yield return null;

            int memberCards = 0, frames = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name.StartsWith("Member-")) memberCards++;
                if (t.name == "PortraitFrame") frames++;
            }
            Debug.Log($"DIAG afterNav memberCards={memberCards} frames={frames}");
            foreach (Canvas cv in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                int membersUnder = 0;
                foreach (Transform t in cv.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("Member-")) membersUnder++;
                Debug.Log($"DIAG canvas {cv.name} order={cv.sortingOrder} mode={cv.renderMode} members={membersUnder}");
            }

            // 全部 Canvas 切到相机模式，渲染到 RenderTexture 存 PNG 自查；
            // 渲染完必须还原——禁用的画布与相机模式会泄漏给后续测试。
            var camGo = new GameObject("DiagCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
            cam.orthographic = true;
            var touched = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                bool hasMembers = false;
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("Member-")) { hasMembers = true; break; }
                touched.Add(canvas);
                if (!hasMembers) { canvas.gameObject.SetActive(false); continue; } // 关掉背景画布
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
            RenderTexture rt = new RenderTexture(720, 1536, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(720, 1536, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 720, 1536), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes("/tmp/diag-members.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(tex);
            Object.Destroy(camGo);
            foreach (Canvas canvas in touched)
            {
                if (canvas == null) continue;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.gameObject.SetActive(true);
            }
            Debug.Log("DIAG saved /tmp/diag-members.png");

            // 收尾拆掉自建 app，与别的用例的 TearDown 行为一致。
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(app.gameObject);
            yield return null;
        }
    }
}

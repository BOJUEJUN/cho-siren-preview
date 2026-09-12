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

            GameObject.Find("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 30; i++) yield return null;

            RenderTo("/tmp/diag-members.png", "Member-");

            // 再打开档案弹窗渲染一张，验证按钮布局与胸像。
            GameObject.Find("Member-xingli").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 15; i++) yield return null;
            RenderTo("/tmp/diag-modal.png", "MemberModal");

            // 再切到饰品页渲染一张，验证大立绘主角区与紧凑详情条。
            GameObject.Find("Nav-accessory").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 15; i++) yield return null;
            RenderTo("/tmp/diag-accessory.png", "AccessoryDetail");

            // 收尾拆掉自建 app，与别的用例的 TearDown 行为一致。
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(app.gameObject);
            yield return null;
        }

        /// <summary>把含 marker 对象的那个 Canvas 切到相机模式渲出 PNG；其余画布临时关闭，渲染完全部还原。</summary>
        private static void RenderTo(string path, string marker)
        {
            var camGo = new GameObject("DiagCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
            cam.orthographic = true;
            var touched = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                bool hasMarker = false;
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith(marker)) { hasMarker = true; break; }
                touched.Add(canvas);
                if (!hasMarker) { canvas.gameObject.SetActive(false); continue; }
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
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
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
            Debug.Log("DIAG saved " + path);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ReferenceScreenCapture
{
    const string ActiveKey = "CHO.ReferenceCapture.Active";
    const string Output = "/Users/bojuejun/Documents/ChatGPT/Codex/cho-progress/assets/current";
    static readonly string[] Pages = { "lobby", "team", "members", "profile", "accessory", "audition", "map", "battle" };
    static int step;
    static double due;
    static bool navigated;
    static ReferenceScreenCapture()
    {
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        SetGameViewSize();
        SessionState.SetBool(ActiveKey + ".HadSave", PlayerPrefs.HasKey(ChoSiren.GameModel.SaveKey));
        SessionState.SetString(ActiveKey + ".Save", PlayerPrefs.GetString(ChoSiren.GameModel.SaveKey, ""));
        SessionState.SetBool(ActiveKey, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        due = EditorApplication.timeSinceStartup + 10;
        EditorApplication.isPlaying = true;
    }
    static void SetGameViewSize()
    {
        Assembly asm = typeof(Editor).Assembly;
        Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance").GetValue(null);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        Type sizeType = asm.GetType("UnityEditor.GameViewSize");
        Type kind = asm.GetType("UnityEditor.GameViewSizeType");
        object entry = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { Enum.ToObject(kind, 1), 720, 1536, "CHO Capture 720x1536" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { entry });
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var view = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, total - 1);
        view.Show();
    }
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < due) return;
        try
        {
            var app = UnityEngine.Object.FindAnyObjectByType<ChoSiren.ChoSirenApp>();
            if (app == null) { due = EditorApplication.timeSinceStartup + 1; return; }
            if (step >= Pages.Length)
            {
                RestoreSave();
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                File.WriteAllText(Path.Combine(Output, "capture-complete.txt"), DateTime.Now.ToString("O"));
                Debug.Log("CHO_CAPTURE_COMPLETE");
                EditorApplication.Exit(0);
                return;
            }
            if (!navigated)
            {
                var loading = GameObject.Find("StartupLoading");
                if (loading != null) UnityEngine.Object.Destroy(loading);
                string page = Pages[step];
                if (page == "profile") Call(app, "OpenMember", 0);
                else if (page == "map") { Call(app, "ShowScreen", "lobby"); Call(app, "OpenLevelMap"); }
                else if (page == "battle") Call(UnityEngine.Object.FindAnyObjectByType<ChoSiren.LevelMapPanel>(), "StartChallenge");
                else Call(app, "ShowScreen", page);
                navigated = true;
                due = EditorApplication.timeSinceStartup + (step == 0 ? 8 : 2);
                return;
            }
            Capture(Path.Combine(Output, "20260915-" + Pages[step] + ".png"));
            step++;
            navigated = false;
            due = EditorApplication.timeSinceStartup + .25;
        }
        catch (Exception ex)
        {
            RestoreSave();
            Debug.LogException(ex);
            File.WriteAllText(Path.Combine(Output, "capture-error.txt"), ex.ToString());
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
    static void RestoreSave()
    {
        if (SessionState.GetBool(ActiveKey + ".HadSave", false))
            PlayerPrefs.SetString(ChoSiren.GameModel.SaveKey, SessionState.GetString(ActiveKey + ".Save", ""));
        else PlayerPrefs.DeleteKey(ChoSiren.GameModel.SaveKey);
        PlayerPrefs.Save();
    }
    static void Capture(string file)
    {
        var go = new GameObject("ReferenceCaptureCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.02f, .02f, .06f);
        cam.orthographic = true;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        var rt = new RenderTexture(720, 1536, 24);
        cam.targetTexture = rt;
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            canvases[i].worldCamera = cam;
            canvases[i].planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var before = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(720, 1536, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 720, 1536), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = before;
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = modes[i];
            canvases[i].worldCamera = cameras[i];
            canvases[i].planeDistance = distances[i];
        }
        cam.targetTexture = null;
        rt.Release();
        UnityEngine.Object.Destroy(tex);
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(go);
        Debug.Log("CHO_CAPTURE_SAVED " + file);
    }
}

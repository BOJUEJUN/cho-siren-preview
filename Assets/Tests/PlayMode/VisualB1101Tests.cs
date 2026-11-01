using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 visual-b): review captures of the audition / album / accessory / level map /
    /// team / roster / member-detail pages at phone (1080×2288) and Mac-window (1080×2160) ratios,
    /// plus a per-page count of the visible words (CJK + Latin letters; numbers excluded).
    /// Output: Builds/qa-logs/zj-1101c/visual-b/&lt;phase&gt;/ where phase = env VISUALB_PHASE (default "after").
    /// </summary>
    public sealed class VisualB1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private GameModel model;
        private string save, legacy;
        private bool had, hadLegacy;
        private readonly List<string> counts = new List<string>();

        private static string Phase => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VISUALB_PHASE"))
            ? "after" : Environment.GetEnvironmentVariable("VISUALB_PHASE");
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Builds/qa-logs/zj-1101c/visual-b", Phase));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var seed = new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, MusicEnabled = false, SfxEnabled = false };
            seed.OwnedAccessories = new List<int> { 0, 1, 2, 3, 4, 5, 9, 12 };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(seed));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("VisualB1101").AddComponent<ChoSirenApp>();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Find("StartupLoading") != null && Time.realtimeSinceStartup < deadline) yield return null;
            model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator CaptureVisualBPagesAtPhoneAndMacRatios()
        {
            model.EquipAccessoryForMember(model.Save.Team[0], 0, out _);
            foreach (var size in new[] { new Vector2Int(1080, 2288), new Vector2Int(1080, 2160) })
            {
                string tag = size.y == 2288 ? "phone" : "mac";
                var camera = new GameObject("VisualB1101Camera", typeof(Camera)).GetComponent<Camera>();
                var rt = new RenderTexture(size.x, size.y, 24);
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                try
                {
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    camera.orthographic = true; camera.targetTexture = rt;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                    foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                    {
                        fitter.enabled = false;
                        var safe = (RectTransform)fitter.transform;
                        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                        safe.offsetMin = safe.offsetMax = Vector2.zero;
                    }
                    canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
                    yield return null; Canvas.ForceUpdateCanvases();

                    // ---- 选秀 / 面试
                    Invoke("ShowScreen", "audition");
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "01-audition-online", tag);
                    GachaPanel gacha = app.GetComponentInChildren<GachaPanel>();
                    Call(gacha, "SelectInterviewPool", 1);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "02-audition-offline", tag);
                    Call(gacha, "SelectInterviewPool", 0);
                    yield return null;
                    int candidate = model.InterviewCandidates(0).First();
                    int cost = model.InterviewQuote(0, candidate);
                    Call(gacha, "ShowSignConfirm", candidate, cost);
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "03-sign-confirm", tag, "SignConfirmOverlay");
                    int diamonds = model.Save.Diamonds;
                    model.Save.Diamonds = Mathf.Max(0, cost - 37);
                    Call(gacha, "ShowSignConfirm", candidate, cost);
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "04-sign-confirm-short", tag, "SignConfirmOverlay");
                    model.Save.Diamonds = diamonds;
                    Call(gacha, "ShowSignedCard", candidate);
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "05-sign-result", tag, "SignResultOverlay");
                    Call(gacha, "CloseSignModal");

                    // ---- 专辑制作（成就纪录）
                    Invoke("ShowScreen", "lobby");
                    yield return null;
                    Invoke("OpenAchievements");
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "06-album", tag, "AchievementModal");
                    Invoke("CloseModal");

                    // ---- 饰品
                    Invoke("ShowScreen", "accessory");
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "07-accessory", tag);
                    Invoke("OpenAccessoryCard", 0, -1, null);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "08-accessory-card", tag, "AccessoryCard");
                    DestroyNamed("AccessoryCard");
                    Invoke("OpenAccessoryCard", 1, model.Save.Team[1], null);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "09-accessory-card-member", tag, "AccessoryCard");
                    DestroyNamed("AccessoryCard");
                    Invoke("OpenAccessoryMemberPicker", 1);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "10-accessory-wear", tag, "AccessoryMemberPicker");
                    DestroyNamed("AccessoryMemberPicker");
                    Invoke("OpenAccessorySwap", model.Save.Team[0], GameModel.AccessorySlot(0), null);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "11-accessory-swap", tag, "AccessorySwap");
                    DestroyNamed("AccessorySwap");
                    Invoke("OpenEquipmentMemberSelector", 0, true, "", 0);
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, "12-accessory-member-selector", tag, "EquipmentPolish038Selector", "EquipmentMemberPicker");
                    Invoke("CloseModal");

                    // ---- 团队 / 成员
                    Invoke("ShowScreen", "team");
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "15-team", tag);
                    Invoke("ShowScreen", "members");
                    yield return new WaitForSecondsRealtime(.4f);
                    Shot(camera, rt, "16-members", tag);

                    // ---- 成员详情 · 技能 / 档案
                    Invoke("OpenMember", model.Save.Team[0]);
                    yield return new WaitForSecondsRealtime(.4f);
                    Click("DetailTab-1");
                    yield return new WaitForSecondsRealtime(.2f);
                    Shot(camera, rt, "17-detail-skill", tag, "DetailPanel");
                    Click("DetailTab-2");
                    yield return new WaitForSecondsRealtime(.2f);
                    Shot(camera, rt, "18-detail-profile", tag, "DetailPanel");
                    Click("DetailTab-0");
                    yield return null;
                    Invoke("CloseModal");
                    // ---- 关卡地图
                    Invoke("ShowScreen", "lobby");
                    yield return null;
                    Invoke("OpenLevelMap");
                    yield return new WaitForSecondsRealtime(.8f);
                    LevelMapPanel map = app.GetComponentInChildren<LevelMapPanel>();
                    Shot(camera, rt, "13-levelmap", tag);
                    if (map != null)
                    {
                        Call(map, "OpenDropPreview");
                        yield return new WaitForSecondsRealtime(.3f);
                        Shot(camera, rt, "14-levelmap-drops", tag, "StageLootModal");
                        Call(map, "CloseChapterModal");
                        map.Close();
                    }
                    float settle = Time.realtimeSinceStartup + 6f;
                    while (UI.ChallengeVideoTransition.IsCoveringGame && Time.realtimeSinceStartup < settle) yield return null;
                    yield return new WaitForSecondsRealtime(.3f);

                    Invoke("ShowScreen", "lobby");
                    yield return null;
                }
                finally
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                    Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                }
            }
            Directory.CreateDirectory(Out);
            File.WriteAllLines(Path.Combine(Out, "text-counts.tsv"), counts, Encoding.UTF8);
        }

        // ------------------------------------------------------------------ helpers

        private void Shot(Camera camera, RenderTexture rt, string page, string tag, params string[] scope)
        {
            Canvas.ForceUpdateCanvases();
            Transform root = scope.Select(Find).FirstOrDefault(g => g != null)?.transform ?? app.transform;
            if (tag == "phone") counts.Add(page + "\t" + CountWords(root, out string list) + "\t" + list);
            Capture(camera, rt, $"{page}-{tag}.png");
        }

        /// <summary>Visible words on screen (shared header / dock / toast excluded): CJK + Latin letters.</summary>
        private int CountWords(Transform root, out string list)
        {
            var parts = new List<string>();
            int total = 0;
            foreach (Text text in root.GetComponentsInChildren<Text>())
            {
                if (!text.enabled || string.IsNullOrWhiteSpace(text.text) || text.color.a < .08f) continue;
                if (Under(text.transform, "TopBar", "BottomNavigation", "Toast", "StartupLoading")) continue;
                CanvasGroup group = text.GetComponentInParent<CanvasGroup>();
                if (group != null && group.alpha < .08f) continue;
                int n = text.text.Count(c => char.IsLetter(c));
                if (n == 0) continue;
                total += n;
                parts.Add(text.text.Replace("\n", "/").Replace("\t", " "));
            }
            list = string.Join(" | ", parts);
            return total;
        }

        private static bool Under(Transform t, params string[] names)
        {
            for (; t != null; t = t.parent) if (names.Contains(t.name)) return true;
            return false;
        }

        private void DestroyNamed(string name)
        {
            foreach (Transform t in app.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray())
                Object.DestroyImmediate(t.gameObject);
        }

        private void Click(string name)
        {
            GameObject target = Find(name);
            if (target != null) target.GetComponent<Button>().onClick.Invoke();
        }

        private static void Call(Component target, string name, params object[] args)
        {
            if (target == null) return;
            MethodInfo method = target.GetType().GetMethods(Private)
                .First(m => m.Name == name && m.GetParameters().Length == args.Length);
            method.Invoke(target, args);
        }

        private void Invoke(string name, params object[] args)
        {
            MethodInfo method = typeof(ChoSirenApp).GetMethods(Private)
                .First(m => m.Name == name && m.GetParameters().Length >= args.Length
                    && m.GetParameters().Skip(args.Length).All(p => p.HasDefaultValue));
            object[] full = args.Concat(method.GetParameters().Skip(args.Length).Select(p => p.DefaultValue)).ToArray();
            method.Invoke(app, full);
        }

        private GameObject Find(string name) => app == null ? null : app.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == name && t.gameObject.activeInHierarchy).Select(t => t.gameObject).FirstOrDefault();

        private static void Capture(Camera camera, RenderTexture rt, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), tex.EncodeToPNG());
            RenderTexture.active = old; Object.DestroyImmediate(tex);
        }
    }
}

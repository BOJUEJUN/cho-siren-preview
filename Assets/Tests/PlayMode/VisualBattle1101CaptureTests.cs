using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-02 (赵金 visual-battle, 用户: "用最少文字的方案，多用设计跟图去表达、音效去表达"): real-frame captures of the
    /// battle screens — normal fight, boss charge warning, 禁魔, pause, rules, defeat — on a phone (1080×2288) and the Mac
    /// window ratio (1080×2160), plus a count of the text that stays on the battle screen. Written to
    /// Builds/qa-logs/zj-1101c/visual-battle/&lt;VB1101_SHOT or "shot"&gt;/.
    /// </summary>
    public sealed class VisualBattle1101CaptureTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Prefix => System.Environment.GetEnvironmentVariable("VB1101_SHOT") ?? "shot";
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1101c", "visual-battle", Prefix));

        bool hadReduce; int reduce;

        [UnitySetUp] public IEnumerator CleanScene()
        {
            foreach (var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(app.gameObject);
            yield return null; yield return null;
        }

        [SetUp] public void Before()
        {
            hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            BattlePresentationOptions.ReduceMotion = false;
        }

        [TearDown] public void After()
        {
            BattlePresentationOptions.ReduceMotion = reduce == 1;
            if (!hadReduce) PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [UnityTest] public IEnumerator Phone() => Run(1080, 2288, "phone");
        [UnityTest] public IEnumerator MacWindow() => Run(1080, 2160, "mac");

        sealed class Rig
        {
            public GameObject Host; public Camera Camera; public RenderTexture Rt;
            public void Dispose()
            {
                if (Host != null) Object.DestroyImmediate(Host);
                if (Camera != null) Object.DestroyImmediate(Camera.gameObject);
                if (Rt != null) { Rt.Release(); Object.DestroyImmediate(Rt); }
            }
        }

        static Rig NewRig(int w, int h)
        {
            var rig = new Rig
            {
                Host = new GameObject("VisualBattle1101", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)),
                Camera = new GameObject("VisualBattle1101Camera", typeof(Camera)).GetComponent<Camera>(),
                Rt = new RenderTexture(w, h, 24),
            };
            rig.Camera.clearFlags = CameraClearFlags.SolidColor; rig.Camera.backgroundColor = Color.black;
            rig.Camera.orthographic = true; rig.Camera.targetTexture = rig.Rt;
            var canvas = rig.Host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = rig.Camera; canvas.planeDistance = 1;
            var scaler = rig.Host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
            return rig;
        }

        static IEnumerator Run(int w, int h, string tag)
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            LogAssert.ignoreFailingMessages = true; // a pre-existing Fx1023 clipping warning must not hide the captures
            var report = new StringBuilder();
            Rig rig = null;
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));

                // ---- 1. a normal two-minion fight, with two status pop-ups on the enemies
                rig = NewRig(w, h);
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 1101UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                TacticsBattlePanel panel = TacticsBattlePanel.Open(rig.Host.transform, model, battle, null);
                panel.StopAllCoroutines();
                Invoke(panel, "RefreshAllCells"); Invoke(panel, "RefreshBattleHud"); Invoke(panel, "RefreshDiceUi");
                yield return Wait(2.8f); // past the landing hand's big stamp
                QuietStamp(panel);
                Save(rig, $"{tag}-1-normal-idle.png");
                report.AppendLine(CountText(panel, $"{tag} normal idle"));
                var enemies = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive).ToList();
                BattleUnit captain = battle.FindUnit(battle.CurrentLeaderId) ?? battle.Units.First(u => u.Side == BattleSide.Player);
                Present(panel, new BattleEvent { Kind = BattleEventKind.Buff, ActorId = captain.Id, TargetId = enemies[0].Id, SkillId = "rt-armor-break", Amount = 1 });
                if (enemies.Count > 1)
                    Present(panel, new BattleEvent { Kind = BattleEventKind.Buff, ActorId = captain.Id, TargetId = enemies[1].Id, SkillId = "rt-slow", Amount = 1 });
                yield return Wait(.28f);
                Save(rig, $"{tag}-2-normal-status.png");
                // Wave hand target reached (目标达成): the captain banner.
                Present(panel, new BattleEvent { Kind = BattleEventKind.Buff, ActorId = captain.Id, TargetId = captain.Id, SkillId = BattleSimulator.DiceTargetSkillId, Amount = 1 });
                yield return Wait(.5f);
                Save(rig, $"{tag}-3-normal-target.png");
                rig.Dispose(); rig = null;
                yield return null;

                // ---- 2. boss charge warning and 禁魔
                rig = NewRig(w, h);
                model = new GameModel();
                battle = model.StartStageBattle("stage-1-5", 1101UL, out message);
                Assert.That(battle, Is.Not.Null, message);
                panel = TacticsBattlePanel.Open(rig.Host.transform, model, battle, null);
                panel.StopAllCoroutines();
                Invoke(panel, "RefreshAllCells"); Invoke(panel, "RefreshBattleHud"); Invoke(panel, "RefreshDiceUi");
                yield return Wait(2.8f);
                QuietStamp(panel);
                BattleUnit boss = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive && battle.IsStageBoss(u));
                var clocks = (IDictionary)typeof(BattleSimulator).GetField("performerClocks", Hidden).GetValue(battle);
                object bossClock = clocks[boss.Id];
                bossClock.GetType().GetField("CastUntil").SetValue(bossClock, battle.ElapsedMilliseconds + battle.RoundsToMilliseconds(1));
                var planned = (IDictionary)typeof(BattleSimulator).GetField("plannedSpecial1101", Hidden).GetValue(battle);
                planned[boss.Id] = EnemySpecialCapability.MassSilence;
                Present(panel, new BattleEvent { Kind = BattleEventKind.Buff, ActorId = boss.Id, TargetId = boss.Id, SkillId = "rt-cast", Amount = 1 });
                yield return Wait(.7f);
                Save(rig, $"{tag}-4-boss-charge.png");
                report.AppendLine(CountText(panel, $"{tag} boss charge"));
                yield return Wait(.6f);
                Save(rig, $"{tag}-4b-boss-charge-hold.png");

                // The charge releases: 群体禁魔 seals the whole team.
                bossClock.GetType().GetField("CastUntil").SetValue(bossClock, 0);
                planned.Remove(boss.Id);
                var members = battle.Units.Where(u => u.Side == BattleSide.Player && u.Alive).ToList();
                foreach (BattleUnit member in members)
                {
                    object c = clocks[member.Id];
                    c.GetType().GetField("SilencedUntil").SetValue(c, battle.ElapsedMilliseconds + battle.RoundsToMilliseconds(2));
                }
                Present(panel, new BattleEvent { Kind = BattleEventKind.ActionStarted, ActorId = boss.Id, TargetId = members[0].Id, SkillId = BattleSimulator.BossMassSilenceSkillId, TimeMilliseconds = 77001 });
                foreach (BattleUnit member in members)
                    Present(panel, new BattleEvent { Kind = BattleEventKind.Buff, ActorId = boss.Id, TargetId = member.Id, SkillId = "rt-seal", Amount = 2 });
                Invoke(panel, "RefreshAllCells");
                yield return Wait(.35f);
                Save(rig, $"{tag}-5-silence.png");
                yield return Wait(1.0f);
                Save(rig, $"{tag}-5b-silence-after.png");
                report.AppendLine(CountText(panel, $"{tag} silenced"));

                // ---- 3. pause and rules
                Invoke(panel, "TogglePause");
                yield return Wait(.4f);
                Save(rig, $"{tag}-6-pause.png");
                report.AppendLine(CountText(panel, $"{tag} pause"));
                Invoke(panel, "OpenRulesOverlay");
                yield return Wait(.4f);
                Save(rig, $"{tag}-7-rules.png");
                report.AppendLine(CountText(panel, $"{tag} rules"));
                rig.Dispose(); rig = null;
                yield return null;

                // ---- 4. defeat
                rig = NewRig(w, h);
                model = new GameModel();
                battle = model.StartStageBattle("stage-1-1", 1101UL, out message);
                panel = TacticsBattlePanel.Open(rig.Host.transform, model, battle, null);
                panel.StopAllCoroutines();
                yield return Wait(.6f);
                battle.AutoPlay(0);
                Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
                Invoke(panel, "ShowResult");
                yield return Wait(2.4f);
                Save(rig, $"{tag}-8-defeat.png");
                report.AppendLine(CountText(panel, $"{tag} defeat"));
            }
            finally
            {
                rig?.Dispose();
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, tag + "-text.txt"), report.ToString());
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Every visible text on the panel: its characters (CJK and others, digits apart) and the strings.</summary>
        static string CountText(TacticsBattlePanel panel, string label)
        {
            foreach (var hud in Object.FindObjectsByType<VisualBattleHud1101>(FindObjectsSortMode.None)) hud.Tick?.Invoke();
            int cjk = 0, other = 0, digits = 0;
            var lines = new List<string>();
            Canvas.ForceUpdateCanvases();
            foreach (Text text in panel.GetComponentsInChildren<Text>())
            {
                if (!text.enabled || string.IsNullOrWhiteSpace(text.text) || text.color.a < .05f) continue;
                if (!Visible(text)) continue;
                string plain = System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", string.Empty);
                foreach (char ch in plain)
                {
                    if (char.IsWhiteSpace(ch)) continue;
                    if (ch >= 0x4e00 && ch <= 0x9fff) cjk++;
                    else if (char.IsDigit(ch)) digits++;
                    else other++;
                }
                lines.Add("    " + text.name + ": " + plain.Replace("\n", " / "));
            }
            return $"[{label}] 汉字 {cjk} · 其它字符 {other} · 数字 {digits}\n" + string.Join("\n", lines);
        }

        static bool Visible(Graphic g)
        {
            for (Transform t = g.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && group.alpha < .05f) return false;
                if (t.localScale.x == 0f || t.localScale.y == 0f) return false;
            }
            return true;
        }

        /// <summary>Stopping the panel's coroutines strands the landing hand's big stamp; a real frame retires it.</summary>
        static void QuietStamp(TacticsBattlePanel panel)
        {
            object show = typeof(TacticsBattlePanel).GetField("comboShow", Hidden)?.GetValue(panel);
            show?.GetType().GetMethod("Cancel")?.Invoke(show, null);
        }

        static void Present(TacticsBattlePanel panel, BattleEvent e) =>
            typeof(TacticsBattlePanel).GetMethod("PresentEvent", Hidden).Invoke(panel, new object[] { e, -1 });

        static void Invoke(TacticsBattlePanel panel, string name) =>
            typeof(TacticsBattlePanel).GetMethod(name, Hidden)?.Invoke(panel, null);

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static void Save(Rig rig, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // The test resumes between Update and LateUpdate; run the late picture refresh a real frame would get.
            foreach (var hud in Object.FindObjectsByType<VisualBattleHud1101>(FindObjectsSortMode.None)) hud.Tick?.Invoke();
            Canvas.ForceUpdateCanvases(); rig.Camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rig.Rt;
            var tex = new Texture2D(rig.Rt.width, rig.Rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rig.Rt.width, rig.Rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(OutDir); File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex);
        }
    }
}

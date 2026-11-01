using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 battle-fx): the small-skill close-up is a foreground layer above the enemy stage, its plates,
    /// bubble and pop-ups (below the pause/rules overlays), larger, and our hits on enemies use the big numbers.
    /// The captures go to Builds/qa-logs/zj-1101/battle-fx/ for review against refs/03, 04, 07 and 08.
    /// </summary>
    public sealed class BattleFx1101CaptureTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1101", "battle-fx"));

        bool hadReduce; int reduce;

        /// <summary>Captures need a clean scene without the auto-started app canvas (as in LobbyVisual1031Tests).</summary>
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

        [UnityTest]
        public IEnumerator MinionStageCloseupLayeringAndBigNumbers() => Run("stage-1-1", "minions", true);

        [UnityTest]
        public IEnumerator BossStageCloseupAndBigNumbers() => Run("stage-1-5", "boss", false);

        [UnityTest]
        public IEnumerator BossWithAddsCloseup() => Run("stage-1-10", "boss-adds", false);

        static IEnumerator Run(string stageId, string tag, bool full)
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("BattleFx1101Capture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("BattleFx1101Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            // A pre-existing Fx1023 clipping exception (also in the 0.9.19 baseline) must not hide these captures.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle(stageId, 1101UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                var clocks = typeof(BattleSimulator).GetField("performerClocks", Hidden).GetValue(battle) as IDictionary;
                foreach (BattleUnit enemy in battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive))
                {
                    object clock = clocks[enemy.Id];
                    clock.GetType().GetField("ArmorBrokenUntil").SetValue(clock, battle.ElapsedMilliseconds + battle.RoundsToMilliseconds(1));
                    clock.GetType().GetField("PoisonStacks").SetValue(clock, 3);
                }
                Invoke(panel, "RefreshAllCells"); Invoke(panel, "RefreshBattleHud");
                yield return Wait(1.2f);

                // ---- layering: the close-up's own layer sits above the PSD stage and the pop-ups, under the overlays
                var closeup = (BattleActiveCloseup1019)typeof(TacticsBattlePanel).GetField("psdCloseup1019", Hidden).GetValue(panel);
                var big = (BigDamageNumbers1101)typeof(TacticsBattlePanel).GetField("bigDamage1101", Hidden).GetValue(panel);
                Assert.That(closeup, Is.Not.Null); Assert.That(big, Is.Not.Null);
                Transform psd = Find(panel.transform, "BattlePSD928"), foreground = Find(panel.transform, "BattleForeground1101");
                Transform popups = Find(panel.transform, "Popups"), bigLayer = Find(panel.transform, "BigDamage1101");
                var pause = (GameObject)typeof(TacticsBattlePanel).GetField("pauseOverlay", Hidden).GetValue(panel);
                Assert.That(closeup.Root.parent, Is.SameAs(foreground));
                Assert.That(foreground.parent, Is.SameAs(psd.parent));
                Assert.That(foreground.GetSiblingIndex(), Is.GreaterThan(psd.GetSiblingIndex()), "above every PSD stage layer (figures, plates, bubbles, rings)");
                Assert.That(foreground.GetSiblingIndex(), Is.GreaterThan(popups.GetSiblingIndex()), "above the status pop-ups");
                Assert.That(foreground.GetSiblingIndex(), Is.GreaterThan(bigLayer.GetSiblingIndex()));
                Assert.That(foreground.GetSiblingIndex(), Is.LessThan(pause.transform.GetSiblingIndex()), "the pause menu stays above it");

                // A bark bubble + a status pop-up on the first enemy, like refs/03.
                BattleUnit first = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
                typeof(TacticsBattlePanel).GetMethod("ForceEnemyBark1031", Hidden)?.Invoke(panel, new object[] { first, "enter" });
                yield return Wait(.45f);
                Save(camera, rt, tag + "-0-stage.png");

                List<BattleUnit> members = battle.Units.Where(u => u.Side == BattleSide.Player).ToList();
                var authored = members.Where(u => BattleAuthoredCutin1019.KeyForMember(u.Definition.Id) != null).ToList();
                Assert.That(authored.Count, Is.GreaterThan(0));
                int stamp = 900000;
                foreach (BattleUnit member in full ? authored : authored.Take(1).ToList())
                {
                    closeup.Cancel();
                    typeof(TacticsBattlePanel).GetMethod("ForceEnemyBark1031", Hidden)?.Invoke(panel, new object[] { first, "enter" });
                    Spawn(panel, first, "迟缓");
                    yield return Wait(.15f);
                    typeof(TacticsBattlePanel).GetMethod("PresentAction", Hidden).Invoke(panel, new object[] { member, first,
                        new BattleEvent { Kind = BattleEventKind.Damage, ActorId = member.Id, TargetId = first.Id, TimeMilliseconds = stamp++,
                            SkillId = battle.ActiveSkillId(member, false) } });
                    Assert.That(closeup.IsShowing, Is.True, member.Definition.Id + " small skill shows its close-up");
                    Assert.That(closeup.UsesAuthoredCutin, Is.True);
                    // Its hit lands while the close-up holds the foreground: the number waits for it to slip away.
                    Present(panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = member.Id, TargetId = first.Id,
                        SkillId = battle.ActiveSkillId(member, false), Amount = 2468, HpLostAmount = 2468 });
                    Assert.That(big.PendingCount, Is.EqualTo(1));
                    yield return Wait(.07f);
                    Save(camera, rt, $"{tag}-1-closeup-{member.Definition.Id}-enter.png");
                    yield return Wait(.23f);
                    for (float until = Time.realtimeSinceStartup + 2f; closeup.Presence < .99f && Time.realtimeSinceStartup < until;) yield return null;
                    yield return null;
                    Assert.That(panel.GetType().GetProperty("EnemyReadoutAlpha1101", Hidden).GetValue(panel), Is.LessThan(.1f),
                        "enemy plates and bubble fade under the close-up");
                    Save(camera, rt, $"{tag}-1-closeup-{member.Definition.Id}.png");
                    Dump(foreground, camera, $"{tag}-1-closeup-{member.Definition.Id}-dump.txt");
                    if (!full) break;
                }
                // The beat ends: the plates come back and the held number lands.
                yield return Wait(1.9f);
                Assert.That(closeup.IsShowing, Is.False);
                Assert.That((float)panel.GetType().GetProperty("EnemyReadoutAlpha1101", Hidden).GetValue(panel), Is.EqualTo(1f).Within(.001f));
                Assert.That(big.PendingCount, Is.Zero);
                yield return Wait(1.2f);

                if (full)
                {
                    // Members without authored paper art: the torn-paper card with their own illustration.
                    foreach (string id in new[] { "chengxia", "yaoguang" })
                    {
                        closeup.Cancel();
                        Sprite art = Resources.Load<Sprite>("Art/Members/member-" + id);
                        if (art == null) continue;
                        string name = GameModel.Members.FirstOrDefault(m => m.Id == id)?.Name ?? id;
                        Assert.That(closeup.Play(art, name, "星河回响", new Color32(255, 106, 211, 255), false, id), Is.True);
                        yield return Wait(.42f);
                        Save(camera, rt, $"{tag}-2-closeup-fallback-{id}.png");
                    }
                    closeup.Cancel();
                    yield return Wait(.3f);
                }

                // ---- big numbers
                BattleUnit hitter = members[0];
                big.Clear();
                Present(panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = hitter.Id, TargetId = first.Id,
                    SkillId = "rt-basic", Amount = 1284, HpLostAmount = 1284 });
                yield return Wait(.05f);
                Save(camera, rt, tag + "-3-damage-pop.png");
                yield return Wait(.25f);
                Assert.That(big.TryNewest(out int shown, out var style, out float width, out float cap, out _), Is.True);
                Assert.That(shown, Is.EqualTo(1284)); Assert.That(style, Is.EqualTo(DamageNumberStyle1101.Gold));
                Assert.That(width, Is.InRange(720f / 3f, 720f * .62f), "about a third to a half of the screen wide");
                Save(camera, rt, tag + "-4-damage-normal.png");
                yield return Wait(1.1f);
                Assert.That(big.ActiveCount, Is.Zero, "the number floats away and retires");

                Present(panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = hitter.Id, TargetId = first.Id,
                    SkillId = "rt-basic", Amount = 15009, HpLostAmount = 15009, Critical = true });
                yield return Wait(.3f);
                Assert.That(big.TryNewest(out shown, out style, out float critWidth, out float critCap, out _), Is.True);
                Assert.That(style, Is.EqualTo(DamageNumberStyle1101.Critical));
                Assert.That(critCap, Is.GreaterThan(cap), "a critical is larger");
                Save(camera, rt, tag + "-5-damage-crit.png");
                yield return Wait(1.3f);

                // Three quick hits on one enemy stack: newest on top, the earlier ones pushed below it.
                int[] chain = { 3460, 4645, 5212 };
                foreach (int amount in chain)
                {
                    Present(panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = hitter.Id, TargetId = first.Id,
                        SkillId = battle.ActiveSkillId(hitter, false), Amount = amount, HpLostAmount = amount });
                    yield return Wait(.13f);
                }
                Assert.That(big.ActiveCountFor(first.Id), Is.EqualTo(3));
                yield return Wait(.12f);
                Save(camera, rt, tag + "-6-damage-chain.png");
                var roots = panel.GetComponentsInChildren<RectTransform>().Where(t => t.name.StartsWith("BigDamage-") && t.gameObject.activeInHierarchy).ToList();
                Assert.That(roots.Count, Is.EqualTo(3));
                var newest = roots.OrderBy(r => r.GetSiblingIndex()).Last();
                foreach (RectTransform older in roots.Where(r => r != newest))
                    Assert.That(older.anchoredPosition.y, Is.LessThan(newest.anchoredPosition.y - 20f), "earlier hits sit below the newest");
                yield return Wait(1.4f);

                // Our members' hurt values stay moderate pop-ups.
                BattleUnit attacker = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
                Present(panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = attacker.Id, TargetId = hitter.Id,
                    SkillId = "rt-enemy-basic", Amount = 86, HpLostAmount = 86 });
                Assert.That(big.ActiveCount, Is.Zero);
                yield return Wait(.2f);
                Save(camera, rt, tag + "-7-damage-on-member.png");
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void ReducedMotionNumbersOnlyFadeAndPauseFreezesThem()
        {
            var root = new GameObject("BigDamageFixture", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(720, 1500);
                bool paused = false, hold = false;
                var big = root.AddComponent<BigDamageNumbers1101>();
                big.Configure(rect, () => paused, () => .02f, () => hold, new Rect(0, 0, 720, 1500));
                Assert.That(big.Spawn(new Vector2(360, 500), 4645, DamageNumberStyle1101.Gold, 7), Is.True);
                Assert.That(big.TryNewest(out int amount, out _, out float width, out _, out RectTransform number), Is.True);
                Assert.That(amount, Is.EqualTo(4645));
                Assert.That(width, Is.InRange(BigDamageNumbers1101.MinWidth, BigDamageNumbers1101.MaxWidth));
                var body = number.Find("Body");
                big.Step(.03f);
                Assert.That(body.localScale.x, Is.GreaterThan(.3f));
                paused = true; Vector3 frozen = body.localScale; big.Step(.5f);
                Assert.That(body.localScale, Is.EqualTo(frozen), "pause freezes the numbers");
                paused = false;
                big.Clear();
                BattlePresentationOptions.ReduceMotion = true;   // the setter also drops the per-frame cache
                big.Spawn(new Vector2(360, 500), 999, DamageNumberStyle1101.Critical, 7);
                big.TryNewest(out _, out _, out _, out _, out number);
                body = number.Find("Body");
                for (int i = 0; i < 20; i++) { big.Step(.02f); Assert.That(body.localScale.x, Is.EqualTo(1f).Within(.0001f), "no bounce with reduced motion"); }
                Vector2 at = number.anchoredPosition;
                big.Step(.4f);
                Assert.That(number.anchoredPosition.y, Is.EqualTo(at.y).Within(.01f), "no rise with reduced motion");
                big.Clear();
                hold = true;
                big.Spawn(new Vector2(360, 500), 12, DamageNumberStyle1101.Skill, 9);
                Assert.That(big.PendingCount, Is.EqualTo(1)); Assert.That(big.ActiveCount, Is.Zero);
                hold = false; big.Step(.02f);
                Assert.That(big.PendingCount, Is.Zero); Assert.That(big.ActiveCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Spawn(TacticsBattlePanel panel, BattleUnit target, string text) =>
            typeof(TacticsBattlePanel).GetMethod("SpawnPopup", Hidden).Invoke(panel, new object[] { target, text, (Color)new Color32(255, 205, 110, 255), 20 });

        static void Present(TacticsBattlePanel panel, BattleEvent entry) =>
            typeof(TacticsBattlePanel).GetMethod("PresentEvent", Hidden).Invoke(panel, new object[] { entry, -1 });

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static Transform Find(Transform root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Assert.That(found, Is.Not.Null, name);
            return found;
        }

        /// <summary>Visible state of every graphic under <paramref name="root"/> (screen rect, alpha chain), for review.</summary>
        static void Dump(Transform root, Camera camera, string filename)
        {
            var lines = new List<string>();
            var corners = new Vector3[4];
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                g.rectTransform.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                float chain = 1f;
                foreach (CanvasGroup cg in g.GetComponentsInParent<CanvasGroup>()) chain *= cg.alpha;
                lines.Add($"{g.name} active={g.gameObject.activeInHierarchy} enabled={g.enabled} a={g.color.a:0.00} cr={g.canvasRenderer.GetAlpha():0.00} group={chain:0.00} " +
                          $"screen=({a.x:0},{1536 - b.y:0})-({b.x:0},{1536 - a.y:0}) cull={g.canvasRenderer.cull} sprite={(g is Image im && im.sprite != null ? im.sprite.name : "-")}");
            }
            Transform panel = root.parent;
            lines.Add("---- panel children");
            for (int i = 0; panel != null && i < panel.childCount; i++)
            {
                Transform c = panel.GetChild(i);
                var rt = c as RectTransform;
                lines.Add($"{i} {c.name} active={c.gameObject.activeInHierarchy} graphics={c.GetComponentsInChildren<Graphic>().Length} " +
                          (rt != null ? $"size={rt.rect.size} scale={rt.localScale.x:0.000} pos={rt.anchoredPosition}" : ""));
            }
            var canvas = root.GetComponentInParent<Canvas>();
            lines.Add($"canvas={canvas?.name} root={canvas?.rootCanvas.name} mode={canvas?.renderMode}");
            Directory.CreateDirectory(OutDir);
            File.WriteAllLines(Path.Combine(OutDir, filename), lines);
        }

        static void Invoke(TacticsBattlePanel panel, string name) => typeof(TacticsBattlePanel)
            .GetMethod(name, Hidden)?.Invoke(panel, null);

        static void Save(Camera camera, RenderTexture rt, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            try { Canvas.ForceUpdateCanvases(); }
            catch (MissingComponentException) { /* pre-existing Fx1023 clip-registry noise (baseline); the frame still renders */ }
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(OutDir); File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex);
        }
    }
}

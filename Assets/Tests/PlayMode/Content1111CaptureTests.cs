using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-11 (赵金 11 月内容): a 第三章 boss fight with the three new members — the boss barrier badge, each new
    /// member's native-size 贴脸图, the 弹反 prompt — and the 第三章 map with 骰子搭配. Isolated save; captures into
    /// Builds/qa-logs/content-1111/.
    /// </summary>
    public sealed class Content1111CaptureTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/content-1111"));

        private GameObject host, events;
        private Camera camera;
        private RenderTexture target;
        private bool had;
        private string save;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey);
            save = PlayerPrefs.GetString(GameModel.SaveKey);
            host = new GameObject("Content1111", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            camera = new GameObject("Content1111Camera", typeof(Camera)).GetComponent<Camera>();
            events = new GameObject("Content1111Events", typeof(EventSystem));
            target = new RenderTexture(1080, 2304, 24);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.targetTexture = target;
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
            Directory.CreateDirectory(Output);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(events);
            target.Release(); Object.DestroyImmediate(target);
            TacticsBattlePanel.ParryAutoGrade1111 = null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.Save();
            yield return null;
        }

        private static GameModel ChapterThreeModel(int clearedChapterThree)
        {
            var data = new GameSave { StoryProgress = 100, CapFloorMigrated1111 = true };
            for (int n = 1; n <= 20; n++)
            {
                data.ClearedStages.Add(new StageClear { Id = "stage-1-" + n, Stars = 3 });
                data.ClearedStages.Add(new StageClear { Id = "stage-2-" + n, Stars = 3 });
            }
            for (int n = 1; n <= clearedChapterThree; n++) data.ClearedStages.Add(new StageClear { Id = "stage-3-" + n, Stars = 2 });
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
            var model = new GameModel();
            int[] team = new[] { "huansha", "huanhuoling", "linyin", "feiyin" }.Select(GameModel.IndexOfMember).ToArray();
            foreach (int index in team)
            {
                if (!model.Save.UnlockedMembers.Contains(index)) model.Save.UnlockedMembers.Add(index);
                model.Save.MemberLevels[index] = 50;
            }
            model.Save.Team = team.ToList();
            model.Save.RelicStarterGiven1111 = true;
            model.Save.RelicInventory1111 = new System.Collections.Generic.List<string> {
                RelicDice1111.Chaos, RelicDice1111.Bloom, RelicDice1111.Mend, RelicDice1111.Blade, RelicDice1111.Blade, RelicDice1111.Venom };
            model.Save.RelicCarry1111 = new System.Collections.Generic.List<string> { RelicDice1111.Chaos, RelicDice1111.Bloom, RelicDice1111.Mend };
            model.Save.Stamina = 200;
            return model;
        }

        [UnityTest]
        public IEnumerator ChapterThreeBossFightShowsBarrierCutinsAndParry()
        {
            GameModel model = ChapterThreeModel(4);
            BattleSimulator battle = model.StartStageBattle("stage-3-5", 1111UL, out string message);
            Assert.That(battle, Is.Not.Null, message);
            BattlePsdArt928.LoadLocal();
            var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
            foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                typeof(TacticsBattlePanel).GetField(field, Private)?.SetValue(panel, false);
            // Let the 首领登场 card play out; look while the layers still stand.
            for (float until = Time.realtimeSinceStartup + 12f; Time.realtimeSinceStartup < until && battle.TurnRound < 1;) yield return null;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(battle.Chapter3Rules1111, Is.True);
            BattleUnit boss = battle.Units.First(u => u.Side == BattleSide.Enemy && battle.IsStageBoss(u));
            Assert.That(battle.BarrierMax(boss), Is.EqualTo(BattleSimulator.ShieldLayersMiniBoss), "小BOSS 3 层顶盾");
            Assert.That(battle.Barrier(boss) == 0 || battle.StatusesOf(boss).Any(s => s.Value.StartsWith("盾×")), Is.True, "the layers hang on the boss");
            var closeup = (BattleActiveCloseup1019)typeof(TacticsBattlePanel).GetField("psdCloseup1019", Private).GetValue(panel);
            Assert.That(closeup, Is.Not.Null);
            for (float until = Time.realtimeSinceStartup + 4f; closeup.IsShowing && Time.realtimeSinceStartup < until;) yield return null;
            Capture("ch3-boss-barrier.png");
            foreach (string id in new[] { "huansha", "huanhuoling", "linyin" })
            {
                for (float until = Time.realtimeSinceStartup + 3f; closeup.IsShowing && Time.realtimeSinceStartup < until;) yield return null;
                var gapField = typeof(BattleActiveCloseup1019).GetField("gap", Private);
                for (float until = Time.realtimeSinceStartup + 3f; ((float)gapField.GetValue(closeup) > 0f || closeup.IsShowing) &&
                     Time.realtimeSinceStartup < until;) yield return null;
                Sprite art = BattlePsdArt928.Sprite(BattleAuthoredCutin1019.KeyForMember(id));
                Assert.That(art, Is.Not.Null, id);
                bool played = closeup.Play(art, id, MemberSkillNames1103.Name(id, CombatRace.Charm, false), Color.cyan, true, id);
                Assert.That(played, Is.True, $"{id} active={closeup.isActiveAndEnabled} showing={closeup.IsShowing} " +
                    $"reduce={BattlePresentationOptions.ReduceMotion} gap={typeof(BattleActiveCloseup1019).GetField("gap", Private).GetValue(closeup)} " +
                    $"banner={typeof(BattleActiveCloseup1019).GetField("banner", Private).GetValue(closeup) != null}");
                closeup.AdvancePresentation(.35f);
                Assert.That(closeup.ArtRect.width, Is.GreaterThan(720f), id + " is larger than the screen, as drawn in the PSD");
                Capture($"ch3-cutin-{id}.png");
                closeup.AdvancePresentation(2f);
            }

            var prompt = ParryPrompt1111.Create(panel.transform, Resources.Load<Font>("Fonts/NotoSansSC-Subset"));
            UltimateTimingGrade? graded = null;
            prompt.Play(() => false, g => graded = g);
            prompt.Advance(UltimateTimingChallenge.DurationSeconds * .5f);
            Capture("ch3-parry-prompt.png");
            prompt.Advance(UltimateTimingChallenge.DurationSeconds * (UltimateTimingChallenge.Target - .5f));
            prompt.Tap();
            Capture("ch3-parry-perfect.png");
            prompt.Advance(1f);
            Assert.That(graded, Is.EqualTo(UltimateTimingGrade.Perfect));
            Object.DestroyImmediate(panel.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChapterThreeMapAndCarriedRelicDice()
        {
            GameModel model = ChapterThreeModel(10);
            Assert.That(model.RelicDiceUnlocked, Is.True);
            Assert.That(model.CarriedRelics, Is.EqualTo(new[] { RelicDice1111.Chaos, RelicDice1111.Bloom, RelicDice1111.Mend }));
            // Earlier tests may have left the map on another chapter; a fresh player opens on the newest one.
            typeof(LevelMapPanel).GetField("lastChapter", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 0);
            var map = LevelMapPanel.Open(host.transform, model);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(typeof(LevelMapPanel).GetField("chapter", Private).GetValue(map), Is.EqualTo(3), "opens on 第三章");
            Capture("ch3-map.png");
            BattleSimulator battle = model.StartStageBattle("stage-3-11", 7UL, out string message);
            Assert.That(battle, Is.Not.Null, message);
            Assert.That(battle.RelicDice, Is.EqualTo(model.CarriedRelics));
            BattleSimulator early = model.StartStageBattle("stage-2-3", 7UL, out message);
            Assert.That(early, Is.Not.Null, message);
            Assert.That(early.RelicDice.Count, Is.Zero, "前两章没有多骰玩法");
            Object.DestroyImmediate(map.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomepageRelicEntryOpensTheDicePage([Values(false, true)] bool locked)
        {
            GameModel seed = ChapterThreeModel(10);
            if (locked)
            {
                // 2026-11-12: before 第三章 the entry is still there, with a lock badge and an unlock hint on the page.
                seed.Save.ClearedStages.RemoveAll(c => c.Id == "stage-2-20" || c.Id.StartsWith("stage-3-"));
                seed.Save.RelicStarterGiven1111 = false;
                seed.Save.RelicInventory1111.Clear(); seed.Save.RelicCarry1111.Clear();
            }
            string tag = locked ? "-locked" : string.Empty;
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(seed.Save));
            PlayerPrefs.SetInt("cho-agreement-v1", 1);
            var app = new GameObject("Relic1111App").AddComponent<ChoSirenApp>();
            var appCamera = new GameObject("Relic1111AppCamera", typeof(Camera)).GetComponent<Camera>();
            appCamera.transform.SetParent(app.transform, false);
            appCamera.clearFlags = CameraClearFlags.SolidColor; appCamera.backgroundColor = Color.black;
            appCamera.orthographic = true; appCamera.targetTexture = target;
            yield return null;
            Canvas canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = appCamera; canvas.planeDistance = 10;
            Camera keep = camera; camera = appCamera;
            try
            {
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private).Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private).Invoke(app, null);
                yield return new WaitForSecondsRealtime(1.6f);
                var entry = app.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "RelicDiceEntry1111");
                Assert.That(entry, Is.Not.Null, "功能骰 entry on the homepage");
                Capture($"relic-home-entry{tag}.png");
                Assert.That(app.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "RelicUnlockCaption"), Is.EqualTo(locked));
                entry.GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(app.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "RelicDiceModal1111"), Is.True);
                Capture($"relic-page{tag}.png");
                Assert.That(app.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "RelicLockBanner"), Is.EqualTo(locked));
            }
            finally
            {
                camera = keep;
                Object.Destroy(app.gameObject);
                PlayerPrefs.DeleteKey("cho-agreement-v1");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewMembersOnTheMembersPage()
        {
            // 2026-11-12 少爷: 新角色直接送所有人 — an old save without them gets them on load.
            var old = new GameSave { StoryProgress = 100, PlayerName = "经纪人" };
            old.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(old));
            PlayerPrefs.SetInt("cho-agreement-v1", 1);
            var app = new GameObject("Members1111App").AddComponent<ChoSirenApp>();
            var appCamera = new GameObject("Members1111AppCamera", typeof(Camera)).GetComponent<Camera>();
            appCamera.transform.SetParent(app.transform, false);
            appCamera.clearFlags = CameraClearFlags.SolidColor; appCamera.backgroundColor = Color.black;
            appCamera.orthographic = true; appCamera.targetTexture = target;
            yield return null;
            Canvas canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = appCamera; canvas.planeDistance = 10;
            SafeAreaFitter.Layout(new Rect(0, 0, target.width, target.height), new Vector2(target.width, target.height),
                out Vector2 min, out Vector2 max, out bool wide);
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = min; safe.anchorMax = max; safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
            Camera keep = camera; camera = appCamera;
            try
            {
                var model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                foreach (string id in GameModel.GiftedMembers1112)
                    Assert.That(model.IsUnlocked(GameModel.IndexOfMember(id)), Is.True, id + " is owned");
                typeof(ChoSirenApp).GetMethod("CloseModal", Private).Invoke(app, null);
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private).Invoke(app, new object[] { "members" });
                yield return new WaitForSecondsRealtime(2f);
                typeof(ChoSirenApp).GetMethod("CloseModal", Private).Invoke(app, null);
                yield return new WaitForSecondsRealtime(.5f);
                Capture("members-new.png");
            }
            finally
            {
                camera = keep;
                Object.Destroy(app.gameObject);
                PlayerPrefs.DeleteKey("cho-agreement-v1");
            }
            yield return null;
        }

        private void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); shot.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Output, name), shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}

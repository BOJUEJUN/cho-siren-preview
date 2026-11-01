// Isolated review fixture. Requires its own company name and explicit capture directory.
// Filter: ChoSiren.Tests.UnifiedUiReview20261013Tests.RepresentativeRoutesRenderAndReturn
// Required: CHO_UI_REVIEW_CAPTURE=/absolute/review/output
// 20 PNGs. System/inventory/map/rules boards retain native 720x1536 cells (no downsampling).
// Uses real Unity Camera.Render/RenderTexture. No cloud generation, protocol acceptance or publishing.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class UnifiedUiReview20261013Tests
    {
        private const string ReviewCompany = "CHO-SIREN UI Review 20261013";
        private const int Width = 720, Height = 1536;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private GameModel model;
        private Canvas canvas;
        private CanvasScaler scaler;
        private Camera camera;
        private RenderTexture target;
        private string output;
        private bool backedUp, oldScalerEnabled;
        private RenderMode oldMode;
        private Camera oldCamera;
        private float oldPlane, oldScale, oldTimeScale;
        private readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        private readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        private readonly HashSet<string> existingKeys = new HashSet<string>();
        private readonly List<Texture2D> temporary = new List<Texture2D>();
        private readonly List<GameObject> createdEventSystems = new List<GameObject>();
        private readonly Report report = new Report();

        [Serializable] private sealed class Capture
        {
            public string file;
            public string[] cells;
            public int cellWidth = Width, cellHeight = Height, columns;
        }
        [Serializable] private sealed class Report
        {
            public string company, startedUtc, completedUtc, outcome;
            public int settlementCallbacks;
            public bool complete;
            public List<Capture> captures = new List<Capture>();
            public List<string> checks = new List<string>();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            output = Environment.GetEnvironmentVariable("CHO_UI_REVIEW_CAPTURE");
            if (string.IsNullOrWhiteSpace(output)) Assert.Ignore("Set CHO_UI_REVIEW_CAPTURE in the isolated UI-review project to run capture acceptance.");
            // Fail before touching any prefs if accidentally imported into the live company.
            Assert.That(Application.companyName, Is.EqualTo(ReviewCompany), "Only the isolated UI-review company may run this fixture.");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "Run PlayMode with a graphics device; -nographics cannot produce acceptance images.");
            Assert.That(string.IsNullOrWhiteSpace(output), Is.False, "CHO_UI_REVIEW_CAPTURE must name the evidence folder.");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            report.company = Application.companyName;
            report.startedUtc = DateTime.UtcNow.ToString("O");
            BackupString(GameModel.SaveKey); BackupString(GameModel.LegacySaveKey);
            BackupInt(MemberTrainingPractice.ReduceMotionPreferenceKey);
            BackupInt(BattlePresentationOptions.ReduceMotionKey);
            backedUp = true;
            oldTimeScale = Time.timeScale; Time.timeScale = 1f;
            foreach (ChoSirenApp old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;

            // Fresh reviewed-company fixture, with original strings retained for finally/TearDown restoration.
            // InMemorySaveStore constructs defaults without reading or deleting a real player's save.
            var fresh = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(fresh.Save));
            PlayerPrefs.Save();
            var systemsBefore = new HashSet<UnityEngine.EventSystems.EventSystem>(
                Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None));
            app = new GameObject("Siren UI Review Fixture App").AddComponent<ChoSirenApp>();
            foreach (var system in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                if (!systemsBefore.Contains(system)) createdEventSystems.Add(system.gameObject);
            yield return null;
            model = Get<GameModel>(app, "model");
            canvas = app.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            scaler = canvas.GetComponent<CanvasScaler>();
            oldScalerEnabled = scaler != null && scaler.enabled;
            oldMode = canvas.renderMode; oldCamera = canvas.worldCamera;
            oldPlane = canvas.planeDistance; oldScale = canvas.scaleFactor;
            camera = new GameObject("SirenUiReviewCaptureCamera").AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            target = new RenderTexture(Width, Height, 24); target.Create(); camera.targetTexture = target;
            // Keep route construction on a native portrait canvas, avoiding headless GameView aspect artifacts.
            // Original canvas/scaler state is restored in TearDown; every snapshot also has its own finally.
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10f; canvas.scaleFactor = 1f;
            Canvas.ForceUpdateCanvases();
            Call(app, "ShowScreen", "lobby");
            yield return Settle();
            float startupDeadline = Time.realtimeSinceStartup + 3f;
            while (Find(app.transform, "StartupLoading", false) != null && Time.realtimeSinceStartup < startupDeadline)
                yield return null;
            Assert.That(app.GetComponentsInChildren<PsdHomeMotion>(true).Any(x => x.gameObject.activeInHierarchy), Is.True);
            // Wait for the real presentation fade; never accept a consent gate in a fixture.
            Assert.That(Find(app.transform, "StartupLoading", false), Is.Null, "A blocking startup must finish before UI review.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                RestoreCanvas();
                if (app != null) Object.Destroy(app.gameObject);
                foreach (GameObject system in createdEventSystems) if (system != null) Object.Destroy(system);
                yield return null;
            }
            finally
            {
                if (backedUp)
                {
                    foreach (var pair in strings)
                        if (existingKeys.Contains(pair.Key)) PlayerPrefs.SetString(pair.Key, pair.Value); else PlayerPrefs.DeleteKey(pair.Key);
                    foreach (var pair in ints)
                        if (existingKeys.Contains(pair.Key)) PlayerPrefs.SetInt(pair.Key, pair.Value); else PlayerPrefs.DeleteKey(pair.Key);
                    PlayerPrefs.Save(); Time.timeScale = oldTimeScale;
                }
                foreach (Texture2D image in temporary) if (image != null) Object.DestroyImmediate(image);
                temporary.Clear();
                if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (!string.IsNullOrEmpty(output))
                {
                    report.completedUtc = DateTime.UtcNow.ToString("O");
                    File.WriteAllText(Path.Combine(output, "review-manifest.json"), JsonUtility.ToJson(report, true));
                }
            }
        }

        [UnityTest]
        public IEnumerator WubaiActionsRemainVisibleAcrossSafariViewportChanges()
        {
            int wubai = Array.FindIndex(GameModel.Members, member => member.Id == "wubai");
            Assert.That(wubai, Is.GreaterThanOrEqualTo(0));
            RectTransform safe = Get<Transform>(app, "safeRoot").GetComponent<RectTransform>();
            Vector2 oldSize = safe.sizeDelta;
            try
            {
                Call(app, "OpenMember", wubai); yield return Settle();
                Save("21-wubai-transparent", "wubai-native-alpha");
                Click(Find(app.transform, "MemberModal"), "CloseTop"); yield return Settle();
                safe.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1268f);
                Canvas.ForceUpdateCanvases();
                Call(app, "OpenMember", wubai); yield return Settle();
                AssertDetailActionsFit();
                Click(Find(app.transform, "MemberModal"), "DetailTab-1"); yield return Settle();
                Transform originalModal = Find(app.transform, "MemberModal");
                foreach (float height in new[] { 1536f, 1268f })
                {
                    safe.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    yield return Settle();
                    Assert.That(Find(app.transform, "MemberModal"), Is.SameAs(originalModal));
                    Assert.That(Get<int>(app, "memberDetailTab"), Is.EqualTo(1));
                    Assert.That(Find(originalModal, "MemberName").GetComponent<Text>().text, Is.EqualTo("雾白"));
                    AssertDetailActionsFit();
                }
                report.checks.Add("Wubai quick equip, unequip, tabs and back/home stay inside a 720x1268 viewport, including resize while open.");
                report.checks.Add("The same member modal and selected skill tab survive Safari viewport changes.");
                report.complete = true; report.outcome = "Wubai transparency and short viewport accepted";
            }
            finally { safe.sizeDelta = oldSize; Canvas.ForceUpdateCanvases(); }
        }

        private void AssertDetailActionsFit()
        {
            Canvas.ForceUpdateCanvases();
            Transform modal = Find(app.transform, "MemberModal");
            RectTransform bounds = modal.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            foreach (string name in new[] { "DetailQuickEquip", "DetailUnequipAll", "DetailTab-0", "DetailTab-1", "DetailTab-2", "CloseTop", "DetailHome" })
            {
                RectTransform button = Find(modal, name).GetComponent<RectTransform>();
                button.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = bounds.InverseTransformPoint(corner);
                    Assert.That(local.y, Is.InRange(bounds.rect.yMin - .5f, bounds.rect.yMax + .5f), name + " must be vertically visible");
                    // The authored back plate intentionally bleeds past the left page edge.
                    if (name != "CloseTop") Assert.That(local.x, Is.InRange(bounds.rect.xMin - .5f, bounds.rect.xMax + .5f), name + " must be horizontally reachable");
                }
            }
        }

        [UnityTest]
        public IEnumerator RepresentativeRoutesRenderAndReturn()
        {
            yield return Settle(); Save("01-home", "home");
            Call(app, "OpenDailyTasks"); yield return Settle();
            var tasks = RequireComponent<TaskBoardPanel>();
            Assert.That(tasks.Cadence, Is.EqualTo(ChoSiren.Systems.Economy.TaskCadence.Daily));
            Save("02-tasks-daily", "tasks-daily");
            Click(tasks.transform, "TabWeekly"); yield return Settle();
            Assert.That(tasks.Cadence, Is.EqualTo(ChoSiren.Systems.Economy.TaskCadence.Weekly));
            Save("03-tasks-weekly", "tasks-weekly");
            Click(tasks.transform, "Back"); yield return Settle(); AssertGone<TaskBoardPanel>(); AssertScreen("lobby");

            var systemFrames = new List<Texture2D>();
            string[] methods = { "OpenSettings", "OpenInbox", "OpenSignIn", "OpenAchievements" };
            string[] modals = { "SettingsModal", "InboxModal", "SignInModal", "AchievementModal" };
            for (int i = 0; i < methods.Length; i++)
            {
                Call(app, methods[i]); yield return Settle();
                Transform modal = Find(app.transform, modals[i]);
                systemFrames.Add(RenderFrame()); AssertVisualSemantics();
                // Only Close/Done. Never Reset, consent-document Primary or legal acceptance.
                Click(modal, "Close"); yield return Settle();
                Assert.That(Find(app.transform, modals[i], false), Is.Null);
                AssertScreen("lobby");
            }
            SaveBoard("04-system-modals-2x2", systemFrames, modals, 2);

            Show("team"); yield return Settle(); Save("05-team", "team");
            Show("members"); yield return Settle(); Save("06-members", "members");
            int owned = Enumerable.Range(0, GameModel.Members.Length).First(model.IsUnlocked);
            Call(app, "OpenMember", owned); yield return Settle();
            for (int tab = 0; tab < 3; tab++)
            {
                Click(Find(app.transform, "MemberModal"), "DetailTab-" + tab); yield return Settle();
                Assert.That(Get<int>(app, "memberDetailTab"), Is.EqualTo(tab));
                Save((7 + tab).ToString("00") + "-member-detail-" + tab, "member-detail-tab-" + tab);
            }
            Click(Find(app.transform, "MemberModal"), "CloseTop"); yield return Settle(); AssertScreen("members");
            int locked = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.IsUnlocked(i));
            Call(app, "OpenMember", locked); yield return Settle();
            Transform lockedModal = Find(app.transform, "MemberModal");
            Assert.That(lockedModal.GetComponentsInChildren<Button>().Where(x => x.name == "Train").All(x => !x.IsInteractable()), Is.True);
            Save("10-member-locked", "member-locked");
            Click(lockedModal, "CloseTop"); yield return Settle(); AssertScreen("members");

            Show("accessory"); yield return Settle();
            var inventoryFrames = new List<Texture2D> { RenderFrame() };
            Assert.That(model.Save.OwnedAccessories.Count, Is.GreaterThan(0));
            int item = model.Save.OwnedAccessories[0];
            int targetMember = model.Save.UnlockedMembers.First(i => i != model.AccessoryOwner(item));
            Call(app, "OpenAccessoryCard", item, -1, null); yield return Settle();
            inventoryFrames.Add(RenderFrame());
            Click(Find(app.transform, "AccessoryCard"), "CardRight"); yield return Settle();
            Transform picker = Find(app.transform, "AccessoryMemberPicker");
            inventoryFrames.Add(RenderFrame()); AssertVisualSemantics();
            Click(picker, "Member-" + GameModel.Members[targetMember].Id); yield return Settle();
            Assert.That(model.AccessoryOwner(item), Is.EqualTo(targetMember), "The real equip callback must choose the requested member.");
            Assert.That(Find(app.transform, "AccessoryMemberPicker", false), Is.Null);
            SaveBoard("11-inventory-detail-equip", inventoryFrames, new[] { "inventory", "item-detail", "equip-member-picker" }, 3);

            Show("audition"); yield return Settle();
            var audition = RequireComponent<GachaPanel>(); int candidate = audition.InterviewCandidateIndex;
            Click(audition.transform, "NextCandidate"); yield return Settle();
            Assert.That(audition.InterviewCandidateIndex, Is.Not.EqualTo(candidate));
            Click(audition.transform, "PreviousCandidate"); yield return Settle();
            Assert.That(audition.InterviewCandidateIndex, Is.EqualTo(candidate));
            float toastDeadline = Time.realtimeSinceStartup + 5f;
            while (Find(app.transform, "Toast", false) != null && Time.realtimeSinceStartup < toastDeadline)
                yield return null;
            Save("12-audition", "audition-carousel-returned");

            Show("lobby"); Call(app, "OpenLevelMap"); yield return Settle();
            var map = RequireComponent<LevelMapPanel>();
            var mapFrames = new List<Texture2D> { RenderFrame() };
            string[] mapMethods = { "OpenDropPreview", "OpenChapterRewards", "OpenChapterTasks" };
            string[] mapModals = { "StageLootModal", "ChapterRewardsModal", "ChapterTasksModal" };
            for (int i = 0; i < mapMethods.Length; i++)
            {
                Call(map, mapMethods[i]); yield return Settle();
                Transform modal = Find(map.transform, mapModals[i]); mapFrames.Add(RenderFrame()); AssertVisualSemantics();
                Click(modal, "CloseChapterModal"); yield return Settle();
                Assert.That(Find(map.transform, mapModals[i], false), Is.Null);
            }
            SaveBoard("13-map-submodals-2x2", mapFrames,
                new[] { "map", "stage-loot", "chapter-rewards", "chapter-tasks" }, 2);
            Click(map.transform, "Back"); yield return Settle(); AssertGone<LevelMapPanel>(); AssertScreen("lobby");

            // Test an explicitly selected member, not the fixed scene cast or automatic captain selection.
            int trainee = model.Save.UnlockedMembers.Last(); int beforeLevel = model.LevelOf(trainee);
            int beforeGold = model.Save.Gold; int[] beforeTeam = model.Save.Team.ToArray();
            Call(app, "OpenTrainingPractice", trainee, -1); yield return Settle();
            var practice = RequireComponent<MemberPracticePanel>();
            var phone = practice.GetComponentInChildren<SirenPracticeScene>();
            Assert.That(phone, Is.Not.Null); Assert.That(phone.HasCompleteCast, Is.True);
            foreach (string id in new[] { "xingli", "feiyin", "wubai", "yeying" })
            {
                var image = Find(phone.transform, "Chibi-" + id).GetComponent<Image>();
                Assert.That(image.sprite, Is.EqualTo(Resources.Load<Sprite>(SirenPracticeScene.ArtRoot + "chibi-" + id)));
                Assert.That(image.sprite, Is.Not.Null); Assert.That(image.preserveAspect, Is.True);
            }
            Save("14-practice-phone", "practice-four-fixed-chibis-selected-member");
            Assert.That(model.CanTrain(trainee, out int trainingCost, out string reason), Is.True, reason);
            Click(practice.transform, "StartPractice");
            float practiceDeadline = Time.realtimeSinceStartup + 6f;
            while (practice.IsAnimating && Time.realtimeSinceStartup < practiceDeadline) yield return null;
            Assert.That(practice.IsAnimating, Is.False);
            Assert.That(model.LevelOf(trainee), Is.EqualTo(beforeLevel + 1));
            Assert.That(model.Save.Gold, Is.EqualTo(beforeGold - trainingCost));
            Assert.That(model.Save.Team, Is.EqualTo(beforeTeam));
            Save("15-practice-result", "practice-real-training-result");
            Click(practice.transform, "PracticeDone"); yield return Settle(); AssertGone<MemberPracticePanel>();
            Click(Find(app.transform, "MemberModal"), "CloseTop"); yield return Settle();

            Show("lobby"); Call(app, "OpenLevelMap"); yield return Settle(); map = RequireComponent<LevelMapPanel>();
            Call(map, "OpenStoryChapter01"); yield return Settle();
            var story = RequireComponent<StoryPanel>();
            var storyReading = Find(story.transform, "DialogueBox").GetComponent<RectTransform>();
            Assert.That(storyReading.anchorMin.y, Is.Zero, "最新剧情设计将对话置于右下");
            Assert.That(storyReading.anchoredPosition.x, Is.GreaterThan(250f));
            Assert.That(Find(story.transform, "DialogueText").GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(26));
            Save("16-story-lower-reading", "story-speaker-left-reading-lower-right");
            Click(story.transform, "History"); yield return Settle();
            Assert.That(Find(story.transform, "HistoryPanel"), Is.Not.Null);
            Click(story.transform, "HistoryClose"); yield return Settle();
            Click(story.transform, "Back"); yield return Settle(); AssertGone<StoryPanel>();
            Assert.That(map.gameObject.activeInHierarchy, Is.True); Click(map.transform, "Back"); yield return Settle();

            // Real public battle construction; no manual Outcome/HP edits and no fake winner.
            Call(app, "OpenLevelMap"); yield return Settle(); map = RequireComponent<LevelMapPanel>();
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 20261013UL, out string battleMessage);
            Assert.That(battle, Is.Not.Null, battleMessage);
            bool returned = false;
            var tactics = TacticsBattlePanel.Open(Get<Transform>(app, "safeRoot"), model, battle,
                sim => { model.SettleStageBattle(sim, out _); report.settlementCallbacks++; },
                back: () => { returned = true; }, message: _ => { });
            float battleDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < battleDeadline &&
                (battle.ElapsedMilliseconds == 0 || tactics.GetComponentInChildren<DiceComboShow>()?.IsPlaying == true))
                yield return null;
            Assert.That(battle.ElapsedMilliseconds, Is.GreaterThan(0), "The opening presentation must finish before live battle acceptance.");
            yield return Settle();
            foreach (object cell in Get<IEnumerable>(tactics, "cells"))
            {
                BattleUnit unit = (BattleUnit)cell.GetType().GetField("Unit").GetValue(cell);
                if (unit == null || unit.Side != BattleSide.Player) continue;
                var image = (Image)cell.GetType().GetField("Portrait").GetValue(cell);
                var member = GameModel.Members.First(x => x.Id == unit.Definition.Id);
                Assert.That(image.sprite, Is.EqualTo(Resources.Load<Sprite>(member.ResourcePath)),
                    unit.Definition.Name + " must retain its catalog portrait during live battle.");
                report.checks.Add("Battle portrait: " + unit.Definition.Name + " / " + unit.Definition.Id +
                    " / cell " + unit.Row + "," + unit.Col + " / sprite " + image.sprite.name);
            }
            Save("17-battle-live", "battle-real-first-stage");
            Click(tactics.transform, "PauseToggle"); yield return Settle();
            Assert.That(tactics.IsPaused, Is.True);
            int pausedAt = battle.ElapsedMilliseconds; yield return new WaitForSecondsRealtime(.15f);
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(pausedAt));
            Save("18-battle-paused", "battle-pause-freezes-clock");
            Click(tactics.transform, "BattleRulesEntry"); yield return Settle();
            var rulesFrames = new List<Texture2D> { RenderFrame() };
            Assert.That(Find(tactics.transform, "RulesOverlay"), Is.Not.Null); AssertVisualSemantics();
            Click(tactics.transform, "RulesBack"); Click(tactics.transform, "ResumeBattle"); yield return Settle();
            Assert.That(tactics.IsPaused, Is.False);
            Call(tactics, "RefreshRealtimeCommands"); Call(tactics, "OpenAbilityDetails"); yield return Settle();
            Assert.That(Find(tactics.transform, "AbilityOverlay"), Is.Not.Null);
            rulesFrames.Add(RenderFrame()); AssertVisualSemantics();
            Click(tactics.transform, "AbilityClose"); yield return Settle();
            SaveBoard("19-battle-rules-abilities", rulesFrames, new[] { "battle-rules", "member-abilities" }, 2);

            // Stop only this fixture panel's coroutine driver, advance its real simulator naturally,
            // then ask the normal presenter to settle/render the actual outcome. The seed may win or lose.
            if (!tactics.AutoMode) Click(tactics.transform, "AutoToggle");
            Assert.That(tactics.AutoMode, Is.True);
            tactics.StopAllCoroutines(); tactics.enabled = false;
            int budget = battle.TimeLimitMilliseconds + 2000;
            for (int elapsed = 0; battle.Outcome == BattleOutcome.Ongoing && elapsed <= budget; elapsed += 100)
            {
                battle.AdvanceRealtime(100, true);
                if (elapsed % 1000 == 0) yield return null;
            }
            Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.Ongoing), "Natural simulator advancement must reach a real result.");
            Call(tactics, "ShowResult"); yield return Settle();
            Assert.That(Find(tactics.transform, "BattleResult"), Is.Not.Null);
            Assert.That(report.settlementCallbacks, Is.EqualTo(1));
            report.outcome = battle.Outcome.ToString(); Save("20-battle-real-result", "battle-" + report.outcome.ToLowerInvariant());
            Click(tactics.transform, "ResultContinue"); yield return Settle(); AssertGone<TacticsBattlePanel>();
            Assert.That(returned, Is.True); Assert.That(map.gameObject.activeInHierarchy, Is.True);
            Click(map.transform, "Back"); yield return Settle(); AssertScreen("lobby");
            Assert.That(report.captures.Count, Is.EqualTo(20));
            report.complete = true;
            report.checks.Add("All reviewed routes closed/returned; 20 native-cell PNGs; actual battle outcome=" + report.outcome);
        }

        private void BackupString(string key)
        { if (PlayerPrefs.HasKey(key)) existingKeys.Add(key); strings[key] = PlayerPrefs.GetString(key); }
        private void BackupInt(string key)
        { if (PlayerPrefs.HasKey(key)) existingKeys.Add(key); ints[key] = PlayerPrefs.GetInt(key); }
        private void RestoreCanvas()
        {
            if (canvas == null) return;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldPlane; canvas.scaleFactor = oldScale;
            if (scaler != null) scaler.enabled = oldScalerEnabled;
        }
        private IEnumerator Settle()
        { yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
        private void Show(string page) { Call(app, "ShowScreen", page); AssertScreen(page); }
        private void AssertScreen(string page) { Assert.That(Get<string>(app, "currentScreen"), Is.EqualTo(page)); }
        private static T Get<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, Private);
            Assert.That(field, Is.Not.Null, "Missing test seam field " + name);
            return (T)field.GetValue(owner);
        }
        private static void Call(object owner, string name, params object[] args)
        {
            MethodInfo method = owner.GetType().GetMethods(Private).SingleOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null, "Missing test seam method " + name);
            method.Invoke(owner, args);
        }
        private static T RequireComponent<T>() where T : Component
        {
            T value = Object.FindAnyObjectByType<T>(); Assert.That(value, Is.Not.Null, typeof(T).Name + " must be active"); return value;
        }
        private static void AssertGone<T>() where T : Component
        { Assert.That(Object.FindAnyObjectByType<T>(), Is.Null, typeof(T).Name + " must close"); }
        private static Transform Find(Transform parent, string name, bool required = true)
        {
            Transform found = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
            if (required) Assert.That(found, Is.Not.Null, "Missing active node " + name);
            return found;
        }
        private static void Click(Transform parent, string name)
        {
            Assert.That(new[] { "Reset", "ConsentAccept", "Accept", "Agree", "SignConfirm" }, Does.Not.Contain(name));
            Button button = Find(parent, name).GetComponent<Button>();
            Assert.That(button, Is.Not.Null); Assert.That(button.IsInteractable(), Is.True, name + " must be usable");
            button.onClick.Invoke();
        }
        private void AssertVisualSemantics()
        {
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                string name = graphic.name;
                bool decor = name == "SirenButtonFace" || name.StartsWith("SirenTitlePlate-") ||
                    name == "DialoguePaperTear" || name.Contains("GlassRecord") || name.StartsWith("Chibi-");
                if (decor) Assert.That(graphic.raycastTarget, Is.False, name + " decoration must never intercept input");
                if (!name.StartsWith("SirenTitlePlate-")) continue;
                var binding = graphic.transform.parent.GetComponentsInChildren<SirenStickerTitleBinding>()
                    .SingleOrDefault(x => x.Plate == graphic);
                Transform title = binding != null ? binding.transform : null;
                Assert.That(title, Is.Not.Null); Assert.That(graphic.transform.GetSiblingIndex(), Is.LessThan(title.GetSiblingIndex()),
                    name + " must be behind its real text, not placed over it");
                Assert.That(title.GetComponent<Text>().color.a, Is.GreaterThan(.5f));
            }
            foreach (Text body in canvas.GetComponentsInChildren<Text>())
            {
                if (body.name != "RulesBody" && body.name != "AbilityBody" && body.name != "DialogueText") continue;
                if (body.preferredHeight <= body.rectTransform.rect.height + 2f) continue;
                Assert.That(body.GetComponentInParent<ScrollRect>(), Is.Not.Null,
                    body.name + " overflows its reading rect and needs a scrollable parent");
            }
            foreach (ScrollRect scroll in canvas.GetComponentsInChildren<ScrollRect>())
            {
                Assert.That(scroll.content, Is.Not.Null); Assert.That(scroll.viewport, Is.Not.Null);
                Assert.That(scroll.viewport.rect.width, Is.GreaterThan(0)); Assert.That(scroll.viewport.rect.height, Is.GreaterThan(0));
                Assert.That(scroll.viewport.GetComponentsInChildren<Graphic>(true).Any(x => x.raycastTarget) ||
                    scroll.GetComponents<Graphic>().Any(x => x.raycastTarget), Is.True, scroll.name + " needs a reachable scrolling surface");
                if (!scroll.vertical || scroll.content.rect.height <= scroll.viewport.rect.height + 2f) continue;
                float old = scroll.verticalNormalizedPosition;
                try
                {
                    scroll.verticalNormalizedPosition = 1f; Vector2 top = scroll.content.anchoredPosition;
                    scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases();
                    Assert.That(Vector2.Distance(top, scroll.content.anchoredPosition), Is.GreaterThan(1f), scroll.name + " long body must reach its end");
                }
                finally { scroll.verticalNormalizedPosition = old; Canvas.ForceUpdateCanvases(); }
            }
        }

        private Texture2D RenderFrame()
        {
            AssertVisualSemantics();
            RenderTexture previous = RenderTexture.active;
            RenderMode mode = canvas.renderMode; Camera world = canvas.worldCamera;
            float plane = canvas.planeDistance, scale = canvas.scaleFactor;
            bool scalerEnabled = scaler != null && scaler.enabled;
            var home = app.GetComponentsInChildren<PsdHomeMotion>(true).FirstOrDefault(x => x.gameObject.activeInHierarchy);
            RectTransform homeRect = home != null ? home.GetComponent<RectTransform>() : null;
            Vector3 homeScale = homeRect != null ? homeRect.localScale : Vector3.one;
            Texture2D image = null;
            try
            {
                if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = 10f; canvas.scaleFactor = 1f;
                if (homeRect != null) homeRect.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                temporary.Add(image); return image;
            }
            catch { if (image != null) Object.DestroyImmediate(image); throw; }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = mode; canvas.worldCamera = world; canvas.planeDistance = plane; canvas.scaleFactor = scale;
                if (scaler != null) scaler.enabled = scalerEnabled;
                if (homeRect != null) homeRect.localScale = homeScale;
            }
        }
        private void Save(string name, string state)
        { SaveBoard(name, new List<Texture2D> { RenderFrame() }, new[] { state }, 1); }
        private void SaveBoard(string name, List<Texture2D> cells, string[] labels, int columns)
        {
            Assert.That(cells.Count, Is.EqualTo(labels.Length));
            int rows = Mathf.CeilToInt(cells.Count / (float)columns);
            var board = new Texture2D(columns * Width, rows * Height, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < cells.Count; i++)
                    board.SetPixels((i % columns) * Width, (rows - 1 - i / columns) * Height, Width, Height, cells[i].GetPixels());
                board.Apply(); string file = name + ".png";
                File.WriteAllBytes(Path.Combine(output, file), board.EncodeToPNG());
                report.captures.Add(new Capture { file = file, cells = labels, columns = columns });
                Debug.Log("CHO_UI_REVIEW_CAPTURE " + file + " cells=" + string.Join(",", labels));
            }
            finally
            {
                Object.DestroyImmediate(board);
                foreach (Texture2D image in cells) { temporary.Remove(image); if (image != null) Object.DestroyImmediate(image); }
            }
        }
    }
}

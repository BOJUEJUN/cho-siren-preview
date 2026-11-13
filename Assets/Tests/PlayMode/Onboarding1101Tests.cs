using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Dice;
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
    /// 2026-11-01 (用户: "刚刚进游戏的新手介绍文字太多了，用最少的文字方案"): the first fight teaches by doing — one line + a hand
    /// when the first reroll is ready (pick dice, press 重投) and when the first card is full (tap her), one line under
    /// the first ultimate's rings. Old saves never see it; tapping the dim always moves on. Every step is captured
    /// into Builds/qa-logs/zj-1101b/onboarding/ at phone (1080×2288) and Mac-window (1080×2160) proportions.
    /// </summary>
    public sealed class Onboarding1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101b/onboarding"));

        private bool tutorial, turnBased, force;
        private int swipeLaunches;
        private bool hadSwipeKey;

        [SetUp]
        public void SetUp()
        {
            tutorial = TacticsBattlePanel.FirstBattleTutorialEnabled; turnBased = TacticsBattlePanel.UseTurnBased;
            force = TacticsBattlePanel.ForceOnboarding1101;
            TacticsBattlePanel.FirstBattleTutorialEnabled = true; TacticsBattlePanel.UseTurnBased = true;
            TacticsBattlePanel.ForceOnboarding1101 = true;
            hadSwipeKey = PlayerPrefs.HasKey("cho-siren.swipe-launches");
            swipeLaunches = PlayerPrefs.GetInt("cho-siren.swipe-launches", 0);
        }

        [TearDown]
        public void TearDown()
        {
            TacticsBattlePanel.FirstBattleTutorialEnabled = tutorial; TacticsBattlePanel.UseTurnBased = turnBased;
            TacticsBattlePanel.ForceOnboarding1101 = force;
            if (hadSwipeKey) PlayerPrefs.SetInt("cho-siren.swipe-launches", swipeLaunches); else PlayerPrefs.DeleteKey("cho-siren.swipe-launches");
        }

        [Test]
        public void EveryCoachLineIsOneShortInstruction()
        {
            foreach (string line in new[] { TacticsBattlePanel.DicePickLine1101, TacticsBattlePanel.RerollLine1101,
                         TacticsBattlePanel.UltLine1101, ChoSirenApp.LobbyCoachLine1101 })
            {
                Assert.That(line.Length, Is.LessThanOrEqualTo(12), line);
                Assert.That(line, Does.Not.Contain("\n"), line);
            }
            Assert.That(TacticsBattlePanel.DicePickLine1101, Does.Contain("2"), "选骰子每次最多 2 颗");
            Assert.That(TacticsBattlePanel.RerollLine1101, Does.Contain("不选").And.Contain("全部重投"));
            Assert.That(TacticsBattlePanel.BeatLine1101, Does.Contain("PERFECT"));
        }

        [UnityTest]
        public IEnumerator FirstFightCoachesDiceRerollUltimateAndBeat_Phone() => FirstFight(1080, 2288, "phone");

        [UnityTest]
        public IEnumerator FirstFightCoachesDiceRerollUltimateAndBeat_MacWindow() => FirstFight(1080, 2160, "mac");

        private IEnumerator FirstFight(int width, int height, string tag)
        {
            var rig = new Rig(width, height);
            try
            {
                GameModel model = NewModel();
                TacticsBattlePanel panel = rig.OpenBattle(model);
                Assert.That((bool)typeof(TacticsBattlePanel).GetField("autoMode", Private).GetValue(panel), Is.False,
                    "New battles start in manual mode.");
                var toggle = typeof(TacticsBattlePanel).GetMethod("ToggleSpeed", Private);
                toggle.Invoke(panel, null); Assert.That(model.Save.BattleSpeed, Is.EqualTo(2));
                toggle.Invoke(panel, null); Assert.That(model.Save.BattleSpeed, Is.EqualTo(1));
                yield return Until(() => model.HasStoryFlag("guide-battle-1101"), 15f);
                // 2026-11-12: the first fight opens with one picture card (dice / energy order / whose turn); a tap starts.
                yield return Until(() => Find(panel.transform, "BattlePrimer1112") != null, 10f);
                Assert.That(Find(panel.transform, "BattlePrimer1112"), Is.Not.Null, "第一场先给一张图示卡。");
                yield return Wait(.8f);
                rig.Capture($"{tag}-04-battle-primer.png");
                Find(panel.transform, "BattlePrimer1112").GetComponent<Button>().onClick.Invoke();
                yield return Until(() => Find(panel.transform, "BattlePrimer1112") == null, 5f);
                Assert.That(model.HasStoryFlag("guide-battle-1112-primer"), Is.True);
                // Whose turn: the sweep and the coloured edge glow appear once someone acts.
                yield return Until(() => Find(panel.transform, "SideSweep1112") != null && Find(panel.transform, "SideSweep1112").gameObject.activeSelf, 10f);
                yield return Wait(.25f);
                rig.Capture($"{tag}-04b-turn-sweep.png");
                yield return Wait(.5f);
                Assert.That(panel.OnboardingLine1101, Is.Null, "开场不再弹任何说明卡。");
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BattleTutorial1021"), Is.False);
                Assert.That(model.HasStoryFlag("guide-battle-1101"), Is.True);
                rig.Capture($"{tag}-05-battle-open.png");

                // Step 1: the first reroll is ready → pick dice.
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy);
                yield return Until(() => panel.OnboardingLine1101 == TacticsBattlePanel.DicePickLine1101, 30f);
                Assert.That(panel.OnboardingLine1101, Is.EqualTo(TacticsBattlePanel.DicePickLine1101));
                yield return Wait(.5f);
                rig.Capture($"{tag}-06-battle-coach-dice.png");
                CoachMark1101 coach = panel.GetComponentInChildren<CoachMark1101>();
                Rect hole = coach.Hole;
                RectTransform die = Find(panel.transform, "Dice-1");
                Assert.That(Contains(coach, hole, die), Is.True, "骰子在高亮洞里，点得到。");
                die.GetComponent<Button>().onClick.Invoke();

                // Step 2: press 重投 (the lone die rerolls; none picked = all six).
                yield return Until(() => panel.OnboardingLine1101 == TacticsBattlePanel.RerollLine1101, 5f);
                Assert.That(panel.OnboardingLine1101, Is.EqualTo(TacticsBattlePanel.RerollLine1101));
                yield return Wait(.5f);
                rig.Capture($"{tag}-07-battle-coach-reroll.png");
                int revision = panel.Battle.BattleDice.Revision;
                Find(panel.transform, "PsdReroll").GetComponent<Button>().onClick.Invoke();
                yield return Until(() => panel.OnboardingLine1101 == null, 5f);
                Assert.That(panel.Battle.BattleDice.Revision, Is.Not.EqualTo(revision), "教程那一步真的重投了。");
                Assert.That(model.HasStoryFlag("guide-battle-1101-dice"), Is.True);

                // Step 3: the first full card → tap her.
                BattleUnit member = panel.Battle.Units.First(u => u.Side == BattleSide.Player && u.Alive);
                FillEnergy(panel.Battle, member);
                yield return Until(() => panel.OnboardingLine1101 == TacticsBattlePanel.UltLine1101 || panel.Battle.Outcome != BattleOutcome.Ongoing, 30f);
                Assert.That(panel.OnboardingLine1101, Is.EqualTo(TacticsBattlePanel.UltLine1101));
                yield return Wait(.6f);
                rig.Capture($"{tag}-08-battle-coach-ultimate.png");
                BattleUnit ready = panel.Battle.Units.First(u => u.Side == BattleSide.Player && panel.Battle.IsUltimateReady(u));
                typeof(TacticsBattlePanel).GetMethod("CellClicked", Private)
                    .Invoke(panel, new object[] { BattleSide.Player, ready.Row, ready.Col });
                yield return Until(() => panel.OnboardingLine1101 == null, 5f);
                Assert.That(model.HasStoryFlag("guide-battle-1101-ult"), Is.True);

                // Step 4: one line under the first ultimate's rings.
                yield return Until(() => Find(panel.transform, "BeatHint1101") != null, 10f);
                Assert.That(Find(panel.transform, "BeatHint1101"), Is.Not.Null, "第一次大招有节奏提示。");
                yield return Wait(1.6f);
                rig.Capture($"{tag}-09-battle-beat-hint.png");
                yield return Until(() => model.HasStoryFlag("guide-battle-1101-beat"), 15f);
                yield return null;
                Assert.That(Find(panel.transform, "BeatHint1101") == null, Is.True, "提示随大招演出一起退场。");

                // The rules page keeps the full (short) rules.
                typeof(TacticsBattlePanel).GetMethod("OpenBattleQuickGuide", Private).Invoke(panel, null);
                yield return null;
                string rules = Find(panel.transform, "RulesBody").GetComponent<Text>().text;
                // 2026-11-01c 无限骰子: no per-battle cap any more — 骰子豆 instead.
                Assert.That(rules, Does.Contain("每次最多2颗").And.Contain("不选=6颗全部重投").And.Contain("重投不限次数").And.Contain("最多存3颗"));
                rig.Capture($"{tag}-10-battle-rules.png");
                ScrollRect scroll = Find(panel.transform, "RulesScroll").GetComponent<ScrollRect>();
                scroll.verticalNormalizedPosition = 0f;
                yield return null;
                rig.Capture($"{tag}-11-battle-rules-end.png");
            }
            finally { rig.Dispose(); }
        }

        [UnityTest]
        public IEnumerator TappingTheDimAlwaysMovesOn()
        {
            var rig = new Rig(1080, 2288);
            try
            {
                GameModel model = NewModel();
                TacticsBattlePanel panel = rig.OpenBattle(model);
                yield return Until(() => Find(panel.transform, "BattlePrimer1112") != null, 10f);
                yield return Wait(1f); // taps in the card's first 0.4 s are ignored (no accidental skip)
                Find(panel.transform, "BattlePrimer1112")?.GetComponent<Button>().onClick.Invoke(); // the first-fight picture card
                yield return Wait(.6f);
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy);
                yield return Until(() => panel.OnboardingLine1101 == TacticsBattlePanel.DicePickLine1101, 30f);
                yield return Wait(.5f);
                TapDim(panel);
                yield return Until(() => panel.OnboardingLine1101 == TacticsBattlePanel.RerollLine1101, 5f);
                Assert.That(panel.OnboardingLine1101, Is.EqualTo(TacticsBattlePanel.RerollLine1101), "跳过选骰子仍到重投这一步。");
                yield return Wait(.5f);
                TapDim(panel);
                yield return Until(() => panel.OnboardingLine1101 == null, 5f);
                Assert.That(model.HasStoryFlag("guide-battle-1101-dice"), Is.True);
                int round = panel.Battle.TurnRound;
                yield return Until(() => panel.Battle.TurnRound > round || panel.Battle.Outcome != BattleOutcome.Ongoing, 30f);
                Assert.That(panel.Battle.TurnRound > round || panel.Battle.Outcome != BattleOutcome.Ongoing, Is.True, "关掉提示后战斗继续。");
            }
            finally { rig.Dispose(); }
        }

        [UnityTest]
        public IEnumerator SavesThatSawTheOldGuideNeverSeeTheCoach()
        {
            var rig = new Rig(1080, 2288);
            try
            {
                GameModel model = NewModel();
                model.FirstTime("guide-battle-v2");
                TacticsBattlePanel panel = rig.OpenBattle(model);
                yield return Wait(1.2f);
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy);
                FillEnergy(panel.Battle, panel.Battle.Units.First(u => u.Side == BattleSide.Player && u.Alive));
                float until = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < until)
                {
                    Assert.That(panel.OnboardingLine1101, Is.Null, "老存档不再弹教程。");
                    yield return null;
                }
                Assert.That(model.HasStoryFlag("guide-battle-1101"), Is.False);
            }
            finally { rig.Dispose(); }
        }

        // ------------------------------------------------------------------ lobby side

        [UnityTest]
        public IEnumerator LobbySignContractCoachAndMapHand_Phone() => Lobby(1080, 2288, "phone");

        [UnityTest]
        public IEnumerator LobbySignContractCoachAndMapHand_MacWindow() => Lobby(1080, 2160, "mac");

        private IEnumerator Lobby(int width, int height, string tag)
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey), legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0 }));
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            ChoSirenApp app = new GameObject("Onboarding1101App").AddComponent<ChoSirenApp>();
            Camera camera = new GameObject("Onboarding1101LobbyCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24);
            try
            {
                yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
                yield return null; Canvas.ForceUpdateCanvases();
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return Wait(1.2f);

                // 签约: the contract, then the name box it opens by itself.
                bool signed = false;
                typeof(ChoSirenApp).GetMethod("OpenPlayerContract1028", Private)
                    .Invoke(app, new object[] { true, (Action)(() => signed = true) });
                yield return Wait(.25f);
                Text hint = FindIn(canvas.transform, "PlayerContract1028")?.Find("Hint")?.GetComponent<Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.text, Is.EqualTo("点签名栏，写下你的名字"));
                Shot(camera, rt, $"{tag}-01-contract.png");
                yield return Until(() => FindIn(canvas.transform, "NameDialog1028") != null, 3f);
                yield return Wait(.3f);
                Shot(camera, rt, $"{tag}-02-name.png");
                Transform dialog = FindIn(canvas.transform, "NameDialog1028");
                Assert.That(dialog, Is.Not.Null);
                FindIn(dialog, "RandomName").GetComponent<Button>().onClick.Invoke();
                FindIn(dialog, "ConfirmName").GetComponent<Button>().onClick.Invoke();
                yield return Wait(2.2f); // the name is signed stroke by stroke
                Assert.That(hint.text, Is.EqualTo("点「签约」盖章"));
                Shot(camera, rt, $"{tag}-03-contract-signed.png");
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                Assert.That(signed, Is.False, "截图只看合约，不真的签约。");
                yield return Wait(.4f);

                // The lobby: one spotlight on 「开始演出」.
                Assert.That(FindVisible(canvas.transform, "LiveOnStage"), Is.Not.Null, "首页上有可见的「开始演出」");
                app.StartCoroutine((IEnumerator)typeof(ChoSirenApp).GetMethod("LobbyCoach1028", Private).Invoke(app, null));
                yield return Until(() => UnityEngine.Object.FindAnyObjectByType<CoachMark1101>() != null, 5f);
                CoachMark1101 coach = UnityEngine.Object.FindAnyObjectByType<CoachMark1101>();
                Assert.That(coach, Is.Not.Null, "psdOpening=" + typeof(ChoSirenApp).GetField("psdOpening", Private)?.GetValue(app));
                Assert.That(coach.Line, Is.EqualTo(ChoSirenApp.LobbyCoachLine1101));
                yield return Wait(.6f);
                Shot(camera, rt, $"{tag}-04-lobby-coach.png");
                // The tap goes through the hole to 「开始演出」 itself.
                Transform stage = FindVisible(canvas.transform, "LiveOnStage");
                Assert.That(Contains(coach, coach.Hole, (RectTransform)stage), Is.True);
                var press = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                Button stageButton = stage.GetComponent<Button>();
                if (stageButton != null) stageButton.onClick.Invoke();
                else ExecuteEvents.Execute(stage.gameObject, press, ExecuteEvents.pointerClickHandler);
                yield return Until(() => UnityEngine.Object.FindAnyObjectByType<CoachMark1101>() == null, 2f);
                Assert.That(UnityEngine.Object.FindAnyObjectByType<CoachMark1101>(), Is.Null, "一点「开始演出」提示就收起，不盖过转场视频。");
                yield return Until(() => UnityEngine.Object.FindAnyObjectByType<LevelMapPanel>() != null, 15f);
                Assert.That(UnityEngine.Object.FindAnyObjectByType<LevelMapPanel>(), Is.Not.Null);
                yield return Until(() => GameObject.Find("StartChallengeHand1101") != null, 12f);
                Assert.That(GameObject.Find("StartChallengeHand1101"), Is.Not.Null, "地图上一只手点着「开始挑战」，不加字。");
                yield return Wait(1.0f);
                Shot(camera, rt, $"{tag}-05a-map-hand.png");
            }
            finally
            {
                UnityEngine.Object.Destroy(app.gameObject);
                UnityEngine.Object.DestroyImmediate(camera.gameObject); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Rig : IDisposable
        {
            public readonly GameObject Host;
            public readonly Camera Camera;
            public readonly RenderTexture Texture;

            public Rig(int width, int height)
            {
                Host = new GameObject("Onboarding1101", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Camera = new GameObject("Onboarding1101Camera", typeof(Camera)).GetComponent<Camera>();
                Texture = new RenderTexture(width, height, 24);
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                Camera.orthographic = true; Camera.targetTexture = Texture;
                var canvas = Host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera; canvas.planeDistance = 1;
                var scaler = Host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 720f * height / width); scaler.matchWidthOrHeight = 0;
                if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                    new GameObject("Onboarding1101Events", typeof(EventSystem)).transform.SetParent(Host.transform, false);
            }

            public TacticsBattlePanel OpenBattle(GameModel model)
            {
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 1101UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                return TacticsBattlePanel.Open(Host.transform, model, battle, null);
            }

            public void Capture(string file) => Shot(Camera, Texture, file);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Host); UnityEngine.Object.DestroyImmediate(Camera.gameObject);
                Texture.Release(); UnityEngine.Object.DestroyImmediate(Texture);
            }
        }

        private static GameModel NewModel() => new GameModel(() => new DateTime(2026, 11, 1, 12, 0, 0), new InMemorySaveStore());

        private static void FillEnergy(BattleSimulator battle, BattleUnit unit)
        {
            var clocks = typeof(BattleSimulator).GetField("performerClocks", Private).GetValue(battle) as IDictionary;
            object clock = clocks[unit.Id];
            clock.GetType().GetField("Energy").SetValue(clock, BattleSimulator.MaxUnitEnergy);
        }

        private static void TapDim(TacticsBattlePanel panel)
        {
            CoachMark1101 coach = panel.GetComponentInChildren<CoachMark1101>();
            Assert.That(coach, Is.Not.Null);
            Transform dim = coach.transform.Find("Dim-0");
            ExecuteEvents.Execute(dim.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        private static bool Contains(CoachMark1101 coach, Rect hole, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 centre = coach.transform.InverseTransformPoint((corners[0] + corners[2]) * .5f);
            return hole.Contains(centre);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Until(Func<bool> condition, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        }

        private static RectTransform Find(Transform root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);

        private static Transform FindIn(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static Transform FindVisible(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(false).FirstOrDefault(t => t.name == name);

        private static void Shot(Camera camera, RenderTexture rt, string file)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, file), tex.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 reward-ui: the shared 获得奖励 presentation in real frames — the popup and the HUD fly-in on a plain
    /// canvas, the app's sign-in / mail / task entries going through it (and not through a text toast), and the victory
    /// page's reward grid with 2 / 4 / 6 rewards staying inside the screen. Captures go to
    /// Builds/qa-logs/zj-1101b/reward-ui/ at the phone (1080×2288) and Mac-window (1080×2160) ratios.
    /// </summary>
    public sealed class RewardGain1101PlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101b/reward-ui"));
        private string save, legacy;
        private bool had, hadLegacy;

        public static IEnumerable<object[]> Screens()
        {
            yield return new object[] { "phone", 1080, 2288 };
            yield return new object[] { "mac", 1080, 2160 };
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        // ------------------------------------------------------------------ behaviour on a plain canvas

        [UnityTest]
        public IEnumerator PopupCollectsIntoTheHudAndRoutineIncomeFliesStraightIn()
        {
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { Diamonds = 300, Gold = 1200, Stamina = 100 }));
            var model = new GameModel();
            var host = new GameObject("RewardGainCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0f;
                Text diamonds = FakePill(host.transform, CurrencyIds.Diamond, "Diamonds", 220f);
                Text gold = FakePill(host.transform, CurrencyIds.Gold, "Gold", 400f);
                model.Changed += () => { diamonds.text = model.Save.Diamonds.ToString("N0"); gold.text = model.Save.Gold.ToString("N0"); };
                diamonds.text = model.Save.Diamonds.ToString("N0"); gold.text = model.Save.Gold.ToString("N0");
                RewardGain1101 presenter = RewardGain1101.Bind(model, host.transform, null);
                Assert.That(RewardGain1101.Bind(model, host.transform, null), Is.SameAs(presenter), "每个存档只绑定一次");
                string before = diamonds.text;

                Assert.That(model.CheckIn(out string message), Is.True);
                Assert.That(RewardGain1101.Absorb(message), Is.True, "同一操作的文字提示并入弹窗");
                yield return null;
                Assert.That(RewardGain1101.LastPresentation.Mode, Is.EqualTo(RewardGain1101.Mode.Full));
                Assert.That(RewardGain1101.LastPresentation.Caption, Is.EqualTo("签到成功 · 连续第 1 天"));
                var popup = UnityEngine.Object.FindAnyObjectByType<RewardPopup1101>();
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.Cards.Count, Is.EqualTo(1));
                Assert.That(diamonds.text, Is.EqualTo(before), "收起前 HUD 先保持旧数字");
                yield return Wait(1.2f);
                popup.Tapped();
                yield return Wait(.3f);
                Assert.That(UnityEngine.Object.FindAnyObjectByType<RewardPopup1101>(), Is.Null, "点击任意处收起");
                yield return Wait(1.6f);
                Assert.That(diamonds.text, Is.EqualTo(model.Save.Diamonds.ToString("N0")), "图标飞入后数字滚到新值");

                string goldBefore = gold.text;
                Assert.That(model.TryClaimTask("daily-login", out message), Is.True);
                RewardGain1101.Absorb(message);
                yield return null;
                Assert.That(RewardGain1101.LastPresentation.Mode, Is.EqualTo(RewardGain1101.Mode.Lite), "单个日常任务走轻量飞入");
                Assert.That(UnityEngine.Object.FindAnyObjectByType<RewardPopup1101>(), Is.Null);
                Assert.That(GameObject.Find(RewardGain1101.LiteName), Is.Not.Null);
                Assert.That(gold.text, Is.EqualTo(goldBefore));
                yield return Wait(1.8f);
                Assert.That(gold.text, Is.EqualTo(model.Save.Gold.ToString("N0")));
                Assert.That(gold.transform.parent.localScale, Is.EqualTo(Vector3.one), "闪光后栏位复原");
            }
            finally { UnityEngine.Object.Destroy(host); }
        }

        private static Text FakePill(Transform canvas, string currency, string valueName, float x)
        {
            var bar = canvas.Find("TopBar") ?? new GameObject("TopBar", typeof(RectTransform)).transform;
            bar.SetParent(canvas, false);
            var pill = new GameObject("Currency-" + currency, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            pill.SetParent(bar, false);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f); pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(x, -20f); pill.sizeDelta = new Vector2(140f, 52f);
            var marker = new GameObject(currency == CurrencyIds.Diamond ? "DiamondIcon" : "GoldIcon", typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(pill, false);
            var value = new GameObject(valueName, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            value.transform.SetParent(pill, false);
            value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return value;
        }

        // ------------------------------------------------------------------ the app's entries

        [UnityTest]
        public IEnumerator AppSignInMailAndTasksUseTheSharedPresentation([ValueSource(nameof(Screens))] object[] screen)
        {
            string tag = (string)screen[0];
            int width = (int)screen[1], height = (int)screen[2];
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, Gold = 5000, Stamina = 120 }));
            var app = new GameObject("RewardGain1101App").AddComponent<ChoSirenApp>();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            var camera = new GameObject("RewardGain1101Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24);
            try
            {
                Portrait(app, camera, rt);
                yield return null; Canvas.ForceUpdateCanvases();
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return Wait(1f);
                var model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                var toast = (GameObject)typeof(ChoSirenApp).GetField("toastObject", Private).GetValue(app);
                toast.SetActive(false);
                int presented = RewardGain1101.PresentedCount;

                // 每日签到: the 7-day grid uses the HUD badges and the claim opens the popup, not the old bottom toast.
                typeof(ChoSirenApp).GetMethod("OpenSignIn", Private).Invoke(app, null);
                yield return Wait(.4f);
                Click("SignInClaim");
                yield return null;
                Assert.That(RewardGain1101.PresentedCount, Is.EqualTo(presented + 1));
                Assert.That(RewardGain1101.LastPresentation.Source, Is.EqualTo(RewardGrant1101.SourceCheckIn));
                Assert.That(RewardGain1101.LastPresentation.Mode, Is.EqualTo(RewardGain1101.Mode.Full));
                Assert.That(toast.activeSelf, Is.False, "签到不再只出一条底部文字");
                var day = GameObject.Find("SignInDay-1");
                Assert.That(day, Is.Not.Null);
                // 2026-11-04: day one pays 星光币 (星钻 is the top-up currency).
                Assert.That(day.transform.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(RewardItemVisuals.HudBadge(CurrencyIds.Gold)));
                yield return Wait(1.3f);
                Capture(camera, rt, $"signin-popup-{tag}.png");
                CollectPopup();
                yield return Wait(.5f);
                Capture(camera, rt, $"signin-collect-{tag}.png");
                yield return Wait(1.4f);

                // 邮件（新手赠送）
                typeof(ChoSirenApp).GetMethod("CloseModal", Private).Invoke(app, null);
                typeof(ChoSirenApp).GetMethod("OpenInbox", Private).Invoke(app, null);
                yield return Wait(.4f);
                Click("MailClaim-welcome-2026");
                yield return null;
                Assert.That(RewardGain1101.LastPresentation.Items.Count, Is.EqualTo(2));
                Assert.That(RewardGain1101.LastPresentation.Mode, Is.EqualTo(RewardGain1101.Mode.Full));
                Assert.That(toast.activeSelf, Is.False);
                yield return Wait(1.3f);
                Capture(camera, rt, $"mail-popup-{tag}.png");
                CollectPopup();
                yield return Wait(1.8f);

                // 任务: one daily task (routine income), then 一键领取 of several.
                typeof(ChoSirenApp).GetMethod("CloseModal", Private).Invoke(app, null);
                typeof(ChoSirenApp).GetMethod("OpenDailyTasks", Private).Invoke(app, null);
                yield return Wait(.6f);
                int count = RewardGain1101.PresentedCount;
                Click("Claim-daily-login");
                yield return null;
                Assert.That(RewardGain1101.PresentedCount, Is.EqualTo(count + 1), "任务领取走统一奖励组件");
                Assert.That(RewardGain1101.LastPresentation.Source, Is.EqualTo(RewardGrant1101.SourceTask));
                Assert.That(toast.activeSelf, Is.False);
                yield return Wait(.38f);
                Capture(camera, rt, $"task-claim-{tag}.png");
                yield return Wait(1.3f);
                CollectPopup();
                yield return Wait(1.6f);

                for (int i = 0; i < 3; i++) Assert.That(model.Perform(out string performed), Is.True, performed);
                Assert.That(model.TrySpend(CurrencyIds.Stamina, 40), Is.True);
                Assert.That(model.ClaimableTaskCount, Is.GreaterThanOrEqualTo(2));
                yield return Wait(1.8f);
                CollectPopup();
                yield return Wait(.6f);
                var board = UnityEngine.Object.FindAnyObjectByType<TaskBoardPanel>();
                Assert.That(board, Is.Not.Null);
                typeof(TaskBoardPanel).GetMethod("ClaimAll", Private).Invoke(board, null);
                yield return null;
                Assert.That(RewardGain1101.LastPresentation.Mode, Is.EqualTo(RewardGain1101.Mode.Full), "一键领取多项合成一次弹窗");
                Assert.That(RewardGain1101.LastPresentation.Batches, Is.GreaterThanOrEqualTo(2));
                Assert.That(RewardGain1101.LastPresentation.Caption, Does.Contain("一键领取"));
                yield return Wait(1.3f);
                Capture(camera, rt, $"task-claimall-{tag}.png");
                CollectPopup();
                yield return Wait(1f);
            }
            finally
            {
                UnityEngine.Object.Destroy(app.gameObject);
                UnityEngine.Object.DestroyImmediate(camera.gameObject); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void CollectPopup()
        {
            var popup = UnityEngine.Object.FindAnyObjectByType<RewardPopup1101>();
            if (popup == null) return;
            popup.Tapped();
            if (UnityEngine.Object.FindAnyObjectByType<RewardPopup1101>() == popup) popup.Tapped();
        }

        private static void Click(string name)
        {
            GameObject target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, name);
            Button button = UnityNull.Real(target.GetComponent<Button>()) ?? target.GetComponentInChildren<Button>();
            Assert.That(button, Is.Not.Null, name);
            button.onClick.Invoke();
        }

        private static void Portrait(ChoSirenApp app, Camera camera, RenderTexture rt)
        {
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.targetTexture = rt;
            var canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
        }

        // ------------------------------------------------------------------ victory page

        [UnityTest]
        public IEnumerator VictoryRewardGridStaysInsideTheScreenFor2To6Rewards([ValueSource(nameof(Screens))] object[] screen)
        {
            string tag = (string)screen[0];
            int width = (int)screen[1], height = (int)screen[2];
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            var host = new GameObject("VictoryRewardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("VictoryRewardCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                yield return null;
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 713UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                TacticsBattlePanel panel = TacticsBattlePanel.Open(host.transform, model, battle, b => model.SettleStageBattle(b, out _));
                panel.StopAllCoroutines();
                foreach (BattleUnit unit in battle.Units)
                    if (unit.Side == BattleSide.Enemy) unit.Hp = 1; else unit.BaseAttack = 10000;
                battle.AutoPlay();
                if (battle.Outcome != BattleOutcome.Victory)
                    typeof(BattleSimulator).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.Invoke(battle, new object[] { BattleOutcome.Victory });
                Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
                typeof(TacticsBattlePanel).GetMethod("ShowResult", Private).Invoke(panel, null);
                yield return Wait(2.4f);
                Capture(camera, rt, $"victory-real-{tag}.png");

                var rewards = (List<(string ItemId, int Amount)>)typeof(GameModel).GetField("lastBattleRewards", Private).GetValue(model);
                foreach (int count in new[] { 2, 4, 6 })
                {
                    rewards.Clear();
                    rewards.AddRange(Loot(count));
                    typeof(TacticsBattlePanel).GetMethod("ApplyResultB1025", Private).Invoke(panel, new object[] { true, 3 });
                    yield return Wait(2.4f);
                    var view = (BattleResultB1025)typeof(TacticsBattlePanel).GetField("resultB1025", Private).GetValue(panel);
                    AssertLayout(view, count);
                    Capture(camera, rt, $"victory-{count}-{tag}.png");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(camera.gameObject); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static List<(string ItemId, int Amount)> Loot(int count)
        {
            var list = new List<(string ItemId, int Amount)> { (CurrencyIds.Gold, 304), (CurrencyIds.Diamond, 5) };
            foreach (var rarity in new[] { GameModel.AccessoryRarity.Epic, GameModel.AccessoryRarity.Rare,
                         GameModel.AccessoryRarity.Legendary, GameModel.AccessoryRarity.Fine })
            {
                if (list.Count >= count) break;
                int index = Enumerable.Range(12, Math.Max(0, GameModel.AccessoryItemIds.Length - 12))
                    .FirstOrDefault(i => GameModel.AccessoryRarityOf(i) == rarity && list.All(l => l.ItemId != GameModel.AccessoryItemIds[i]));
                list.Add((GameModel.AccessoryItemIds[Math.Max(index, list.Count)], 1));
            }
            return list.Take(count).ToList();
        }

        private static void AssertLayout(BattleResultB1025 view, int count)
        {
            var root = (RectTransform)view.transform;
            Rect screen = root.rect;
            var cards = root.Cast<Transform>().Where(t => t.name.StartsWith("Loot-", StringComparison.Ordinal) && t.name != "LootHeader" && t.name != "LootRule")
                .Select(t => (RectTransform)t).ToList();
            Assert.That(cards.Count, Is.EqualTo(count), "全部奖励都显示，不再只取前四个");
            var boxes = cards.Select(c => LocalBox(root, c)).ToList();
            float margin = screen.width * .04f;
            foreach (Rect box in boxes)
            {
                Assert.That(box.xMin, Is.GreaterThanOrEqualTo(screen.xMin + margin), "奖励卡左侧留边距");
                Assert.That(box.xMax, Is.LessThanOrEqualTo(screen.xMax - margin), "奖励卡右侧留边距");
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Rect a = boxes[i], b = boxes[j];
                    Rect shrunk = new Rect(a.x + 6f, a.y + 6f, a.width - 12f, a.height - 12f);
                    Assert.That(shrunk.Overlaps(b), Is.False, $"奖励卡 {cards[i].name} 与 {cards[j].name} 重叠");
                }
            Rect slot = view.ContinueSlot;
            float continueTop = screen.yMax - slot.y;   // ContinueSlot is measured from the top
            foreach (Rect box in boxes)
                Assert.That(box.yMin, Is.GreaterThan(continueTop), "奖励卡在继续按钮之上");
            Assert.That(boxes.Select(b => Mathf.Round(b.center.y / 4f)).Distinct().Count(), Is.EqualTo(count <= 4 ? 1 : 2),
                "四个以内一行，更多时自动换成两行");
            foreach (Transform bar in root.Cast<Transform>().Where(t => t.name.StartsWith("Bar-", StringComparison.Ordinal)))
            {
                Rect b = LocalBox(root, (RectTransform)bar);
                Assert.That(b.xMax, Is.LessThanOrEqualTo(screen.xMax), bar.name + " 不出右边界");
                foreach (Rect box in boxes) Assert.That(b.yMin, Is.GreaterThan(box.yMax), "MVP 列表在奖励区之上");
            }
        }

        private static Rect LocalBox(RectTransform root, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 c in corners)
            {
                Vector2 p = root.InverseTransformPoint(c);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void Capture(Camera camera, RenderTexture rt, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), tex.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}

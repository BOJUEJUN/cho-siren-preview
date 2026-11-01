using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using ChoSiren.Panels;
using ChoSiren.Systems.Dice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class BattlePsd928Tests
    {
        const string Out = "/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/battle-psd-align-20261019/runtime";
        static void Call(TacticsBattlePanel panel, string name) => typeof(TacticsBattlePanel)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        static T Get<T>(TacticsBattlePanel panel, string name) => (T)typeof(TacticsBattlePanel)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

        [UnityTest]
        public IEnumerator NativeBattleRendersAndAllSixDiceStayInteractive()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("PsdBattleTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Camera camera = new GameObject("PsdCaptureCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            try
            {
                // A reversible isolated save is used only to construct a real first-stage battle.
                PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-1", 928UL, out var message);
                Assert.That(battle, Is.Not.Null, message);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                typeof(TacticsBattlePanel).GetField("awaitingInput", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
                float landingDeadline = Time.realtimeSinceStartup + 20f;
                var initialRolls = panel.GetComponentsInChildren<DiceRollPresentation>(true);
                while (initialRolls.Any(roll => roll.Busy) && Time.realtimeSinceStartup < landingDeadline)
                    yield return null;
                Assert.That(initialRolls.Any(roll => roll.Busy), Is.False, "Opening dice must finish their real roll");
                yield return null; // Let the panel observe landing before asserting input.
                Call(panel, "RefreshDiceUi"); Call(panel, "RefreshBattleHud");
                Canvas.ForceUpdateCanvases();
                var all = panel.GetComponentsInChildren<Transform>(true);
                Assert.That(all.Count(t => t.name.StartsWith("Dice-") && t.GetComponent<Button>() != null), Is.EqualTo(6));
                Assert.That(battle.BattleDice.Values.Count, Is.EqualTo(6));
                Assert.That(all.First(t => t.name == "BattlePSD928").gameObject.activeInHierarchy, Is.True);
                Assert.That(all.Where(t => t.name.StartsWith("PsdSkillRow-")).All(t => !t.gameObject.activeInHierarchy), Is.True, "Untriggered skill art must not remain on stage.");
                var idleHeart = all.First(t => t.name == "PsdReroll").GetComponent<BattleRerollSlide929>();
                Assert.That(idleHeart.GetComponentsInChildren<CanvasGroup>(true).All(g => g.alpha == 0), Is.True, "No permanent ghost for an unavailable reroll.");
                Capture(camera, rt, "battle-runtime-initial.png");
                var sixth = all.First(t => t.name == "Dice-5").GetComponent<Button>();
                Assert.That(sixth.interactable, Is.True);
                sixth.onClick.Invoke();
                Assert.That(battle.BattleDice.SelectedForRerollCount, Is.EqualTo(1));
                Assert.That(battle.BattleDice.Held[5], Is.False);
                var saved = battle.BattleDice.Values.ToArray();
                battle.BattleDice.GainEnergy(100);
                Call(panel, "RefreshDiceUi");
                yield return new WaitForSecondsRealtime(.35f);
                var heart = all.First(t => t.name == "PsdReroll").GetComponent<Button>();
                Assert.That(heart.interactable, Is.True);
                heart.onClick.Invoke();
                Assert.That(battle.BattleDice.UsedRerolls, Is.EqualTo(1));
                for (int i = 0; i < 5; i++) Assert.That(battle.BattleDice.Values[i], Is.EqualTo(saved[i]));
                Assert.That(battle.BattleDice.Held.All(h => h), Is.True);
                yield return new WaitForSecondsRealtime(1.3f);
                Call(panel, "TogglePause");
                int revision = battle.BattleDice.Revision;
                heart.onClick.Invoke();
                Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
                Assert.That(Get<GameObject>(panel, "pauseOverlay").activeInHierarchy, Is.True);
                Capture(camera, rt, "battle-runtime-pause.png");
                Call(panel, "TogglePause");
                Call(panel, "ToggleSpeed"); Assert.That(panel.Speed, Is.EqualTo(2));
                Call(panel, "ToggleAuto"); Assert.That(panel.AutoMode, Is.True);
                var guard = all.First(t => t.name == "TacticalGuard").GetComponent<Button>();
                Call(panel, "RefreshRealtimeCommands"); guard.onClick.Invoke();
                Assert.That(battle.GuardCooldownRemaining, Is.GreaterThan(0));
                battle.AdvanceRealtime(1000); Call(panel, "RefreshAllCells"); Call(panel, "RefreshBattleHud");
                Capture(camera, rt, "battle-runtime-live.png");
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
        public void DifferentEncounterUsesSeparateEnemyLanes()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("PsdEnemyLanes", typeof(RectTransform));
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-3", 928UL, out var message);
                Assert.That(battle, Is.Not.Null, message);
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                var figures = Get<System.Collections.Generic.Dictionary<int, Image>>(panel, "enemyFigures");
                var alive = battle.Units.Where(u => u.Side == ChoSiren.Systems.Tactics.BattleSide.Enemy && u.Alive).ToArray();
                Assert.That(alive.Length, Is.EqualTo(2));
                // A scout may occupy the left lane when its opponent is not the PSD's wraith.
                // Its source-world x must not force it into the right-hand enemy's lane.
                var authored = panel.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "BattlePSD928");
                float left = authored.InverseTransformPoint(figures[alive[0].Id].rectTransform.position).x;
                float right = authored.InverseTransformPoint(figures[alive[1].Id].rectTransform.position).x;
                Assert.That(Mathf.Abs(right - left), Is.GreaterThan(180));
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }

        private static void Capture(Camera camera, RenderTexture rt, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, filename), tex.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex);
        }
    }
}

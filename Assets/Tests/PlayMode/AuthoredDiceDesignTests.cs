using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class AuthoredDiceDesignTests
    {
        static Sprite[] Faces() => Enumerable.Range(1, 6)
            .Select(n => Resources.Load<Sprite>("Art/Reference038/vfx-die-face-" + n)).ToArray();
        static void Call(TacticsBattlePanel panel, string name) => typeof(TacticsBattlePanel)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
        static T Get<T>(TacticsBattlePanel panel, string name) => (T)typeof(TacticsBattlePanel)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);

        [Test]
        public void SixAuthoredFacesKeepTheirNativeDimensionsAndDistinctIdentities()
        {
            Sprite[] faces = Faces();
            Assert.That(faces.All(s => s != null), Is.True);
            Assert.That(faces.Distinct().Count(), Is.EqualTo(6));
            for (int i = 0; i < faces.Length; i++)
            {
                Assert.That(faces[i].name, Is.EqualTo("vfx-die-face-" + (i + 1)));
                Assert.That(faces[i].rect.size, Is.EqualTo(new Vector2(148, 152)));
            }
        }

        [Test]
        public void RollUsesOnlyAuthoredFacesAndRespectsPauseLatestResultAndReducedMotion()
        {
            var button = new GameObject("AuthoredDiceHitTarget", typeof(RectTransform), typeof(Button));
            var rigObject = new GameObject("Motion", typeof(RectTransform));
            rigObject.transform.SetParent(button.transform, false);
            var artObject = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artObject.transform.SetParent(rigObject.transform, false);
            var art = artObject.GetComponent<Image>();
            var rig = rigObject.GetComponent<RectTransform>();
            bool paused = false, reduced = false;
            int speed = 1;
            try
            {
                var faces = Faces();
                var animation = rigObject.AddComponent<DiceRollPresentation>();
                animation.Configure(art, rig, () => paused, () => speed, () => reduced, renderedRoll: false);
                animation.SetRestingFace(faces[0], 1, true, Color.white);
                animation.PlayRoll(faces[5], 6, faces, 0, true);
                for (int i = 0; i < 25; i++)
                {
                    animation.AdvancePresentation(1f / 30f);
                    Assert.That(faces.Contains(art.sprite), Is.True, "Old rendered crystal frames must never appear.");
                }
                float elapsed = animation.Elapsed;
                Vector2 pose = rig.anchoredPosition;
                paused = true; animation.AdvancePresentation(1f);
                Assert.That(animation.Elapsed, Is.EqualTo(elapsed));
                Assert.That(rig.anchoredPosition, Is.EqualTo(pose));
                paused = false; speed = 2;
                animation.PlayRoll(faces[2], 3, faces, 0, false);
                Assert.That(animation.Elapsed, Is.EqualTo(elapsed), "A new model result updates the target without restarting.");
                for (int i = 0; i < 35 && animation.IsRolling; i++) animation.AdvancePresentation(1f / 30f);
                Assert.That(animation.IsRolling, Is.False);
                Assert.That(animation.FinalValue, Is.EqualTo(3));
                Assert.That(art.sprite, Is.SameAs(faces[2]));
                Assert.That(rig.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(button.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(Vector2.zero));
                reduced = true;
                animation.PlayRoll(faces[4], 5, faces, 0, true);
                Assert.That(animation.IsRolling, Is.False);
                Assert.That(art.sprite, Is.SameAs(faces[4]));
            }
            finally { UnityEngine.Object.DestroyImmediate(button); }
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest]
        public IEnumerator RealBattleKeepsSixSelectableFacesAndOnlyRerollsTheSelectedDie()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey), oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            var host = new GameObject("DiceDesignBattle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("DiceDesignCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(720, 1536, 24);
            try
            {
                PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                var model = new GameModel();
                var battle = model.StartStageBattle("stage-1-1", 20261016UL, out string error);
                Assert.That(battle, Is.Not.Null, error);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536);
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                panel.StopAllCoroutines();
                typeof(TacticsBattlePanel).GetField("awaitingInput", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
                yield return new WaitForSecondsRealtime(1.8f);
                Call(panel, "RefreshDiceUi"); Call(panel, "RefreshBattleHud");
                var faces = Faces();
                Assert.That(Get<Sprite[]>(panel, "userDiceFaceSprites"), Is.EqualTo(faces));
                var buttons = Get<List<GameObject>>(panel, "diceButtons");
                var images = Get<List<Image>>(panel, "diceFaceImages");
                Assert.That(buttons.Count, Is.EqualTo(6));
                Assert.That(buttons.All(b => b.GetComponent<Button>().interactable), Is.True);
                for (int i = 0; i < 6; i++)
                {
                    Assert.That(images[i].sprite, Is.SameAs(faces[battle.BattleDice.Values[i] - 1]));
                }
                int[] original = battle.BattleDice.Values.ToArray();
                buttons[5].GetComponent<Button>().onClick.Invoke();
                Assert.That(battle.BattleDice.SelectedForRerollCount, Is.EqualTo(1));
                battle.BattleDice.GainEnergy(100); Call(panel, "RefreshDiceUi");
                var heart = panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "PsdReroll");
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(heart.interactable, Is.True, "The real expanded control must be clickable before invoking it.");
                heart.onClick.Invoke(); heart.onClick.Invoke();
                Assert.That(battle.BattleDice.UsedRerolls, Is.EqualTo(1));
                for (int i = 0; i < 5; i++) Assert.That(battle.BattleDice.Values[i], Is.EqualTo(original[i]));
                yield return new WaitForSecondsRealtime(1.3f);
                Call(panel, "RefreshDiceUi");
                Assert.That(buttons.All(b => b.GetComponent<Button>().interactable), Is.True);
                Call(panel, "TogglePause");
                int revision = battle.BattleDice.Revision; heart.onClick.Invoke();
                Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
                Call(panel, "TogglePause"); Call(panel, "ToggleSpeed");
                Assert.That(panel.Speed, Is.EqualTo(2));
                Canvas.ForceUpdateCanvases();
                Call(panel, "RefreshRealtimeCommands");
                Text guardCount = Get<Text>(panel, "guardHint");
                Assert.That(guardCount.text, Is.EqualTo("1/1"));
                Assert.That(guardCount.preferredHeight, Is.LessThanOrEqualTo(guardCount.rectTransform.rect.height),
                    "The real Noto count line must fit; a larger font cannot be silently truncated.");
                Assert.That(guardCount.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
                RectTransform guardHit = Get<GameObject>(panel, "guardButton").GetComponent<RectTransform>();
                Assert.That(guardHit.rect.width * 390f / 720f, Is.GreaterThanOrEqualTo(44f));
                Assert.That(guardHit.rect.height * 390f / 720f, Is.GreaterThanOrEqualTo(44f));
                camera.Render();
                string output = Environment.GetEnvironmentVariable("CHO_DICE_CAPTURE_PATH");
                if (!string.IsNullOrEmpty(output))
                {
                    RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
                    var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); capture.Apply();
                    File.WriteAllBytes(output, capture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(capture); RenderTexture.active = previous;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(camera.gameObject);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                PlayerPrefs.Save();
            }
        }
    }
}

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
    /// 2026-10-31 (赵金 hero-card): the acting member's bust swells out of her card (basic &lt; skill &lt; ultimate)
    /// and always springs back. Captures go to Builds/qa-logs/zj-1031/hero-card/ for visual review.
    /// </summary>
    public sealed class HeroCardLunge1031Tests
    {
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1031", "hero-card"));
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        sealed class Fixture : System.IDisposable
        {
            public GameObject Host;
            public Camera Camera;
            public RenderTexture Rt;
            public TacticsBattlePanel Panel;
            public BattleSimulator Battle;
            bool had, oldHad, reduced;
            string before, old;

            public Fixture()
            {
                had = PlayerPrefs.HasKey(GameModel.SaveKey); oldHad = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
                before = PlayerPrefs.GetString(GameModel.SaveKey); old = PlayerPrefs.GetString(GameModel.LegacySaveKey);
                reduced = BattlePresentationOptions.ReduceMotion;
                BattlePresentationOptions.ReduceMotion = false;
                Host = new GameObject("HeroLungeCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Camera = new GameObject("HeroLungeCamera", typeof(Camera)).GetComponent<Camera>();
                Rt = new RenderTexture(720, 1536, 24);
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                Battle = model.StartStageBattle("stage-1-1", 1031UL, out string message);
                Assert.That(Battle, Is.Not.Null, message);
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                Camera.orthographic = true; Camera.targetTexture = Rt;
                var canvas = Host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera; canvas.planeDistance = 1;
                var scaler = Host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                Panel = TacticsBattlePanel.Open(Host.transform, model, Battle, null);
                Panel.StopAllCoroutines();
            }

            public IList Cells => (IList)typeof(TacticsBattlePanel).GetField("cells", Private).GetValue(Panel);

            public object Cell(BattleUnit unit) => typeof(TacticsBattlePanel)
                .GetMethod("FindCell", Private, null, new[] { typeof(BattleUnit) }, null).Invoke(Panel, new object[] { unit });

            public Transform CellRoot(BattleUnit unit) =>
                ((GameObject)Cell(unit).GetType().GetField("Root").GetValue(Cell(unit))).transform;

            public bool Play(BattleUnit unit, string skillId) => (bool)typeof(TacticsBattlePanel)
                .GetMethod("PlayHeroLunge1031", Private).Invoke(Panel, new[] { Cell(unit), unit, skillId });

            public HeroCardLunge1031 Lunge(BattleUnit unit) => CellRoot(unit).GetComponentInChildren<HeroCardLunge1031>(true);

            public void SetField(string name, object value) => typeof(TacticsBattlePanel).GetField(name, Private).SetValue(Panel, value);

            public void Dispose()
            {
                Object.DestroyImmediate(Host); Object.DestroyImmediate(Camera.gameObject); Rt.Release(); Object.DestroyImmediate(Rt);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (oldHad) PlayerPrefs.SetString(GameModel.LegacySaveKey, old); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                BattlePresentationOptions.ReduceMotion = reduced;
                BattleHitStop.Clear();
                PlayerPrefs.Save();
            }
        }

        static IEnumerator Settle(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        static string SkillFor(BattleSimulator battle, BattleUnit unit, HeroCardLunge1031.Tier tier) =>
            tier == HeroCardLunge1031.Tier.Basic ? "rt-basic" : battle.ActiveSkillId(unit, tier == HeroCardLunge1031.Tier.Ultimate);

        [UnityTest]
        public IEnumerator CapturePeakFramesForEveryTier()
        {
            using (var f = new Fixture())
            {
                yield return Settle(3.2f);
                typeof(TacticsBattlePanel).GetMethod("RefreshDiceUi", Private).Invoke(f.Panel, null);
                yield return Settle(.5f);
                BattleHitStop.Clear();
                var members = f.Battle.Units.Where(u => u.Side == BattleSide.Player).ToList();
                Save(f, "hero-card-rest");
                // Second card: neighbours on both sides show whether the bust correctly leans over them.
                BattleUnit actor = members[Mathf.Min(1, members.Count - 1)];
                foreach (HeroCardLunge1031.Tier tier in new[] { HeroCardLunge1031.Tier.Basic, HeroCardLunge1031.Tier.Skill, HeroCardLunge1031.Tier.Ultimate })
                {
                    Assert.That(f.Play(actor, SkillFor(f.Battle, actor, tier)), Is.True, "PSD cards own the act-out.");
                    HeroCardLunge1031 lunge = f.Lunge(actor);
                    Assert.That(lunge.CurrentTier, Is.EqualTo(tier));
                    float swell = HeroCardLunge1031.SwellSeconds(tier);
                    string key = "hero-card-" + tier.ToString().ToLowerInvariant();
                    // Overshoot (back ease) — the biggest frame of the swell.
                    lunge.Advance(swell * .55f);
                    Save(f, key + "-overshoot");
                    // Held peak.
                    lunge.Advance(swell * .45f + .02f);
                    Save(f, key + "-peak");
                    // Contact jolt, a quarter of the way in.
                    lunge.Impact();
                    lunge.Advance(HeroCardLunge1031.JoltSeconds * .25f);
                    Save(f, key + "-jolt");
                    lunge.Rest();
                    yield return null;
                }
                // Every member at once at full ultimate strength, to judge each card's crop.
                foreach (BattleUnit unit in members)
                {
                    f.Play(unit, SkillFor(f.Battle, unit, HeroCardLunge1031.Tier.Ultimate));
                    f.Lunge(unit).Advance(HeroCardLunge1031.SwellSeconds(HeroCardLunge1031.Tier.Ultimate) + .02f);
                }
                Save(f, "hero-card-all-ultimate-peak");
                foreach (BattleUnit unit in members) f.Lunge(unit).Rest();
                // A mid-return frame (eases back, slight undershoot).
                f.Play(actor, "rt-basic");
                for (int i = 0; i < 25; i++) f.Lunge(actor).Advance(.02f);   // 0.12 swell + 0.30 hold/jolt + 0.08 of the return
                Save(f, "hero-card-basic-return");
                f.Lunge(actor).Rest();
            }
        }

        [UnityTest]
        public IEnumerator LungeOnlyMovesTheBustAndAlwaysReturnsToRest()
        {
            using (var f = new Fixture())
            {
                yield return null;
                BattleHitStop.Clear();
                BattleUnit actor = f.Battle.Units.First(u => u.Side == BattleSide.Player);
                Transform root = f.CellRoot(actor);
                var frame = (RectTransform)root.Find("PsdUnitFrame");
                var hp = (RectTransform)root.GetComponentsInChildren<Transform>(true).First(t => t.name == "UnitHpText");
                Vector3 frameAt = frame.position, hpAt = hp.position, cardScale = root.localScale;

                Assert.That(f.Play(actor, f.Battle.ActiveSkillId(actor, true)), Is.True);
                HeroCardLunge1031 lunge = f.Lunge(actor);
                Assert.That(lunge, Is.Not.Null);
                Image portrait = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "PerformerMotion")
                    .Find("Portrait").GetComponent<Image>();
                Assert.That(portrait.transform.IsChildOf(lunge.transform), Is.True, "The rig must carry the card's bust.");
                lunge.Advance(.25f);
                Assert.That(lunge.transform.localScale.y, Is.GreaterThan(1.2f), "An ultimate must swell the bust clearly.");
                Assert.That(frame.position, Is.EqualTo(frameAt), "The card frame never moves.");
                Assert.That(hp.position, Is.EqualTo(hpAt), "The HP readout never moves.");
                Assert.That(root.localScale, Is.EqualTo(cardScale), "The card itself is not scaled.");
                var clip = lunge.transform.parent.GetComponent<RectMask2D>();
                Assert.That(clip != null && clip.enabled, Is.True, "While acting out, the sleeve-width clip hides the art's side crop edges.");
                Assert.That(((RectTransform)lunge.transform).rect.size.x,
                    Is.EqualTo(((RectTransform)root.GetComponentsInChildren<Transform>(true).First(t => t.name == "PsdPortraitArea")).rect.size.x).Within(.01f),
                    "The rig keeps the portrait area's own rectangle.");

                // Tier strength order at the held peak.
                var peaks = new List<float>();
                foreach (string skill in new[] { "rt-basic", f.Battle.ActiveSkillId(actor, false), f.Battle.ActiveSkillId(actor, true) })
                {
                    lunge.Rest(); f.Play(actor, skill);
                    lunge.Advance(HeroCardLunge1031.SwellSeconds((HeroCardLunge1031.Tier)peaks.Count) + .02f);
                    peaks.Add(lunge.transform.localScale.y);
                }
                Assert.That(peaks[0], Is.GreaterThan(1.08f));
                Assert.That(peaks[1], Is.GreaterThan(peaks[0]));
                Assert.That(peaks[2], Is.GreaterThan(peaks[1]));

                // Finishes on the exact resting pose.
                lunge.Rest(); f.Play(actor, "rt-basic");
                for (int i = 0; i < 40; i++) lunge.Advance(.02f);
                AssertRest(lunge);

                // Rapid repeats never leave it enlarged.
                for (int i = 0; i < 6; i++) { f.Play(actor, i % 2 == 0 ? "rt-basic" : f.Battle.ActiveSkillId(actor, false)); lunge.Advance(.07f); }
                for (int i = 0; i < 60; i++) lunge.Advance(.02f);
                AssertRest(lunge);

                // Pause freezes the pose; it resumes and completes afterwards.
                f.Play(actor, "rt-basic"); lunge.Advance(.1f);
                Vector3 frozen = lunge.transform.localScale;
                f.SetField("paused", true);
                lunge.Advance(.5f);
                Assert.That(lunge.transform.localScale, Is.EqualTo(frozen));
                f.SetField("paused", false);
                for (int i = 0; i < 40; i++) lunge.Advance(.02f);
                AssertRest(lunge);

                // Closing mid-swell snaps home.
                f.Play(actor, "rt-basic"); lunge.Advance(.1f);
                f.SetField("closing", true); lunge.Advance(.02f); AssertRest(lunge);
                f.SetField("closing", false);

                // Defeat mid-swell snaps home.
                f.Play(actor, "rt-basic"); lunge.Advance(.1f);
                int hpBefore = actor.Hp;
                actor.Hp = 0;
                lunge.Advance(.02f); AssertRest(lunge);
                Assert.That(f.Play(actor, "rt-basic") && lunge.IsActive, Is.False, "A fallen member does not act out.");
                actor.Hp = hpBefore;

                // Reduced motion: no swell at all, and switching it on mid-swell snaps home.
                f.Play(actor, "rt-basic"); lunge.Advance(.1f);
                BattlePresentationOptions.ReduceMotion = true;
                lunge.Advance(.02f); AssertRest(lunge);
                f.Play(actor, f.Battle.ActiveSkillId(actor, true)); lunge.Advance(.2f); AssertRest(lunge);
                BattlePresentationOptions.ReduceMotion = false;

                // Disabling the card (wave change / close) restores it.
                f.Play(actor, "rt-basic"); lunge.Advance(.1f);
                lunge.gameObject.SetActive(false); AssertRest(lunge); lunge.gameObject.SetActive(true);
            }
        }

        static void AssertRest(HeroCardLunge1031 lunge)
        {
            var rect = (RectTransform)lunge.transform;
            Assert.That(lunge.IsActive, Is.False);
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rect.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(lunge.transform.parent.GetComponent<RectMask2D>().enabled, Is.False, "Idle cards are never clipped.");
        }

        static void Save(Fixture f, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); f.Camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = f.Rt;
            var tex = new Texture2D(f.Rt.width, f.Rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, f.Rt.width, f.Rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            // Card strip close-up (the four member cards plus the stage just above them), 2x.
            const int x = 0, y = 240, w = 720, h = 460;
            var crop = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, false);
            for (int py = 0; py < h * 2; py++)
                for (int px = 0; px < w * 2; px++)
                    crop.SetPixel(px, py, tex.GetPixel(x + px / 2, y + py / 2));
            crop.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + "-cards.png"), crop.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(tex); Object.DestroyImmediate(crop);
        }
    }
}

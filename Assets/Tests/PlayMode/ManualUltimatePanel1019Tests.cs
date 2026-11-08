using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace ChoSiren.Tests
{
    public sealed class ManualUltimatePanel1019Tests
    {
        GameObject host, events; Camera camera; RenderTexture frame;
        TacticsBattlePanel panel; BattleUnit caster;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            host = new GameObject("ManualUltimatePanelFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            camera = new GameObject("ManualUltimateCamera", typeof(Camera)).GetComponent<Camera>();
            frame = new RenderTexture(720, 1536, 24); camera.targetTexture = frame;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720,1536);
            events = new GameObject("ManualUltimateEvents", typeof(EventSystem));
            var model = new GameModel(() => new DateTime(2026,10,19,12,0,0), new InMemorySaveStore());
            var battle = model.StartStageBattle("stage-1-1", 1019UL, out var message); Assert.That(battle, Is.Not.Null, message);
            panel = TacticsBattlePanel.Open(host.transform, model, battle, null); panel.StopAllCoroutines();
            caster = battle.Units.First(u => u.Side == BattleSide.Player);
            var clocks = (System.Collections.IDictionary)typeof(BattleSimulator).GetField("performerClocks",Private).GetValue(battle);
            var clock = clocks[caster.Id]; clock.GetType().GetField("NextBig").SetValue(clock,0);
            typeof(BattleSimulator).GetMethod("GainUltimateEnergy",Private).Invoke(battle,new object[]{caster,100});
            Call("RefreshAllCells"); Assert.That(battle.IsUltimateReady(caster), Is.True);
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(events); Object.DestroyImmediate(camera.gameObject);
            frame.Release(); Object.DestroyImmediate(frame);
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator RealCardRaycastStartsTimingAndOneHitCommitsOneUltimate()
        {
            yield return null;
            Click(Find("Cell-P-0-0").GetComponent<Button>());
            var timing = panel.GetComponent<BattleUltimateTimingView1019>();
            Assert.That(timing, Is.Not.Null); Assert.That(timing.IsActive, Is.True);
            Assert.That(panel.Battle.EnergyOf(caster), Is.EqualTo(100), "opening the prompt cannot spend energy");
            timing.Advance(.77f); Capture("battle-manual-qte.png");
            var hit = Find("TimingBar").GetComponent<Button>(); Click(hit); Assert.That(hit.interactable, Is.False);
            ExecuteEvents.Execute(hit.gameObject, new PointerEventData(events.GetComponent<EventSystem>()), ExecuteEvents.pointerClickHandler);
            timing.Advance(.2f);
            Assert.That(timing.IsActive, Is.False); Assert.That(panel.Battle.EnergyOf(caster), Is.Zero);
            Assert.That(panel.Battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.ActorId == caster.Id && e.SkillId == panel.Battle.ActiveSkillId(caster,true)), Is.EqualTo(1));
            Call("RefreshAllCells"); Assert.That(panel.Battle.UltimateCooldownRemaining(caster), Is.GreaterThan(0));
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator VisiblePauseCancelsUncommittedTimingWithoutSpendingAndAutoToggleDoesLikewise()
        {
            yield return null; Click(Find("Cell-P-0-0").GetComponent<Button>());
            var timing = panel.GetComponent<BattleUltimateTimingView1019>(); Assert.That(timing.IsActive, Is.True);
            Click(Find("PauseToggle").GetComponent<Button>());
            Assert.That(panel.IsPaused, Is.True); Assert.That(timing.IsActive, Is.False);
            Assert.That(panel.Battle.EnergyOf(caster), Is.EqualTo(100));
            Click(Find("PauseToggle").GetComponent<Button>()); Assert.That(panel.IsPaused, Is.False);
            Click(Find("Cell-P-0-0").GetComponent<Button>()); Assert.That(timing.IsActive, Is.True);
            Call("ToggleAuto"); Assert.That(timing.IsActive, Is.False); Assert.That(panel.AutoMode, Is.True);
            Assert.That(panel.Battle.EnergyOf(caster), Is.EqualTo(100));
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator ActualSkillSymbolsDisappearAfterTheirShortCueDespiteFullEnergy()
        {
            yield return null;
            var card = Find("Cell-P-0-0");
            var rows = card.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("PsdSkillRow-")).OrderBy(t=>t.name).ToArray();
            Assert.That(rows.All(t=>!t.gameObject.activeInHierarchy), Is.True, "Full energy is not a permanent skill-icon column.");
            panel.Battle.AdvanceRealtime(3000);
            var entry = panel.Battle.Log.Last(e=>e.Kind==BattleEventKind.ActionStarted && e.ActorId==caster.Id && e.SkillId==panel.Battle.ActiveSkillId(caster,false));
            var hp = panel.Battle.Units.ToDictionary(u=>u.Id,u=>u.Hp);
            typeof(TacticsBattlePanel).GetMethod("PresentEvent",Private).Invoke(panel,new object[]{entry,-1});
            Assert.That(rows[1].gameObject.activeInHierarchy, Is.True);
            Assert.That(rows[0].gameObject.activeInHierarchy, Is.False);
            typeof(TacticsBattlePanel).GetField("presentationClock",Private).SetValue(panel,2f); Call("RefreshPsdSkillCues1019");
            Assert.That(rows.All(t=>!t.gameObject.activeInHierarchy), Is.True);
            foreach(var u in panel.Battle.Units) Assert.That(u.Hp, Is.EqualTo(hp[u.Id]), "Cue retirement cannot change combat.");
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator DicePassiveFlashesOnlyOnceForActualProcInOneHand()
        {
            yield return null; panel.Battle.AdvanceRealtime(3000);
            var entries = panel.Battle.Log.Where(e=>e.Kind==BattleEventKind.Damage && e.SkillId=="rt-charm-follow" && e.Amount>0).Take(2).ToArray();
            Assert.That(entries.Length, Is.EqualTo(2));
            var row=Find("Cell-P-0-0").GetComponentsInChildren<Transform>(true).First(t=>t.name=="PsdSkillRow-2");
            var method=typeof(TacticsBattlePanel).GetMethod("PresentEvent",Private);
            method.Invoke(panel,new object[]{entries[0],-1}); Assert.That(row.gameObject.activeInHierarchy, Is.True);
            typeof(TacticsBattlePanel).GetField("presentationClock",Private).SetValue(panel,2f);Call("RefreshPsdSkillCues1019");
            Assert.That(row.gameObject.activeInHierarchy, Is.False);
            method.Invoke(panel,new object[]{entries[1],-1}); Assert.That(row.gameObject.activeInHierarchy, Is.False,"Repeated real follow-ups must not keep the passive decoration permanently visible.");
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator UltimateFlashStaysAbovePsdBelowRuntimeOverlaysAndReducedMotionNeverFlashes()
        {
            bool hadReduced=PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            int previousReduced=PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            try
            {
                PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey,0);
                yield return null;
                var flash=(Image)typeof(TacticsBattlePanel).GetField("ultFlash",Private).GetValue(panel);
                var psd=(RectTransform)Find("BattlePSD928");
                Vector3 stageScale=psd.localScale; Vector2 stagePosition=psd.anchoredPosition;
                Assert.That(flash.gameObject.activeSelf,Is.False);
                AssertFlashLayer(flash,psd);
                Click(Find("Cell-P-0-0").GetComponent<Button>());
                var timing=panel.GetComponent<BattleUltimateTimingView1019>();
                Assert.That(timing.IsActive,Is.True);
                AssertFlashLayer(flash,psd); // QTE was created at runtime, after Build.
                timing.Cancel();

                int cursor=panel.Battle.Log.Count;
                Assert.That(panel.Battle.TryCastUltimate(caster.Id),Is.True);
                var entry=panel.Battle.Log.Skip(cursor).First(e=>e.Kind==BattleEventKind.ActionStarted &&
                    e.ActorId==caster.Id && e.SkillId==panel.Battle.ActiveSkillId(caster,true));
                var hp=panel.Battle.Units.ToDictionary(u=>u.Id,u=>u.Hp);
                typeof(TacticsBattlePanel).GetMethod("PresentEvent",Private).Invoke(panel,new object[]{entry,-1});
                Assert.That(flash.gameObject.activeSelf,Is.True,"A real ultimate must activate the existing flash.");
                Assert.That(flash.raycastTarget,Is.False);
                Assert.That(Find("OptionalOriginalUltimateVideo"),Is.Not.Null,"Runtime cut-in fallback was built.");
                AssertFlashLayer(flash,psd);
                Assert.That(psd.localScale,Is.EqualTo(stageScale));
                Assert.That(psd.anchoredPosition,Is.EqualTo(stagePosition),"Flash cannot move the PSD/HUD or viewport fit.");
                foreach(var unit in panel.Battle.Units) Assert.That(unit.Hp,Is.EqualTo(hp[unit.Id]));

                PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey,1);
                yield return null; yield return null;
                Assert.That(flash.gameObject.activeSelf,Is.False,"Enabling reduced motion cancels an in-flight flash.");
                Assert.That(flash.color.a,Is.Zero);
                var next=panel.Battle.Units.First(u=>u.Side==BattleSide.Player && u.Id!=caster.Id);
                var clocks=(System.Collections.IDictionary)typeof(BattleSimulator).GetField("performerClocks",Private).GetValue(panel.Battle);
                var clock=clocks[next.Id]; clock.GetType().GetField("NextBig").SetValue(clock,0);
                typeof(BattleSimulator).GetMethod("GainUltimateEnergy",Private).Invoke(panel.Battle,new object[]{next,100});
                cursor=panel.Battle.Log.Count;
                Assert.That(panel.Battle.TryCastUltimate(next.Id),Is.True);
                entry=panel.Battle.Log.Skip(cursor).First(e=>e.Kind==BattleEventKind.ActionStarted &&
                    e.ActorId==next.Id && e.SkillId==panel.Battle.ActiveSkillId(next,true));
                typeof(TacticsBattlePanel).GetMethod("PresentEvent",Private).Invoke(panel,new object[]{entry,-1});
                Assert.That(flash.gameObject.activeSelf,Is.False,"Reduced motion never activates a new ultimate flash.");
                Assert.That(flash.color.a,Is.Zero);
                AssertFlashLayer(flash,psd);
            }
            finally
            {
                if(hadReduced)PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey,previousReduced);
                else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            }
        }
        void AssertFlashLayer(Image flash,RectTransform psd)
        {
            Assert.That(flash.transform.parent,Is.SameAs(psd.parent));
            int order=flash.transform.GetSiblingIndex();
            Assert.That(order,Is.GreaterThan(psd.GetSiblingIndex()),"The opaque PSD/backdrop cannot cover the flash.");
            foreach(string field in new[]{"pauseOverlay","rulesOverlay","abilityOverlay","resultOverlay"})
            {
                var overlay=(GameObject)typeof(TacticsBattlePanel).GetField(field,Private).GetValue(panel);
                if(overlay!=null)Assert.That(order,Is.LessThan(overlay.transform.GetSiblingIndex()),field+" stays above flash.");
            }
            foreach(string name in new[]{"AlbumUltimate","SkillCutInBanner","ManualUltimateTiming","OptionalOriginalUltimateVideo"})
            {
                var layer=panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
                if(layer!=null)Assert.That(order,Is.LessThan(layer.GetSiblingIndex()),name+" stays above flash.");
            }
        }
        void Call(string name) => typeof(TacticsBattlePanel).GetMethod(name,Private).Invoke(panel,null);
        Transform Find(string name) => panel.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        void Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); var rect = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(events.GetComponent<EventSystem>()){position=point,button=PointerEventData.InputButton.Left};
            var hits = new List<RaycastResult>(); host.GetComponent<GraphicRaycaster>().Raycast(data,hits);
            Assert.That(hits, Is.Not.Empty); Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), string.Join(" > ",hits.Select(h=>h.gameObject.name)));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            Canvas.ForceUpdateCanvases(); camera.Render(); var old=RenderTexture.active; RenderTexture.active=frame;
            var tex=new Texture2D(frame.width,frame.height,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);tex.Apply();
            string path="/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/battle-psd-align-20261019/runtime/"+name;
            File.WriteAllBytes(path,tex.EncodeToPNG()); RenderTexture.active=old;Object.DestroyImmediate(tex);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace ChoSiren.Tests
{
    public sealed class BattlePostReleaseAudit929Tests
    {
        GameObject host, eventHost; Camera camera; RenderTexture rt;
        bool had, legacyHad, hadReduced; int reduced; string save, legacy;
        static void Call(TacticsBattlePanel p,string n)=>typeof(TacticsBattlePanel).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        [SetUp] public void Setup()
        {
            had=PlayerPrefs.HasKey(GameModel.SaveKey);legacyHad=PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            save=PlayerPrefs.GetString(GameModel.SaveKey);legacy=PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadReduced=PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduced=PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey,0);
            foreach(var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))Object.DestroyImmediate(app.gameObject);
            PlayerPrefs.SetString(GameModel.SaveKey,JsonUtility.ToJson(new GameSave{StoryProgress=100}));PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            host=new GameObject("PostRelease929Audit",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            camera=new GameObject("AuditCamera",typeof(Camera)).GetComponent<Camera>();rt=new RenderTexture(720,1536,24);camera.targetTexture=rt;
            var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(720,1536);scaler.matchWidthOrHeight=1;
            eventHost=new GameObject("AuditEvents",typeof(EventSystem));
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(host);Object.DestroyImmediate(eventHost);Object.DestroyImmediate(camera.gameObject);rt.Release();Object.DestroyImmediate(rt);
            if(hadReduced)PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey,reduced);else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            if(had)PlayerPrefs.SetString(GameModel.SaveKey,save);else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if(legacyHad)PlayerPrefs.SetString(GameModel.LegacySaveKey,legacy);else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);PlayerPrefs.Save();
        }
        TacticsBattlePanel Open()
        {
            var model=new GameModel();var b=model.StartStageBattle("stage-1-1",929UL,out var message);Assert.That(b,Is.Not.Null,message);
            var p=TacticsBattlePanel.Open(host.transform,model,b,null);p.StopAllCoroutines();return p;
        }
        GameObject ClickAt(Button button)
        {
            Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(eventHost.GetComponent<EventSystem>()){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();host.GetComponent<GraphicRaycaster>().Raycast(data,hits);
            Assert.That(hits,Is.Not.Empty);
            Debug.Log("AUDIT click "+button.name+" hits "+string.Join(" > ",hits.Select(x=>x.gameObject.name)));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);return hits[0].gameObject;
        }
        [UnityTest] public IEnumerator VisibleTopContinueButtonResumesBattle()
        {
            var p=Open();yield return null;
            var top=p.GetComponentsInChildren<Button>(true).Single(x=>x.name=="PauseToggle");
            ClickAt(top);Assert.That(p.IsPaused,Is.True);yield return null;
            Assert.That(top.GetComponentInChildren<AlbumPlayGlyph929>(true).gameObject.activeInHierarchy,Is.True);
            var hit=ClickAt(top);
            Assert.That(p.IsPaused,Is.False,"Visible continue button is intercepted by "+hit.name);
        }
        [UnityTest] public IEnumerator SixDiceDoubleTapOnlySpendsOneAvailableCharge()
        {
            var p=Open();
            var openingRolls=p.GetComponentsInChildren<DiceRollPresentation>(true);
            Assert.That(openingRolls.Length,Is.EqualTo(6),"The landing guard cannot pass on an empty dice list");
            float landingDeadline=Time.realtimeSinceStartup+20f;
            while(openingRolls.Any(roll=>roll.Busy)&&Time.realtimeSinceStartup<landingDeadline)yield return null;
            Assert.That(openingRolls.Any(roll=>roll.Busy),Is.False,"Opening dice must complete their real presentation");
            yield return null; // The panel must observe landing before allowing the first input.
            typeof(TacticsBattlePanel).GetField("awaitingInput",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p,true);
            p.Battle.BattleDice.GainEnergy(200);Call(p,"RefreshDiceUi");
            var slide=p.GetComponentInChildren<BattleRerollSlide929>(true);slide.Advance(.3f);
            var sixth=p.GetComponentsInChildren<Button>(true).Single(x=>x.name=="Dice-5");
            Assert.That(sixth.interactable,Is.True,"Sixth die is available only after real landing");
            ClickAt(sixth);
            Assert.That(p.Battle.BattleDice.SelectedForRerollCount,Is.EqualTo(1));
            Assert.That(p.Battle.BattleDice.Held[5],Is.False);
            var values=p.Battle.BattleDice.Values.ToArray();var reroll=slide.GetComponent<Button>();
            ClickAt(reroll);ClickAt(reroll);
            Assert.That(p.Battle.BattleDice.UsedRerolls,Is.EqualTo(1));
            Assert.That(p.Battle.BattleDice.Rolled.Take(5).Any(x=>x),Is.False);
            Assert.That(p.Battle.BattleDice.Rolled[5],Is.True);
            Assert.That(sixth.GetComponentInChildren<DiceRollPresentation>(true).IsRolling,Is.True,"Selected sixth die must animate after model resets Held");
            for(int i=0;i<5;i++)Assert.That(p.Battle.BattleDice.Values[i],Is.EqualTo(values[i]));
            landingDeadline=Time.realtimeSinceStartup+20f;
            while(openingRolls.Any(roll=>roll.Busy)&&Time.realtimeSinceStartup<landingDeadline)yield return null;
            Assert.That(openingRolls.Any(roll=>roll.Busy),Is.False,"The selected sixth die eventually lands");
            yield return null;
            Assert.That(p.Battle.BattleDice.UsedRerolls,Is.EqualTo(1),"Landing cannot spend another charge");
            Assert.That(p.Battle.BattleDice.Held.All(x=>x),Is.True);
        }
    }
}

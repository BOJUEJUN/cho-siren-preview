using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
namespace ChoSiren.Tests
{
    public sealed class BattleMeeting929PlayTests
    {
        const string Out="/Users/bojuejun/Documents/Codex/2026-09-21/zhe/work/battle-r16-20260929";
        GameObject host;Camera camera;RenderTexture rt;bool had,legacyHad;string save,legacy;
        static void Call(TacticsBattlePanel p,string n)=>typeof(TacticsBattlePanel).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        static T Get<T>(TacticsBattlePanel p,string n)=>(T)typeof(TacticsBattlePanel).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
        [SetUp] public void Setup()
        {
            had=PlayerPrefs.HasKey(GameModel.SaveKey);legacyHad=PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            save=PlayerPrefs.GetString(GameModel.SaveKey);legacy=PlayerPrefs.GetString(GameModel.LegacySaveKey);
            foreach(var app in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))Object.DestroyImmediate(app.gameObject);
            PlayerPrefs.SetString(GameModel.SaveKey,JsonUtility.ToJson(new GameSave{StoryProgress=100}));PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            host=new GameObject("Meeting929Test",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            camera=new GameObject("Meeting929Camera",typeof(Camera)).GetComponent<Camera>();rt=new RenderTexture(720,1536,24);
            camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(720,1536);scaler.matchWidthOrHeight=1;
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(host);Object.DestroyImmediate(camera.gameObject);rt.Release();Object.DestroyImmediate(rt);
            if(had)PlayerPrefs.SetString(GameModel.SaveKey,save);else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if(legacyHad)PlayerPrefs.SetString(GameModel.LegacySaveKey,legacy);else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);PlayerPrefs.Save();
        }
        TacticsBattlePanel Open(string stage)
        {
            var model=new GameModel();var b=model.StartStageBattle(stage,929UL,out var message);Assert.That(b,Is.Not.Null,message);
            var p=TacticsBattlePanel.Open(host.transform,model,b,null);p.StopAllCoroutines();return p;
        }
        [UnityTest] public IEnumerator PauseResumeReceivesActualRaycasts()
        {
            var p=Open("stage-1-5");yield return new WaitForSecondsRealtime(1.1f);
            Call(p,"TogglePause");yield return null;Canvas.ForceUpdateCanvases();
            var resume=p.GetComponentsInChildren<Button>(true).Single(x=>x.name=="ResumeBattle");
            var eventHost=new GameObject("929RaycastEvents",typeof(UnityEngine.EventSystems.EventSystem));
            try
            {
                var events=eventHost.GetComponent<UnityEngine.EventSystems.EventSystem>();
                var rect=resume.GetComponent<RectTransform>();
                var at=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
                var data=new UnityEngine.EventSystems.PointerEventData(events){position=at,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                host.GetComponent<GraphicRaycaster>().Raycast(data,hits);
                Assert.That(hits.Count,Is.GreaterThan(0));
                Assert.That(hits[0].gameObject,Is.EqualTo(resume.gameObject),string.Join(" > ",hits.Select(x=>x.gameObject.name)));
                Assert.That(resume.IsInteractable(),Is.True);
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                Assert.That(p.IsPaused,Is.False);
            }
            finally{Object.DestroyImmediate(eventHost);}
        }
        [UnityTest] public IEnumerator RerollSlidesOnlyWithChargeAndNeverMovesItsHitRectangle()
        {
            var p=Open("stage-1-1");yield return new WaitForSecondsRealtime(1.1f);
            var b=Get<ChoSiren.Systems.Tactics.BattleSimulator>(p,"battle");
            var slide=p.GetComponentInChildren<BattleRerollSlide929>(true);var button=slide.GetComponent<Button>();var hit=slide.GetComponent<RectTransform>();
            var at=hit.anchoredPosition;Assert.That(slide.Reveal,Is.Zero);Assert.That(button.interactable,Is.False);Capture("battle-initial.png");
            b.BattleDice.GainEnergy(100);Call(p,"RefreshDiceUi");slide.Advance(.3f);
            Assert.That(slide.Reveal,Is.EqualTo(1));Assert.That(hit.anchoredPosition,Is.EqualTo(at));
            // The panel coroutine was stopped before enabling realtime input in this isolated fixture.
            typeof(TacticsBattlePanel).GetField("awaitingInput",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,true);Call(p,"RefreshDiceUi");
            Assert.That(button.interactable,Is.True);Capture("battle-reroll-ready.png");
            var dice=Get<System.Collections.Generic.List<GameObject>>(p,"diceButtons");dice[5].GetComponent<Button>().onClick.Invoke();
            var values=b.BattleDice.Values.ToArray();button.onClick.Invoke();
            Assert.That(b.BattleDice.UsedRerolls,Is.EqualTo(1));
            for(int i=0;i<5;i++)Assert.That(b.BattleDice.Values[i],Is.EqualTo(values[i]));
            slide.Advance(.3f);Assert.That(slide.Reveal,Is.Zero);Assert.That(button.interactable,Is.False);
            Call(p,"TogglePause");b.BattleDice.GainEnergy(100);Call(p,"RefreshDiceUi");slide.Advance(1);Assert.That(slide.Reveal,Is.Zero);
            Call(p,"TogglePause");slide.Advance(.3f);Assert.That(slide.Reveal,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator PortraitsRestBehindSleevesAndUltimateIsBoundedAndNonBlocking()
        {
            var p=Open("stage-1-1");yield return null;Canvas.ForceUpdateCanvases();
            var b=Get<ChoSiren.Systems.Tactics.BattleSimulator>(p,"battle");
            foreach(var cell in p.GetComponentsInChildren<RectTransform>(true).Where(x=>x.name.StartsWith("Cell-P-")&&x.gameObject.activeInHierarchy))
            {
                var frame=cell.Find("PsdUnitFrame");var pic=cell.GetComponentsInChildren<Image>(true).First(x=>x.name=="Portrait");
                Assert.That(frame.GetSiblingIndex(),Is.GreaterThan(pic.transform.parent.GetSiblingIndex()));
                Assert.That(pic.rectTransform.rect.height,Is.InRange(140,166));
            }
            b.AutoCastUltimates=false;b.AdvanceRealtime(22000);Call(p,"RefreshAllCells");
            var ult=p.GetComponent<BattleAlbumUltimate929>();var portrait=BattlePsdArt928.Sprite("battle-39");
            ult.Enqueue(portrait,"夜莺","猩红处决",Color.magenta);ult.Advance(.4f);Capture("battle-ultimate.png");
            Assert.That(ult.IsShowing,Is.True);var page=p.transform.Find("AlbumUltimate");
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(page.GetComponent<Image>(),Is.Null,"No dark backdrop behind ultimate");
            Assert.That(page.GetComponentsInChildren<Graphic>().Length,Is.EqualTo(1),"Only transparent portrait, no record or banner");
            Assert.That(page.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),Is.True);
            Call(p,"TogglePause");ult.Advance(5);Assert.That(ult.IsShowing,Is.True);
            Call(p,"TogglePause");ult.Advance(2.5f);Assert.That(ult.IsShowing,Is.False);
        }
        [UnityTest] public IEnumerator QueuedUltimatesLeaveBattleVisibleAndStayAboveCombatFx()
        {
            var p=Open("stage-1-1");yield return null;
            var ult=p.GetComponent<BattleAlbumUltimate929>();
            var art=BattlePsdArt928.Sprite("battle-39");
            ult.Enqueue(art,"A","第一段",Color.magenta);ult.Enqueue(art,"B","第二段",Color.cyan);
            var page=p.transform.Find("AlbumUltimate");
            Assert.That(page.GetSiblingIndex(),Is.GreaterThan(p.transform.Find("SkillEffects").GetSiblingIndex()));
            Assert.That(page.GetSiblingIndex(),Is.GreaterThan(p.transform.Find("CombatCueArea").GetSiblingIndex()));
            Assert.That(page.GetSiblingIndex(),Is.LessThan(p.transform.Find("PauseOverlay").GetSiblingIndex()));
            ult.Advance(1.51f);Assert.That(ult.IsShowing,Is.False);Assert.That(ult.PendingCount,Is.EqualTo(1));
            ult.Advance(.34f);Assert.That(ult.IsShowing,Is.False);
            ult.Advance(.12f);Assert.That(ult.IsShowing,Is.True);Assert.That(ult.PendingCount,Is.Zero);
            ult.Cancel();
        }
        [UnityTest] public IEnumerator BossAddsHaveSeparateSilhouetteLanesAndGuardExpires()
        {
            var p=Open("stage-1-5");yield return null;var b=Get<ChoSiren.Systems.Tactics.BattleSimulator>(p,"battle");
            b.TryTacticalGuard();Call(p,"RefreshAllCells");yield return null;Capture("battle-boss.png");
            var bossUnit=b.Units.First(u=>u.Definition.Id=="siren-queen");
            typeof(ChoSiren.Systems.Tactics.BattleSimulator).GetMethod("ApplyActualDamage",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(b,new object[]{b.Units.First(u=>u.Side==ChoSiren.Systems.Tactics.BattleSide.Player),bossUnit,"test-phases",bossUnit.MaxHp*75/100,false,1000});
            Call(p,"RefreshAllCells");yield return null;Capture("battle-boss-three-lanes.png");
            Assert.That(b.EnemyPhase,Is.EqualTo(3));
            var boss=p.GetComponentsInChildren<Image>(true).First(x=>x.name=="BossPortrait");
            Vector3[] bc=new Vector3[4];boss.rectTransform.GetWorldCorners(bc);
            var enemies=p.GetComponentsInChildren<Image>().Where(x=>x.name.StartsWith("EnemyFigure-"));
            foreach(var e in enemies)
            {
                Vector3[] ec=new Vector3[4];e.rectTransform.GetWorldCorners(ec);
                Assert.That(ec[3].x<=bc[0].x || ec[0].x>=bc[3].x,Is.True,"Adds must remain outside boss silhouette lane");
            }
            b.AdvanceRealtime(3000);Assert.That(b.TryTacticalGuard(),Is.False);Call(p,"RefreshRealtimeCommands");
            Assert.That(Get<GameObject>(p,"guardButton").GetComponent<Button>().interactable,Is.False);
        }
        [UnityTest] public IEnumerator GuardHitSurfaceStaysTransparentAcrossRefreshPauseAndUse()
        {
            var p=Open("stage-1-1");yield return null;
            var b=p.Battle;var button=Get<GameObject>(p,"guardButton");
            for(int state=0;state<5;state++)
            {
                if(state==1)Call(p,"TogglePause");
                if(state==2){Call(p,"TogglePause");Assert.That(b.TryTacticalGuard(),Is.True);}
                if(state==3)b.AdvanceRealtime(3100);
                if(state==4)Call(p,"TogglePause");
                for(int refresh=0;refresh<12;refresh++)
                {
                    Call(p,"RefreshRealtimeCommands");
                    Assert.That(button.GetComponent<Image>().color.a,Is.Zero,"State "+state+" refresh "+refresh);
                    yield return null;
                }
                if(state==2)Capture("battle-guard-active.png");
            }
        }
        [UnityTest] public IEnumerator MemberSleevesExpandOnlyWhenReadyAndRetractAfterCast()
        {
            var p=Open("stage-1-2");yield return null;var b=p.Battle;b.AutoCastUltimates=false;
            var albums=p.GetComponentsInChildren<BattleMemberAlbum929>();Assert.That(albums.Length,Is.EqualTo(4));
            var idlePositions=albums.Select(a=>a.Disc.anchoredPosition.y).ToArray();
            Assert.That(albums.All(a=>a.Expansion==0),Is.True);Capture("members-idle.png");
            // An isolated weak-opponent visual fixture leaves all four alive until each has full energy.
            foreach(var u in b.Units.Where(u=>u.Side==ChoSiren.Systems.Tactics.BattleSide.Enemy)){u.Hp=u.MaxHp=10000000;}
            b.AdvanceRealtime(50000);Call(p,"RefreshAllCells");
            for(int i=0;i<albums.Length;i++)albums[i].Advance(.4f);
            Assert.That(albums.Any(a=>a.Expansion==1),Is.True);
            for(int i=0;i<albums.Length;i++)if(albums[i].Expansion>0)
                Assert.That(albums[i].Disc.anchoredPosition.y,Is.GreaterThan(idlePositions[i]+40));
            Capture("members-ready.png");
            foreach(var u in b.Units.Where(u=>u.Side==ChoSiren.Systems.Tactics.BattleSide.Player))b.TryCastUltimate(u.Id);
            for(int i=0;i<albums.Length;i++)albums[i].Advance(.3f);
            Assert.That(albums.All(a=>a.Expansion==0),Is.True);Capture("members-spent.png");
        }
        [UnityTest] public IEnumerator FreshMapMakesLocksExplicitWithoutChangingUserSave()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            var model=new GameModel();var map=LevelMapPanel.Open(host.transform,model);
            yield return null;Capture("map-locks.png");
            var nodes=map.GetComponentsInChildren<RectTransform>(true);
            var locked=nodes.First(n=>n.name=="Level-1-2");
            Assert.That(locked.Find("LockIcon").GetComponent<Image>().enabled,Is.True);
            Assert.That(locked.Find("StatusLabel").GetComponent<Text>().text,Is.EqualTo("未解锁"));
            var current=nodes.First(n=>n.name=="Level-1-1");
            Assert.That(current.Find("StatusLabel").GetComponent<Text>().text,Is.EqualTo("可挑战"));
            int stage=map.SelectedStage;locked.GetComponent<Button>().onClick.Invoke();Assert.That(map.SelectedStage,Is.EqualTo(stage));
        }
        [Test] public void FreshFirstChapterBalanceSample()
        {
            // 2026-11-01 (赵金数值批次, Balance1101Tests): the shipped turn battle; a level-1 team wins 1-1 about six times in
            // ten now, so the sample records outcomes instead of demanding a 1-1 victory on every seed.
            var lines=new System.Collections.Generic.List<string>();
            for(ulong seed=929;seed<932;seed++)
            {
                PlayerPrefs.DeleteKey(GameModel.SaveKey);PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);var model=new GameModel();
                for(int level=1;level<=5;level++)
                {
                    var b=model.StartStageBattle("stage-1-"+level,seed,out var message);
                    if(b==null){lines.Add($"seed {seed}, stage {level}: {message}");break;}
                    b.EnableTurnBased();
                    for(int guard=0;guard<4000 && b.Outcome==ChoSiren.Systems.Tactics.BattleOutcome.Ongoing;guard++)
                    {
                        var actor=b.NextTurnActor();if(actor==null)break;
                        if(b.TurnRound>=3 && !b.TacticalGuardUsed)b.TryTacticalGuard();
                        b.AutoTurn();
                    }
                    Assert.That(b.Outcome,Is.Not.EqualTo(ChoSiren.Systems.Tactics.BattleOutcome.Ongoing));
                    model.SettleStageBattle(b,out message);
                    var casts=b.Log.Where(e=>e.Kind==ChoSiren.Systems.Tactics.BattleEventKind.ActionStarted && e.SkillId!=null && e.SkillId.EndsWith("-big")).GroupBy(e=>e.ActorId).Select(g=>g.Key+":"+g.Count());
                    lines.Add($"seed {seed}, stage {level}: {b.Outcome}, {b.TurnRound} rounds, lost {b.PlayerUnitsLost}, ults [{string.Join(",",casts)}]");
                    if(b.Outcome!=ChoSiren.Systems.Tactics.BattleOutcome.Victory)break;
                }
            }
            Directory.CreateDirectory(Out);File.WriteAllLines(Path.Combine(Out,"balance-samples.txt"),lines);
        }
        [Test] public void FirstBossAtRecommendedLevelKeepsAllThreePhasesPlayable()
        {
            // 2026-11-01: 1-5 is the first 小BOSS wall — recommended level 10 with the gear of 1-1~1-4 (≈65%, Balance1101Tests),
            // played in the shipped turn battle. Every win goes through all three phases.
            var lines=new System.Collections.Generic.List<string>();int wins=0;
            for(ulong seed=929;seed<939;seed++)
            {
                PlayerPrefs.DeleteKey(GameModel.SaveKey);PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);var model=new GameModel();
                model.Save.StoryProgress=100;foreach(int member in model.Save.Team)model.Save.MemberLevels[member]=10;
                for(int cleared=1;cleared<=4;cleared++){int item=GameModel.FirstClearAccessory("stage-1-"+cleared);if(!model.Save.OwnedAccessories.Contains(item))model.Save.OwnedAccessories.Add(item);}
                foreach(int member in model.Save.Team.ToArray())model.QuickEquip(member,out _);
                var b=model.StartStageBattle("stage-1-5",seed,out var message);Assert.That(b,Is.Not.Null,message);
                b.EnableTurnBased();
                // 2026-11-01c 无限骰子: the 通过率 policy = auto ultimates + the shipped auto reroll (Balance1101Tests).
                int actions=0,lastReroll=-100;var dice=b.BattleDice;
                for(int guard=0;guard<4000 && b.Outcome==ChoSiren.Systems.Tactics.BattleOutcome.Ongoing;guard++)
                {
                    var actor=b.NextTurnActor();if(actor==null)break;
                    if(actor.Side==ChoSiren.Systems.Tactics.BattleSide.Player&&actions-lastReroll>=3&&ChoSiren.Systems.Dice.DiceAutoPlanner1031.ShouldAutoReroll(dice))
                    {
                        bool[] pick=ChoSiren.Systems.Dice.DiceAutoPlanner1031.ChooseRerollMask(dice.Values,ChoSiren.Systems.Dice.DiceTurn.MaxDicePerReroll,false);
                        for(int i=0;i<pick.Length;i++)if(pick[i]&&dice.Held[i])dice.ToggleHold(i);
                        if(dice.RerollUnheld(out _))lastReroll=actions;
                    }
                    b.AutoTurn();actions++;
                }
                if(b.Outcome==ChoSiren.Systems.Tactics.BattleOutcome.Victory){wins++;Assert.That(b.EnemyPhase,Is.EqualTo(3));}
                lines.Add($"seed {seed}: {b.Outcome}, {b.TurnRound} rounds, lost {b.PlayerUnitsLost}, enemyHP {b.CurrentEnemyHp}, phase {b.EnemyPhase}");
            }
            Directory.CreateDirectory(Out);File.WriteAllLines(Path.Combine(Out,"boss-level5-samples.txt"),lines);
            Assert.That(wins,Is.InRange(3,9),"推荐等级+装备的 1-5 小BOSS 约六成五通过：不靠一个幸运种子，也不白给");
        }
        void Capture(string file)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            Canvas.ForceUpdateCanvases();camera.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
            Directory.CreateDirectory(Out);File.WriteAllBytes(Path.Combine(Out,file),tex.EncodeToPNG());RenderTexture.active=prev;Object.DestroyImmediate(tex);
        }
    }
}

using System.Collections;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-12 (原画同事反馈): on a boss wave the boss was already dead but stayed on the stage until her adds were
    /// cleared too. She must break apart and leave the moment she falls, while the adds keep standing.
    /// </summary>
    public sealed class BossDeath1112Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator FallenBossLeavesTheStageWhileHerAddsFightOn()
        {
            bool had = PlayerPrefs.HasKey(GameModel.SaveKey);
            string save = PlayerPrefs.GetString(GameModel.SaveKey);
            var host = new GameObject("BossDeath", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var events = new GameObject("BossDeathEvents", typeof(EventSystem));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var data = new GameSave { StoryProgress = 100 };
                for (int n = 1; n <= 4; n++) data.ClearedStages.Add(new StageClear { Id = "stage-1-" + n, Stars = 3 });
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
                var model = new GameModel();
                BattleSimulator battle = model.StartStageBattle("stage-1-5", 1112UL, out string message);
                Assert.That(battle, Is.Not.Null, message);
                var panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
                yield return null;
                panel.StopAllCoroutines();
                var movie = panel.transform.Find("BossEntrance1102");
                if (movie != null) Object.DestroyImmediate(movie.gameObject);
                foreach (string field in new[] { "paused", "closing", "coveredByStory", "bossEntrancePlaying1102" })
                    typeof(TacticsBattlePanel).GetField(field, Private)?.SetValue(panel, false);
                yield return new WaitForSecondsRealtime(.3f);

                int bossId = (int)typeof(TacticsBattlePanel).GetField("bossUnitId", Private).GetValue(panel);
                BattleUnit boss = battle.FindUnit(bossId);
                Assert.That(boss, Is.Not.Null, "1-5 is a boss stage");
                var presentation = panel.GetComponentInChildren<BossBattlePresentation>(true);
                Assert.That(presentation, Is.Not.Null);
                Image portrait = panel.GetComponentsInChildren<Image>(true).First(i => i.name == "BossPortrait");
                Assert.That(portrait.gameObject.activeInHierarchy, Is.True, "the boss stands on the stage before she falls");

                // Make sure an add stands beside her, as on the reported 3/3 boss wave.
                BattleUnit add = battle.Units.FirstOrDefault(u => u.Side == BattleSide.Enemy && u.Id != bossId && u.Alive)
                    ?? battle.Units.First(u => u.Side == BattleSide.Enemy && u.Id != bossId);
                add.Spawned = true;
                if (add.Hp <= 0) add.Hp = add.MaxHp;
                typeof(TacticsBattlePanel).GetMethod("RefreshAllCells", Private)?.Invoke(panel, null);
                yield return null;

                BattleUnit striker = battle.Units.First(u => u.Side == BattleSide.Player && u.Alive);
                boss.Hp = 0;
                Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing), "her add still fights, so the battle goes on");
                typeof(TacticsBattlePanel).GetMethod("PresentEvent", Private).Invoke(panel, new object[]
                {
                    new BattleEvent { Kind = BattleEventKind.Defeated, ActorId = striker.Id, TargetId = bossId, SkillId = "basic" }, -1
                });
                yield return new WaitForSecondsRealtime(1.6f);

                Assert.That(presentation.Vanished, Is.True, "the fallen boss leaves at once, not after her adds");
                Assert.That(portrait.gameObject.activeInHierarchy, Is.False, "no dead boss figure is left on the stage");
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Defeated));
                Image addFigure = panel.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "EnemyFigure-" + add.Id);
                Assert.That(addFigure, Is.Not.Null);
                Assert.That(addFigure.gameObject.activeInHierarchy, Is.True, "her add stays on the stage until it is beaten");

                // The later victory must not bring her back.
                presentation.PlayOutcome(true);
                yield return null;
                Assert.That(portrait.gameObject.activeInHierarchy, Is.False);
                Object.DestroyImmediate(panel.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(events);
                if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            }
        }
    }
}

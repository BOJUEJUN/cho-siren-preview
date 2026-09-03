using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BossBattlePresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator BattleDamageAndEnemyActionDriveBossFeedbackLayers()
        {
            GameObject root = new GameObject("Boss Battle Integration Test");
            try
            {
                BattleSimulator simulator = CreateBattle();
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform,
                    new GameModel(() => new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Local)),
                    simulator, _ => { });
                BossBattlePresentation boss = panel.GetComponentInChildren<BossBattlePresentation>(true);
                Assert.That(boss, Is.Not.Null);
                Assert.That(Find(panel.transform, "BossMotionRig"), Is.Not.Null);
                Assert.That(Find(panel.transform, "BossHitSlashAI"), Is.Not.Null);
                Assert.That(Find(panel.transform, "BossHeartImpactAI"), Is.Not.Null);
                Assert.That(Find(panel.transform, "BossChargeAuraAI"), Is.Not.Null);
                Assert.That(Find(panel.transform, "BossLowHealthFrameAI"), Is.Not.Null);
                Assert.That(Find(panel.transform, "BossDamageNumbers"), Is.Not.Null);

                BattleUnit player = FindUnit(simulator, BattleSide.Player);
                BattleUnit enemy = FindUnit(simulator, BattleSide.Enemy);
                MethodInfo present = typeof(TacticsBattlePanel).GetMethod("PresentEvent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(present, Is.Not.Null);

                present.Invoke(panel, new object[]
                {
                    new BattleEvent
                    {
                        Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = enemy.Id,
                        SkillId = "strike", Amount = 120, Critical = true,
                    },
                    -1,
                });
                Assert.That(boss.HitReactionCount, Is.EqualTo(1),
                    "敌方受到真实伤害事件时必须驱动 Boss 受击演出");

                MethodInfo enemyPrelude = typeof(TacticsBattlePanel).GetMethod("BeginEnemyActionPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(enemyPrelude, Is.Not.Null);
                enemyPrelude.Invoke(panel, new object[]
                {
                    enemy,
                    new BattleAction { ActorId = enemy.Id, SkillId = "strike", Row = 0, Col = 0 },
                });
                Assert.That(boss.ChargeReactionCount, Is.EqualTo(1),
                    "敌方行动必须先驱动 Boss 蓄力前摇，再由战斗事件表现命中");

                present.Invoke(panel, new object[]
                {
                    new BattleEvent { Kind = BattleEventKind.PhaseChanged, Phase = 3 },
                    -1,
                });
                Assert.That(boss.PhaseReactionCount, Is.EqualTo(1));

                boss.SetHealthRatio(0.25f);
                Assert.That(boss.LowHealth, Is.True);
                boss.PlayOutcome(true);
                Assert.That(boss.OutcomeReactionCount, Is.EqualTo(1));
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Defeated));

                yield return null;
                Assert.That(boss.MotionRoot.localScale.x, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator PresentationPausesCleanlyThenRestoresEffectsAndShowsLowHealthWarning()
        {
            GameObject root = new GameObject("Boss Presentation State Test", typeof(RectTransform));
            bool paused = false;
            int speed = 1;
            try
            {
                RectTransform rig = new GameObject("Rig", typeof(RectTransform)).GetComponent<RectTransform>();
                rig.SetParent(root.transform, false);
                Image portrait = NewImage("Portrait", rig);
                Image echo = NewImage("Echo", rig);
                Image rearAura = NewImage("RearAura", root.transform);
                Image coreAura = NewImage("CoreAura", root.transform);
                Image shadow = NewImage("Shadow", root.transform);
                Image stagePulse = NewImage("StagePulse", root.transform);
                Image warning = NewImage("Warning", root.transform);
                Image hitSlash = NewImage("HitSlash", root.transform);
                Image heartImpact = NewImage("HeartImpact", root.transform);
                Image chargeAura = NewImage("ChargeAura", root.transform);
                Text label = new GameObject("State", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(root.transform, false);
                Image[] rings = { NewImage("Ring", root.transform) };
                Image[] trails = { NewImage("Trail", root.transform) };
                Text[] damage =
                {
                    new GameObject("Damage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                        .GetComponent<Text>(),
                };
                damage[0].transform.SetParent(root.transform, false);

                BossBattlePresentation presentation = root.AddComponent<BossBattlePresentation>();
                presentation.Configure(rig, portrait, echo, rearAura, coreAura, shadow, stagePulse,
                    warning, hitSlash, heartImpact, chargeAura, label, rings, trails, damage,
                    () => paused, () => speed);

                presentation.PlayHit(456, true);
                yield return null;
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Hit));
                Assert.That(hitSlash.gameObject.activeSelf, Is.True);
                Assert.That(heartImpact.gameObject.activeSelf, Is.True);
                Assert.That(damage[0].gameObject.activeSelf, Is.True);

                paused = true;
                Vector2 frozenPosition = rig.anchoredPosition;
                Vector3 frozenScale = rig.localScale;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(rig.anchoredPosition, Is.EqualTo(frozenPosition),
                    "暂停后 Boss 的受击位移不应偷偷继续");
                Assert.That(rig.localScale, Is.EqualTo(frozenScale),
                    "暂停后 Boss 的受击缩放不应偷偷继续");

                paused = false;
                yield return new WaitForSecondsRealtime(0.62f);
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Idle));
                Assert.That(hitSlash.gameObject.activeSelf, Is.False,
                    "受击结束后 AI 斩击层必须复位，避免永久遮挡角色");
                Assert.That(heartImpact.gameObject.activeSelf, Is.False,
                    "受击结束后 AI 心碎冲击层必须复位");
                Assert.That(portrait.color, Is.EqualTo(Color.white));

                presentation.SetHealthRatio(0.25f);
                yield return null;
                Assert.That(presentation.LowHealth, Is.True);
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.LowHealth));
                Assert.That(label.gameObject.activeSelf, Is.True);
                Assert.That(warning.color.a, Is.GreaterThan(0f),
                    "低血量时 AI 危险边框必须实际可见");

                presentation.PlayCharge("终曲");
                yield return new WaitForSecondsRealtime(0.2f);
                speed = 2;
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.LowHealth),
                    "中途切换到 2 倍速后，蓄力演出仍须按逻辑时间完成并回到低血量待机");
                Assert.That(chargeAura.gameObject.activeSelf, Is.False,
                    "蓄力结束后 AI 聚光舞台层必须复位");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator BattleDelayRespondsWhenSpeedChangesMidWait()
        {
            GameObject root = new GameObject("Dynamic Battle Speed Test");
            try
            {
                TacticsBattlePanel panel = root.AddComponent<TacticsBattlePanel>();
                FieldInfo speedField = typeof(TacticsBattlePanel).GetField("speed",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo waitMethod = typeof(TacticsBattlePanel).GetMethod("WaitBattleDelay",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(speedField, Is.Not.Null);
                Assert.That(waitMethod, Is.Not.Null);

                speedField.SetValue(panel, 1);
                IEnumerator wait = (IEnumerator)waitMethod.Invoke(panel, new object[] { 1.2f });
                float startedAt = Time.realtimeSinceStartup;
                bool switched = false;
                while (wait.MoveNext())
                {
                    if (!switched && Time.realtimeSinceStartup - startedAt >= 0.12f)
                    {
                        speedField.SetValue(panel, 2);
                        switched = true;
                    }
                    yield return wait.Current;
                }

                float realDuration = Time.realtimeSinceStartup - startedAt;
                Assert.That(switched, Is.True);
                Assert.That(realDuration, Is.LessThan(0.95f),
                    "中途切换 2 倍速后，既有等待必须立即加速，不能继续使用进入协程时固化的时长");
                Assert.That(realDuration, Is.GreaterThan(0.35f),
                    "倍速切换不应跳过剩余表现帧");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static BattleSimulator CreateBattle()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "音爆斩", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000,
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "player", Name = "我方", MaxHp = 1000, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" },
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "首领", MaxHp = 3000, Attack = 80, Defense = 30, Speed = 90,
                SkillIds = new List<string> { "strike" },
            });
            var stage = new StageDefinition
            {
                Id = "boss-animation-test", Name = "Boss 动效测试", TurnLimit = 10,
                Enemies = new List<EnemySpawn>
                {
                    new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0, ScalePermille = 1000 },
                },
            };
            manifest.Stages.Add(stage);
            return new BattleSimulator(manifest, stage, new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "player", Row = 0, Col = 0 },
            }, new ScriptedRandom(new[] { 999 }));
        }

        private static BattleUnit FindUnit(BattleSimulator simulator, BattleSide side)
        {
            for (int index = 0; index < simulator.Units.Count; index++)
                if (simulator.Units[index].Side == side) return simulator.Units[index];
            return null;
        }

        private static Image NewImage(string name, Transform parent)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<Image>();
            image.transform.SetParent(parent, false);
            return image;
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
                if (all[index].name == name) return all[index];
            return null;
        }
    }
}

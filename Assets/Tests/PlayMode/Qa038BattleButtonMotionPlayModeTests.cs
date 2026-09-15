using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 0.3.8 battle feedback acceptance: hover/press/release/exit on the authored battle
    /// controls must animate the single visual container (frame + caption + heart + fx),
    /// never the Button hit rect itself. Dispatches real pointer events; mirrors the
    /// bottom-navigation motion language (1.035x hover + lift, 0.96x press, exact restore).
    /// </summary>
    public sealed class Qa038BattleButtonMotionPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (EventSystem system in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                Object.Destroy(system.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleControlsAnimateVisualGroupWhileHitRectStaysStatic()
        {
            GameObject root = new GameObject("Battle Motion Acceptance", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1536);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            try
            {
                BattleSimulator battle = CreateBattle();
                battle.EnableRealtime(new Dictionary<string, string> { { "player", "魅族" } }, "player");
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), battle, null);
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy);
                Invoke(panel, "RefreshDiceUi");
                Invoke(panel, "RefreshRealtimeCommands");
                // Un-hold one die so the selective reroll button is genuinely usable.
                FindRect(panel.transform, "Dice-0").GetComponent<Button>().onClick.Invoke();
                yield return null;

                // DiceReroll / EnergyReroll / TacticalGuard / AbilityDetails each own one
                // motion-wired visual group; the hit rect must never move.
                AssertMotionPair(panel.transform, "DiceReroll", "SelectedRerollVisualGroup");
                AssertMotionPair(panel.transform, "EnergyReroll", "AllRerollVisualGroup");
                AssertMotionPair(panel.transform, "TacticalGuard", "GuardVisualGroup");
                AssertMotionPair(panel.transform, "AbilityDetails", "AbilityVisualGroup");

                foreach (var pair in new[]
                         {
                             new KeyValuePair<string, string>("DiceReroll", "SelectedRerollVisualGroup"),
                             new KeyValuePair<string, string>("EnergyReroll", "AllRerollVisualGroup"),
                             new KeyValuePair<string, string>("TacticalGuard", "GuardVisualGroup"),
                             new KeyValuePair<string, string>("AbilityDetails", "AbilityVisualGroup"),
                         })
                {
                    RectTransform button = FindRect(panel.transform, pair.Key);
                    RectTransform group = FindRect(panel.transform, pair.Value);
                    Assert.That(button.GetComponent<Button>().IsInteractable(), Is.True,
                        $"{pair.Key} must be usable before feedback can be verified.");
                    Vector3 baseScale = group.localScale;
                    Vector2 basePosition = group.anchoredPosition;
                    Vector2 hitSize = button.sizeDelta;
                    Vector2 hitPosition = button.anchoredPosition;
                    PointerEventData pointer = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    };

                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    yield return WaitRealtime(0.3f);
                    Assert.That(group.localScale.x, Is.GreaterThan(baseScale.x * 1.02f),
                        $"{pair.Key}: hover must visibly enlarge the whole visual group.");
                    Assert.That(group.anchoredPosition.y, Is.GreaterThan(basePosition.y),
                        $"{pair.Key}: hover must lift the visual group.");
                    AssertHitStatic(button, hitSize, hitPosition, pair.Key);

                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    yield return WaitRealtime(0.2f);
                    Assert.That(group.localScale.x, Is.LessThan(baseScale.x * 0.985f),
                        $"{pair.Key}: press must compress the visual group.");
                    AssertHitStatic(button, hitSize, hitPosition, pair.Key);

                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    yield return WaitRealtime(0.35f);
                    Assert.That(group.localScale.x, Is.GreaterThan(baseScale.x),
                        $"{pair.Key}: release over the button must settle back to the hover pose.");

                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    yield return WaitRealtime(0.25f);
                    Assert.That(group.localScale, Is.EqualTo(baseScale),
                        $"{pair.Key}: exit must restore the captured baseline scale exactly.");
                    Assert.That(group.anchoredPosition, Is.EqualTo(basePosition),
                        $"{pair.Key}: exit must restore the captured baseline position exactly.");
                    AssertHitStatic(button, hitSize, hitPosition, pair.Key);
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static void AssertMotionPair(Transform root, string buttonName, string groupName)
        {
            RectTransform button = FindRect(root, buttonName);
            RectTransform group = FindRect(root, groupName);
            Assert.That(button.GetComponent<BattlePolish038ButtonMotion>(), Is.Not.Null,
                $"{buttonName} must carry the motion component.");
            Assert.That(group, Is.Not.SameAs(button),
                $"{groupName} must be a dedicated container, not the button's own rect.");
            Assert.That(group.parent, Is.EqualTo((Transform)button),
                $"{groupName} must sit inside {buttonName} so the whole authored body moves together.");
            Assert.That(group.rect.size, Is.EqualTo(button.rect.size),
                $"{groupName} must cover the full button so frames, captions and effects move as one.");
        }

        private static void AssertHitStatic(RectTransform button, Vector2 size, Vector2 position, string name)
        {
            Assert.That(button.sizeDelta, Is.EqualTo(size), $"{name}: hit rect must never resize.");
            Assert.That(button.anchoredPosition, Is.EqualTo(position), $"{name}: hit rect must never move.");
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static BattleSimulator CreateBattle()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "攻击", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "player", Name = "我方", MaxHp = 1000, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" }
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = 1000, Attack = 50, Defense = 10, Speed = 90,
                SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "qa-038-motion", Name = "动效验收", TurnLimit = 10,
                Enemies = new List<EnemySpawn>
                {
                    new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0, ScalePermille = 1000 }
                }
            };
            manifest.Stages.Add(stage);
            var party = new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "player", Row = 0, Col = 0 }
            };
            return new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }));
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            RectTransform result = root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, $"未找到布局节点：{name}");
            return result;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段：{name}");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到方法：{name}");
            method.Invoke(target, null);
        }
    }
}

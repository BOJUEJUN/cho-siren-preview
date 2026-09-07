using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class AttackTrajectoryPresentationPlayModeTests
    {
        [Test]
        public void PlayerAttackUsesRegisteredStageProxyThenTravelsToTheActualEnemy()
        {
            GameObject owner = new GameObject("Trajectory stage", typeof(RectTransform));
            Texture2D texture = new Texture2D(2, 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            try
            {
                RectTransform stage = ConfigureStage(owner);
                var cues = owner.AddComponent<AttackTrajectoryPresentation>();
                cues.Configure(stage, () => false, () => 1);
                RectTransform enemy = NewRect("Enemy", stage, new Vector2(320, -240));
                RectTransform remoteCard = NewRect("PlayerCardBelowDice", stage, new Vector2(430, -1200));
                cues.SetPlayerProxy(2, sprite, "雾白");
                Assert.That(cues.Play(remoteCard, enemy, sprite, "雾白", "噪声幽灵", Color.cyan, false, false, 2), Is.True);
                Transform flight = stage.Find("AttackTrajectoryLayer/AttackTrajectory-0");
                RectTransform launch = flight.Find("AttackLaunch").GetComponent<RectTransform>();
                Assert.That(launch.anchoredPosition, Is.EqualTo(new Vector2(450, -624)),
                    "我方出手必须从舞台内对应阵位出发，不能从骰子下方真实卡片连长线。");
                Assert.That(flight.Find("AttackArrow").gameObject.activeSelf, Is.False);
                cues.AdvancePresentation(.2f);
                RectTransform arrow = flight.Find("AttackArrow").GetComponent<RectTransform>();
                Assert.That(arrow.gameObject.activeSelf, Is.True);
                Assert.That(arrow.anchoredPosition.x, Is.InRange(320f, 450f));
                Assert.That(arrow.anchoredPosition.y, Is.InRange(-624f, -240f));
                cues.AdvancePresentation(.25f);
                RectTransform impact = flight.Find("AttackImpact").GetComponent<RectTransform>();
                Assert.That(impact.gameObject.activeSelf, Is.True);
                Assert.That(impact.anchoredPosition, Is.EqualTo(new Vector2(320, -240)));
                Assert.That(flight.Find("AttackCausalLabel").GetComponent<Text>().text, Is.EqualTo("雾白 攻击 噪声幽灵"));
                Image proxy = stage.Find("AttackTrajectoryLayer/AttackPlayerProxy-2/AttackProxyPortrait").GetComponent<Image>();
                Assert.That(proxy.sprite, Is.SameAs(sprite));
                Assert.That(proxy.sprite.texture, Is.SameAs(texture), "代理头像只引用传入素材，不改原图。");
                Assert.That(stage.Find("AttackTrajectoryLayer").GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(stage.Find("AttackTrajectoryLayer").GetComponentsInChildren<Graphic>(true)
                    .All(graphic => !graphic.raycastTarget), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EnemyAttackClearlyLandsOnTheSpecifiedPlayerAndFreezesWithPause()
        {
            GameObject owner = new GameObject("Enemy causal target stage", typeof(RectTransform));
            bool paused = false;
            int speed = 1;
            try
            {
                RectTransform stage = ConfigureStage(owner);
                var cues = owner.AddComponent<AttackTrajectoryPresentation>();
                cues.Configure(stage, () => paused, () => speed);
                cues.SetPlayerProxy(3, null, "夜莺");
                RectTransform enemy = NewRect("Enemy", stage, new Vector2(200, -220));
                RectTransform card = NewRect("PlayerCard", stage, new Vector2(550, -1300));
                cues.Play(enemy, card, null, "回响无人机", "夜莺", Color.red, true, false, 3);
                cues.AdvancePresentation(.2f);
                Transform flight = stage.Find("AttackTrajectoryLayer/AttackTrajectory-0");
                RectTransform arrow = flight.Find("AttackArrow").GetComponent<RectTransform>();
                Vector2 frozen = arrow.anchoredPosition;
                paused = true;
                cues.AdvancePresentation(8f);
                Assert.That(arrow.anchoredPosition, Is.EqualTo(frozen));
                Assert.That(cues.ActiveCount, Is.EqualTo(1));
                paused = false;
                speed = 2;
                cues.AdvancePresentation(.12f);
                RectTransform impact = flight.Find("AttackImpact").GetComponent<RectTransform>();
                Assert.That(impact.gameObject.activeSelf, Is.True);
                Assert.That(impact.anchoredPosition, Is.EqualTo(new Vector2(630, -624)),
                    "敌人的命中应落到实际受击队员的第四阵位，而不是固定首位。");
                Assert.That(card.anchoredPosition, Is.EqualTo(new Vector2(550, -1300)),
                    "表现组件不能移动原始玩家卡片或战斗布局。");
                cues.AdvancePresentation(.2f);
                Assert.That(cues.ActiveCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void HeavyAndBasicAttacksHaveDistinctTravelAndBoundedNoBacklogLifecycle()
        {
            GameObject owner = new GameObject("Trajectory capacity stage", typeof(RectTransform));
            try
            {
                RectTransform stage = ConfigureStage(owner);
                var cues = owner.AddComponent<AttackTrajectoryPresentation>();
                cues.Configure(stage, () => false, () => 1);
                RectTransform enemy = NewRect("Enemy", stage, new Vector2(360, -220));
                for (int index = 0; index < AttackTrajectoryPresentation.Capacity; index++)
                    Assert.That(cues.Play(null, enemy, null, "队员", "怪物", Color.cyan, false,
                        index % 2 != 0, index % 4), Is.True);
                Assert.That(cues.Play(null, enemy, null, "额外队员", "怪物", Color.cyan, false, false), Is.False);
                Assert.That(cues.ActiveCount, Is.EqualTo(AttackTrajectoryPresentation.Capacity));
                cues.AdvancePresentation(.45f);
                Assert.That(stage.Find("AttackTrajectoryLayer/AttackTrajectory-0/AttackImpact").gameObject.activeSelf, Is.True);
                Assert.That(stage.Find("AttackTrajectoryLayer/AttackTrajectory-1/AttackArrow").gameObject.activeSelf, Is.True,
                    "重击有更长蓄力与飞行，不能与普通攻击表现完全一样。");
                cues.Cancel();
                cues.AdvancePresentation(3f);
                Assert.That(cues.ActiveCount, Is.Zero);
                Assert.That(stage.Find("AttackTrajectoryLayer").gameObject.activeSelf, Is.False,
                    "取消后不能排队补播已经过去的攻击。");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void OnlyTheActualAttackerAndVictimMoveWhileOtherPlayerPortraitsStayStill()
        {
            GameObject owner = new GameObject("Performer causality test", typeof(RectTransform));
            try
            {
                var motions = new EnemyUnitPresentation[4];
                for (int index = 0; index < motions.Length; index++)
                {
                    RectTransform rig = NewRect("PlayerMotion-" + index, owner.transform, Vector2.zero);
                    Image image = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(rig, false);
                    motions[index] = rig.gameObject.AddComponent<EnemyUnitPresentation>();
                    motions[index].Configure(image, () => false, () => 1, "performer-charm");
                    motions[index].AdvancePresentation(.5f);
                    Assert.That(rig.anchoredPosition, Is.EqualTo(Vector2.zero), "无实际事件的队员不能一直作出攻击般摇晃。");
                }
                motions[1].PlayAttack(false);
                motions[3].PlayHit(true);
                foreach (EnemyUnitPresentation motion in motions) motion.AdvancePresentation(.1f);
                Assert.That(motions[1].GetComponent<RectTransform>().anchoredPosition, Is.Not.EqualTo(Vector2.zero));
                Assert.That(motions[3].GetComponent<RectTransform>().anchoredPosition, Is.Not.EqualTo(Vector2.zero));
                Assert.That(motions[0].GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(motions[2].GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static RectTransform ConfigureStage(GameObject owner)
        {
            RectTransform stage = owner.GetComponent<RectTransform>();
            stage.pivot = new Vector2(0, 1);
            stage.sizeDelta = new Vector2(720, 700);
            return stage;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(100, 100);
            rect.anchoredPosition = position;
            return rect;
        }
    }
}

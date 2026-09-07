using System;
using System.Collections;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BattlePresentationRegressionTests
    {
        [UnityTest]
        public IEnumerator BossHitFeedbackDoesNotCancelChargeOrPhaseTelegraphs()
        {
            GameObject root = new GameObject("Boss feedback priority regression", typeof(RectTransform));
            try
            {
                BossBattlePresentation boss = CreateBoss(root, () => false, () => 1);
                Text label = root.transform.Find("State").GetComponent<Text>();
                Image echo = root.transform.Find("Rig/Echo").GetComponent<Image>();
                boss.PlayCharge("重击");
                string chargeLabel = label.text;
                for (int hit = 0; hit < 5; hit++) boss.PlayHit(17 + hit, hit % 2 == 0);
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Charging));
                Assert.That(label.text, Is.EqualTo(chargeLabel), "受击不能把蓄力标签替换成受击标签。");
                Assert.That(echo.color.a, Is.GreaterThan(0f), "保留蓄力时也必须有独立命中闪光。");
                Assert.That(boss.HitReactionCount, Is.EqualTo(5));
                Assert.That(root.transform.Find("Damage").gameObject.activeSelf, Is.True);
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Charging));

                boss.PlayPhaseSurge(3);
                string phaseLabel = label.text;
                boss.PlayCharge("不应盖住转阶段");
                boss.PlayHit(99, true);
                Assert.That(label.text, Is.EqualTo(phaseLabel), "转阶段优先于普通蓄力和命中反馈。");
                yield return new WaitForSecondsRealtime(1.12f);
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Idle));
                Assert.That(echo.color.a, Is.EqualTo(0f));

                boss.PlayPhaseSurge(2);
                boss.PlayHit(10, false);
                boss.PlayOutcome(true);
                boss.PlayHit(10, true);
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Defeated),
                    "结束战斗应立即抢占转阶段，且不能被后续命中事件复活。");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator BossMinorHitAndPrimaryPoseFreezeDuringPauseAndResumeAtCurrentSpeed()
        {
            GameObject root = new GameObject("Boss pause and speed regression", typeof(RectTransform));
            bool paused = false;
            int speed = 1;
            try
            {
                BossBattlePresentation boss = CreateBoss(root, () => paused, () => speed);
                boss.PlayCharge("重击");
                boss.PlayHit(120, true);
                yield return null;
                paused = true;
                Image echo = root.transform.Find("Rig/Echo").GetComponent<Image>();
                Vector2 position = boss.MotionRoot.anchoredPosition;
                Vector3 scale = boss.MotionRoot.localScale;
                Color echoColor = echo.color;
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(boss.MotionRoot.anchoredPosition, Is.EqualTo(position));
                Assert.That(boss.MotionRoot.localScale, Is.EqualTo(scale));
                Assert.That(echo.color, Is.EqualTo(echoColor));
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Charging));

                paused = false;
                speed = 2;
                yield return new WaitForSecondsRealtime(0.95f);
                Assert.That(boss.State, Is.EqualTo(BossBattlePresentation.BossVisualState.Idle),
                    "恢复后改成2倍速，已在播放的蓄力也必须按新速度结束。");
                Assert.That(echo.color.a, Is.EqualTo(0f));
                Assert.That(root.transform.Find("Charge").gameObject.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator BossIdleClockUsesSamePauseAndSpeedAsActionClock()
        {
            GameObject root = new GameObject("Boss idle clock regression", typeof(RectTransform));
            bool paused = false;
            int speed = 2;
            try
            {
                BossBattlePresentation boss = CreateBoss(root, () => paused, () => speed);
                yield return null;
                FieldInfo clock = typeof(BossBattlePresentation).GetField("animationClock",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = typeof(BossBattlePresentation).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                float before = (float)clock.GetValue(boss);
                update.Invoke(boss, null);
                Assert.That((float)clock.GetValue(boss) - before,
                    Is.EqualTo(Time.unscaledDeltaTime * 2f).Within(0.0001f),
                    "待机光环/呼吸不能仍停在1倍而动作使用2倍。");
                paused = true;
                before = (float)clock.GetValue(boss);
                update.Invoke(boss, null);
                Assert.That((float)clock.GetValue(boss), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        private static BossBattlePresentation CreateBoss(GameObject root, Func<bool> paused, Func<int> speed)
        {
            RectTransform rig = new GameObject("Rig", typeof(RectTransform)).GetComponent<RectTransform>();
            rig.SetParent(root.transform, false);
            Image portrait = NewImage("Portrait", rig);
            Image echo = NewImage("Echo", rig);
            Text state = NewText("State", root.transform);
            Text damage = NewText("Damage", root.transform);
            var boss = root.AddComponent<BossBattlePresentation>();
            boss.Configure(rig, portrait, echo, NewImage("Back", root.transform),
                NewImage("Core", root.transform), NewImage("Shadow", root.transform),
                NewImage("Pulse", root.transform), NewImage("Warning", root.transform),
                NewImage("HitSlash", root.transform), NewImage("HeartImpact", root.transform),
                NewImage("Charge", root.transform), state, new Image[0], new Image[0],
                new[] { damage }, paused, speed);
            return boss;
        }

        private static Image NewImage(string name, Transform parent)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<Image>();
            image.transform.SetParent(parent, false);
            return image;
        }

        private static Text NewText(string name, Transform parent)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            text.transform.SetParent(parent, false);
            return text;
        }
    }
}

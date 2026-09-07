using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class SkillCutInPresentationPlayModeTests
    {
        [Test]
        public void CutInUsesSeparateReadableLinesAndNeverInterceptsBattleInput()
        {
            GameObject root = new GameObject("Skill cut-in layout test", typeof(RectTransform));
            try
            {
                var cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => false, () => 1);
                cutIn.Enqueue(null, "星璃", "终曲·魅声压制", Color.magenta);
                cutIn.AdvancePresentation(.2f);
                RectTransform banner = root.transform.Find("SkillCutInBanner").GetComponent<RectTransform>();
                Text actor = banner.Find("SkillCutInActor").GetComponent<Text>();
                Text skill = banner.Find("SkillCutInSkill").GetComponent<Text>();
                Assert.That(actor.text, Is.EqualTo("星璃"));
                Assert.That(skill.text, Is.EqualTo("终曲·魅声压制"));
                Assert.That(actor.rectTransform.anchoredPosition.y - actor.rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(skill.rectTransform.anchoredPosition.y),
                    "角色名和技能名必须独立两行，不挤进一条即时日志。");
                Assert.That(banner.anchoredPosition.y, Is.EqualTo(-735f));
                Assert.That(-banner.anchoredPosition.y + banner.rect.height, Is.LessThan(840f),
                    "横幅不能压住骰子与战斗控制台。");
                Assert.That(banner.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(banner.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(banner.GetComponentInChildren<Button>(true), Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BoundedQueueKeepsVisibleSkillAndDropsOldestWaitingSkills()
        {
            GameObject root = new GameObject("Skill cut-in queue test", typeof(RectTransform));
            try
            {
                var cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => false, () => 1);
                for (int index = 0; index < 9; index++) cutIn.Enqueue(null, "成员", "技能" + index, Color.cyan);
                Text label = root.transform.Find("SkillCutInBanner/SkillCutInSkill").GetComponent<Text>();
                Assert.That(cutIn.PendingCount, Is.EqualTo(4));
                Assert.That(label.text, Is.EqualTo("技能0"), "当前正在阅读的技能不能被新事件覆盖。");
                cutIn.AdvancePresentation(1.5f);
                Assert.That(label.text, Is.EqualTo("技能5"), "积压时丢最旧的等待项，保留最近四次大招。");
                Assert.That(cutIn.PendingCount, Is.EqualTo(3));
                cutIn.AdvancePresentation(7f);
                Assert.That(cutIn.PendingCount, Is.Zero);
                Assert.That(cutIn.IsShowing, Is.False, "超过六秒的旧技能不能在稍后继续冒出。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PauseFreezesSlideAndEvenDoubleSpeedKeepsAtLeastPoint65SecondsToRead()
        {
            GameObject root = new GameObject("Skill cut-in timing test", typeof(RectTransform));
            bool paused = false;
            try
            {
                var cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => paused, () => 2);
                cutIn.Enqueue(null, "雾白", "深海庇护", Color.cyan);
                cutIn.AdvancePresentation(.12f);
                RectTransform banner = root.transform.Find("SkillCutInBanner").GetComponent<RectTransform>();
                CanvasGroup alpha = banner.GetComponent<CanvasGroup>();
                Assert.That(alpha.alpha, Is.EqualTo(1f));
                Vector2 shownPosition = banner.anchoredPosition;
                paused = true;
                cutIn.AdvancePresentation(5f);
                Assert.That(banner.anchoredPosition, Is.EqualTo(shownPosition));
                Assert.That(alpha.alpha, Is.EqualTo(1f));
                Assert.That(cutIn.IsShowing, Is.True);
                paused = false;
                cutIn.AdvancePresentation(.65f);
                Assert.That(alpha.alpha, Is.EqualTo(1f), "战斗2倍速也不能让文字一闪而过。");
                cutIn.AdvancePresentation(.2f);
                Assert.That(cutIn.IsShowing, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CancelClearsCurrentAndQueuedSkillsWithoutLateReappearance()
        {
            GameObject root = new GameObject("Skill cut-in cancellation test", typeof(RectTransform));
            try
            {
                var cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => false, () => 1);
                cutIn.Enqueue(null, "星璃", "终曲", Color.magenta);
                cutIn.Enqueue(null, "夜莺", "追猎", Color.yellow);
                cutIn.Cancel();
                cutIn.AdvancePresentation(3f);
                Assert.That(cutIn.IsShowing, Is.False);
                Assert.That(cutIn.PendingCount, Is.Zero);
                Assert.That(root.transform.Find("SkillCutInBanner").gameObject.activeSelf, Is.False);
                cutIn.Enqueue(null, "绯音", "新战斗技能", Color.red);
                Assert.That(cutIn.IsShowing, Is.True, "取消表现不应破坏组件后续使用。");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

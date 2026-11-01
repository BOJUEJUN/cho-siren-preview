using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class DiceComboCompactPlayModeTests
    {
        private bool hadReduced;
        private int reduced;
        private GameObject owner;
        private RectTransform layer, hand, member;
        private DiceComboShow cue;
        private BattleAttackBadge badge;
        private Font font;
        private bool paused;

        [SetUp]
        public void SetUp()
        {
            hadReduced = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduced = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            owner = new GameObject("CompactComboFixture", typeof(RectTransform));
            layer = owner.GetComponent<RectTransform>();
            layer.sizeDelta = new Vector2(720f, 1536f);
            hand = Rect("HandReadout", layer, new Vector2(0f, -500f), new Vector2(440f, 52f));
            member = Rect("MemberHpAndPortrait", layer, new Vector2(0f, -200f), new Vector2(600f, 220f));
            font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cue = DiceComboShow.Attach(layer, () => paused, () => .1f);
            badge = BattleAttackBadge.Create(member, font, () => paused);
            badge.SetTarget(100, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            if (hadReduced) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduced);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [TestCase("一对")]
        [TestCase("两对")]
        public void WeakHandsReleaseTheRealAttackBadgeWithoutCreatingAStageOverlay(string name)
        {
            badge.Hold();
            badge.SetTarget(115, true);
            int releases = 0;
            cue.Play(name, 1.15f, 1, font, new[] { hand }, new[] { member }, target =>
            {
                Assert.That(target, Is.EqualTo(0));
                releases++;
                badge.Release();
            }, hand);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(badge.Held, Is.False, "No skipped banner may strand the actual attack value in Hold.");
            Assert.That(cue.IsPlaying, Is.False);
            Assert.That(layer.Find("DiceComboShow"), Is.Null);
        }

        [TestCase("三条", 2, 1f)]
        [TestCase("双三条", 3, .84f)]
        [TestCase("六连顺", 4, .84f)]
        public void StrongHandStaysOnItsReadoutAndKeepsTheWholeTitleClearOfThePortrait(string name, int tier, float scale)
        {
            hand.localScale = Vector3.one * scale;
            badge.Hold();
            int releases = 0;
            cue.Play(name, 1.45f, tier, font, new[] { hand }, new[] { member }, target =>
            { releases++; badge.Release(); }, hand);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(badge.Held, Is.False);
            Assert.That(cue.IsPlaying, Is.True);
            RectTransform visual = layer.Find("DiceComboShow").GetComponent<RectTransform>();
            Assert.That(visual.anchoredPosition, Is.EqualTo(hand.anchoredPosition));
            Assert.That(visual.rect.width, Is.LessThanOrEqualTo(hand.rect.width * scale + .01f));
            Assert.That(visual.rect.height, Is.LessThanOrEqualTo(hand.rect.height * scale + .01f));
            Assert.That(Bounds(visual).Overlaps(Bounds(member)), Is.False,
                "A hand cue must not cover the member portrait or its HP readout.");
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name == "Flash" || child.name == "Shard" || child.name == "BackStrip"), Is.False);
            Assert.That(visual.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Text title = visual.Find("Title").GetComponent<Text>();
            Assert.That(title.text, Is.EqualTo(name + " ×1.45"));
            Assert.That(title.cachedTextGenerator.Populate(title.text,
                title.GetGenerationSettings(title.rectTransform.rect.size)), Is.True);
            Assert.That(title.cachedTextGenerator.lineCount, Is.EqualTo(1), "The short title must not wrap into a clipped second row.");
            Assert.That(title.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(title.text.Length),
                "Every title and multiplier character must fit in the local cue.");
            Vector2 before = visual.anchoredPosition;
            Invoke("Advance", .3f);
            Assert.That(visual.anchoredPosition, Is.EqualTo(before), "A compact cue only fades; it does not shake the stage.");
        }

        [Test]
        public void AWeakNewHandRemovesThePreviousStrongCueWithoutLeavingAHeldBadge()
        {
            cue.Play("六同", 2f, 4, font, new[] { hand }, new[] { member }, target => badge.Release(), hand);
            Invoke("Advance", .3f);
            Assert.That(cue.IsPlaying, Is.True);
            badge.Hold();
            cue.Play("一对", 1.05f, 1, font, new[] { hand }, new[] { member }, target => badge.Release(), hand);
            Assert.That(cue.IsPlaying, Is.False);
            Assert.That(badge.Held, Is.False);
            Assert.That(layer.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name == "DiceComboShow" && child.gameObject.activeSelf), Is.False);
        }

        [Test]
        public void PauseAndCancelNeverRepeatTheAttackBadgeRelease()
        {
            int releases = 0;
            cue.Play("六同", 2f, 4, font, new[] { hand }, new[] { member }, target => releases++, hand);
            Invoke("Advance", .3f);
            RectTransform visual = layer.Find("DiceComboShow").GetComponent<RectTransform>();
            paused = true;
            Invoke("Update");
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(cue.IsPlaying, Is.True);
            paused = false;
            Invoke("Update");
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Invoke("Advance", 1f);
            Assert.That(cue.IsPlaying, Is.False);
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void ReducedMotionReleasesTheBadgeWithoutShowingAHandOverlay()
        {
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1);
            badge.Hold();
            cue.Play("六同", 2f, 4, font, new[] { hand }, new[] { member }, target => badge.Release(), hand);
            Assert.That(badge.Held, Is.False);
            Assert.That(cue.IsPlaying, Is.False);
            Assert.That(layer.Find("DiceComboShow"), Is.Null);
        }

        private void Invoke(string method, params object[] values) => typeof(DiceComboShow)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cue, values);

        private static RectTransform Rect(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return rect;
        }

        private Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = layer.InverseTransformPoint(corners[0]);
            Vector2 max = layer.InverseTransformPoint(corners[2]);
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class HomeMinimalControls1019Tests
    {
        [Test]
        public void MusicRecordTurnsAtEighteenDegreesPerSecondAndReducedMotionStopsIt()
        {
            bool had = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            int before = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            var disc = new GameObject("QuietHomeRecord", typeof(RectTransform));
            var audioHost = new GameObject("QuietRecordAudio");
            try
            {
                GameAudio audio = audioHost.AddComponent<GameAudio>();
                audio.Initialize(new GameModel(() => System.DateTime.Now, new InMemorySaveStore()));
                audio.SetMusicEnabled(true);
                BattlePresentationOptions.ReduceMotion = false;
                var rect = disc.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(148, 148);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(20, -30);
                var oldCorners = new Vector3[4]; rect.GetWorldCorners(oldCorners);
                HomeMusicDiscSpin spin = disc.AddComponent<HomeMusicDiscSpin>();
                spin.Bind(audio);
                var newCorners = new Vector3[4]; rect.GetWorldCorners(newCorners);
                for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(oldCorners[i], newCorners[i]), Is.LessThan(.001f));
                Quaternion start = rect.localRotation;
                spin.AdvancePresentation(1f);
                Quaternion second = rect.localRotation;
                Assert.That(Quaternion.Angle(start, second), Is.EqualTo(18f).Within(.01f));
                for (int i = 0; i < 20; i++) spin.AdvancePresentation(.05f);
                Assert.That(Quaternion.Angle(second, rect.localRotation), Is.EqualTo(18f).Within(.01f),
                    "The rate must be steady rather than ramping toward 150 degrees per second");
                BattlePresentationOptions.ReduceMotion = true;
                Quaternion still = rect.localRotation;
                spin.AdvancePresentation(10f);
                Assert.That(rect.localRotation, Is.EqualTo(still));
            }
            finally
            {
                Object.DestroyImmediate(disc);
                Object.DestroyImmediate(audioHost);
                BattlePresentationOptions.ReduceMotion = before == 1;
                if (!had) PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            }
        }

        [Test]
        public void UnboundHomeCaptionContainsOnlyTheApprovedSongTitle()
        {
            var caption = new GameObject("QuietSongCaption", typeof(RectTransform), typeof(Text));
            try
            {
                Text text = caption.GetComponent<Text>();
                caption.AddComponent<HomeMusicSwitcher>().Bind(null, text);
                Assert.That(text.text, Is.EqualTo(HomeMusicPlaylist.Get(0).Title));
                Assert.That(text.text, Does.Not.Contain("/"));
                Assert.That(text.text, Does.Not.Contain("♪"));
            }
            finally { Object.DestroyImmediate(caption); }
        }

        [UnityTest]
        public IEnumerator CharacterArrowHasNoPinkPlateAndItsChevronRespondsWithoutMovingTheHitArea()
        {
            var owner = new GameObject("InactiveArrowBuilder", typeof(RectTransform));
            owner.SetActive(false);
            var host = new GameObject("ActiveArrowFixture", typeof(RectTransform));
            try
            {
                var motion = owner.AddComponent<PsdHomeMotion>();
                var carousel = owner.AddComponent<PsdHomeCharacterCarousel>();
                typeof(PsdHomeCharacterCarousel).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(carousel, motion);
                MethodInfo build = typeof(PsdHomeCharacterCarousel).GetMethod("Arrow", BindingFlags.NonPublic | BindingFlags.Instance);
                var button = (Button)build.Invoke(carousel, new object[] { "NextCharacter", 1, 648f, null, null });
                button.transform.SetParent(host.transform, false);
                RectTransform hit = button.GetComponent<RectTransform>();
                Vector2 position = hit.anchoredPosition;
                Assert.That(hit.sizeDelta, Is.EqualTo(new Vector2(60, 78)), "Keep the approved original character-switch hit area");
                foreach (Image image in button.GetComponentsInChildren<Image>(true))
                    Assert.That(image.color.a, Is.Zero, "Only the chevron may draw; no pink or black square plate");
                Assert.That(button.GetComponentsInChildren<Shadow>(true), Is.Empty);
                var glyph = button.GetComponentInChildren<PsdHomeChevron>();
                RectTransform visual = (RectTransform)glyph.transform.parent;
                Color rest = glyph.color;
                Assert.That(rest.a, Is.InRange(.5f, .8f));
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(glyph.color.a, Is.GreaterThan(rest.a));
                Assert.That(visual.localScale.x, Is.GreaterThan(1f));
                Assert.That(hit.anchoredPosition, Is.EqualTo(position));
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(visual.localScale.x, Is.LessThan(1f));
                Assert.That(hit.anchoredPosition, Is.EqualTo(position));
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(Vector3.Distance(visual.localScale, Vector3.one), Is.LessThan(.001f));
                Assert.That(glyph.color.a, Is.EqualTo(rest.a).Within(.001f));
                Assert.That(hit.anchoredPosition, Is.EqualTo(position));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(owner); }
        }
    }
}

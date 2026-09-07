using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class DiceRollPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator RollsAuthoredFacesAndLandsOnLatestModelResultWithoutMovingButton()
        {
            GameObject button = new GameObject("DiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Texture2D texture = new Texture2D(2, 2);
            Sprite[] faces = MakeFaces(texture);
            try
            {
                DiceRollPresentation animation = Create(button.transform, out Image face, out RectTransform rig);
                Vector2 buttonPosition = button.GetComponent<RectTransform>().anchoredPosition;
                animation.Configure(face, rig, () => false, () => 1);
                animation.SetRestingFace(faces[0], 1, false);
                animation.PlayRoll(faces[4], 5, faces, 0, true);
                animation.AdvancePresentation(.12f);
                Sprite processFace = face.sprite;
                Vector3 processScale = rig.localScale;
                Assert.That(animation.IsRolling, Is.True);
                Assert.That(rig.anchoredPosition.y, Is.GreaterThan(0f));
                Assert.That(rig.localScale.x, Is.LessThan(1f));
                animation.SetRestingFace(faces[4], 5, true);
                Assert.That(face.sprite, Is.SameAs(processFace), "UI refresh cannot replace rolling faces.");
                Assert.That(rig.localScale, Is.EqualTo(processScale));
                animation.PlayRoll(faces[2], 3, faces, 0, false);
                Assert.That(animation.Elapsed, Is.EqualTo(.12f).Within(.0001f), "Rapid rerolls do not restart animation.");
                animation.AdvancePresentation(.6f);
                Assert.That(animation.IsRolling, Is.False);
                Assert.That(face.sprite, Is.SameAs(faces[2]));
                Assert.That(animation.FinalValue, Is.EqualTo(3));
                Assert.That(rig.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rig.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(button.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(buttonPosition));
                Assert.That(button.GetComponent<Button>().interactable, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(button);
                foreach (Sprite sprite in faces) Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator PausesStaggersScalesSpeedAndCancelsWithoutOldFaceReplay()
        {
            GameObject button = new GameObject("DiceButton", typeof(RectTransform));
            Texture2D texture = new Texture2D(2, 2);
            Sprite[] faces = MakeFaces(texture);
            bool paused = false;
            int speed = 1;
            try
            {
                DiceRollPresentation animation = Create(button.transform, out Image face, out RectTransform rig);
                animation.Configure(face, rig, () => paused, () => speed);
                animation.SetRestingFace(faces[0], 1, false);
                animation.PlayRoll(faces[5], 6, faces, 4, true);
                animation.AdvancePresentation(.1f);
                Assert.That(face.sprite, Is.SameAs(faces[0]), "Ordinal delay preserves old face before this die starts.");
                paused = true;
                animation.AdvancePresentation(2f);
                Assert.That(animation.Elapsed, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(rig.anchoredPosition, Is.EqualTo(Vector2.zero));
                paused = false;
                speed = 2;
                animation.AdvancePresentation(.36f);
                Assert.That(animation.IsRolling, Is.False);
                Assert.That(face.sprite, Is.SameAs(faces[5]));
                animation.PlayRoll(faces[3], 4, faces, 0, false);
                animation.AdvancePresentation(.1f);
                animation.gameObject.SetActive(false);
                Assert.That(animation.IsRolling, Is.False);
                Assert.That(face.sprite, Is.SameAs(faces[3]));
                Assert.That(rig.localScale, Is.EqualTo(Vector3.one));
                animation.gameObject.SetActive(true);
                animation.SetRestingFace(faces[1], 2, false);
                animation.AdvancePresentation(5f);
                Assert.That(face.sprite, Is.SameAs(faces[1]), "Cancelled rolls cannot overwrite new resting state.");
                foreach (Image effect in rig.GetComponentsInChildren<Image>())
                    if (effect != face) Assert.That(effect.raycastTarget, Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(button);
                foreach (Sprite sprite in faces) Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        private static Sprite[] MakeFaces(Texture2D texture)
        {
            Sprite[] result = new Sprite[6];
            for (int i = 0; i < result.Length; i++)
                result[i] = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            return result;
        }

        private static DiceRollPresentation Create(Transform button, out Image face, out RectTransform rig)
        {
            GameObject motion = new GameObject("DiceMotion", typeof(RectTransform));
            motion.transform.SetParent(button, false);
            rig = motion.GetComponent<RectTransform>();
            GameObject art = new GameObject("DiceFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.transform.SetParent(motion.transform, false);
            face = art.GetComponent<Image>();
            return motion.AddComponent<DiceRollPresentation>();
        }
    }
}

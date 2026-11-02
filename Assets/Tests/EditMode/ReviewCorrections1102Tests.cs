using System;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class ReviewCorrections1102Tests
    {
        [Test]
        public void TicketSigningIsExplicitAtomicAndCannotSilentlyChargeDiamonds()
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => new DateTime(2026, 11, 2, 12, 0, 0), store);
            int member = model.InterviewCandidates(0)[0];
            int quote = model.InterviewQuote(0, member);
            string cycle = model.CurrentInterviewCycle;
            model.Save.RecruitTickets = 1;
            model.Save.Diamonds = 0;
            Assert.That(model.SignInterviewCandidate(0, member, cycle, quote + 1, true, out _), Is.False);
            Assert.That(model.Save.RecruitTickets, Is.EqualTo(1));
            Assert.That(model.SignInterviewCandidate(0, member, cycle, quote, true, out string message), Is.True, message);
            Assert.That(model.Save.RecruitTickets, Is.Zero);
            Assert.That(model.Save.Diamonds, Is.Zero);
            Assert.That(model.IsUnlocked(member), Is.True);
            Assert.That(new GameModel(() => new DateTime(2026, 11, 2, 12, 0, 0), store).IsUnlocked(member), Is.True);
            int next = model.InterviewCandidates(0)[0];
            model.Save.Diamonds = 10000;
            Assert.That(model.SignInterviewCandidate(0, next, cycle, model.InterviewQuote(0, next), true, out _), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(10000), "a missing ticket never silently charges diamonds");
            Assert.That(model.IsUnlocked(next), Is.False);
            model.Save.RecruitTickets = 1;
            int nextQuote = model.InterviewQuote(0, next);
            Assert.That(model.SignInterviewCandidate(0, next, cycle, nextQuote, false, out message), Is.True, message);
            Assert.That(model.Save.RecruitTickets, Is.EqualTo(1), "explicit diamond payment preserves tickets");
            Assert.That(model.Save.Diamonds, Is.EqualTo(10000 - nextQuote));
        }

        [Test]
        public void CurrentAvatarImportsUseTheirActualCatalogFace()
        {
            foreach (var member in GameModel.Members)
            {
                Sprite art = Resources.Load<Sprite>(member.ResourcePath);
                if (art == null) continue;
                bool found = PortraitFace1101.TryGetFace(member.Id, art, out Rect face);
                Assert.That(found, Is.True, member.ResourcePath);
                if (PortraitFace1101.TryMemberFace(member.Id, out Rect expected, out _))
                    Assert.That(face, Is.EqualTo(expected), member.ResourcePath);
                Sprite crop = PortraitFace1101.CropSprite(member.Id, art, 1f);
                Vector2 facePixel = art.textureRect.position - art.textureRectOffset +
                    Vector2.Scale(face.center, art.rect.size);
                Assert.That(crop.rect.Contains(facePixel), Is.True, member.ResourcePath + " must contain the original face after alpha trimming");
            }
        }

        [Test]
        public void ImportedPortraitSliceNameUsesTheMembersRealFaceAnchor()
        {
            Texture2D texture = new Texture2D(512, 704) { name = "portrait" };
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 512, 704), Vector2.one * .5f);
            sprite.name = "portrait_0";
            try
            {
                Assert.That(PortraitFace1101.TryGetFaceByPath("Art/Members/hero-0002/portrait", out Rect expected), Is.True);
                Assert.That(PortraitFace1101.TryGetFace("hero-0002", sprite, out Rect face), Is.True);
                Assert.That(face, Is.EqualTo(expected));
                Rect crop = PortraitFace1101.UvFor("hero-0002", sprite, 1f);
                Assert.That(crop.Contains(face.center), Is.True, "the avatar contains the actual face, including seated poses");
                Assert.That(crop.height, Is.LessThan(.3f));
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}

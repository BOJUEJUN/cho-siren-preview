using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class StarterMemberAvatarTests
    {
        private static readonly string[] StarterIds = { "xingli", "feiyin", "wubai", "yeying" };

        private static readonly Vector2[] OriginalPortraitSizes = { new Vector2(995, 2048), new Vector2(881, 2048), new Vector2(860, 2048), new Vector2(1164, 2048) };

        [Test]
        public void StarterRosterCardsUseAuthoredHeadAvatars()
        {
            for (int index = 0; index < StarterIds.Length; index++)
            {
                MemberDefinition member = GameModel.Members[index];
                Assert.That(member.Id, Is.EqualTo(StarterIds[index]));
                Assert.That(member.ThumbnailResourcePath,
                    Is.EqualTo("Art/Members/Avatars/avatar-" + member.Id));

                Sprite avatar = Resources.Load<Sprite>(member.ThumbnailResourcePath);
                Assert.That(avatar, Is.Not.Null, member.ThumbnailResourcePath);
                // PanelKit frames sprites named avatar-* whole (head at the top) instead of
                // through the full-body bust table.
                Assert.That(avatar.name, Does.StartWith("avatar-"));
                Assert.That(avatar.rect.size, Is.EqualTo(new Vector2(512f, 768f)),
                    $"{member.Id} 头像需按原尺寸导入");

                Sprite portrait = Resources.Load<Sprite>(member.ResourcePath);
                Assert.That(portrait, Is.Not.Null, member.ResourcePath);
                Assert.That(portrait.name, Does.Not.StartWith("avatar-"));
                Assert.That(portrait.rect.size, Is.EqualTo(OriginalPortraitSizes[index]),
                    $"{member.Id} 全身立绘需按原尺寸导入");
            }
        }
    }
}

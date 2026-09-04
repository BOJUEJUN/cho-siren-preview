using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class MemberCatalogIntegrationTests
    {
        [Test]
        public void RuntimeCatalogContainsAtLeastFiftyUniqueMembersAndKeepsLegacyOrder()
        {
            Assert.That(GameModel.Members.Length, Is.GreaterThanOrEqualTo(50));

            string[] legacyIds =
            {
                "xingli", "feiyin", "wubai", "yeying", "yaoguang",
                "hupo", "xianyue", "chuxue", "chengxia"
            };
            for (int index = 0; index < legacyIds.Length; index++)
                Assert.That(GameModel.Members[index].Id, Is.EqualTo(legacyIds[index]));

            var ids = new HashSet<string>();
            var portraits = new HashSet<string>();
            var thumbnails = new HashSet<string>();
            for (int index = 0; index < GameModel.Members.Length; index++)
            {
                MemberDefinition member = GameModel.Members[index];
                Assert.That(ids.Add(member.Id), Is.True, $"成员 ID 重复：{member.Id}");
                Assert.That(portraits.Add(member.ResourcePath), Is.True,
                    $"成员立绘路径重复：{member.ResourcePath}");
                Assert.That(thumbnails.Add(member.ThumbnailResourcePath), Is.True,
                    $"成员缩略图路径重复：{member.ThumbnailResourcePath}");
            }
        }

        [Test]
        public void LegacyNineExposeSelectionProfileRaceAndCareer()
        {
            string[] expectedRaces =
            {
                "魅族", "魔族 · 恶魔", "海灵族 · 人鱼", "血精灵", "魅族",
                "魔族 · 恶魔", "海灵族 · 人鱼", "血精灵", "魅族"
            };
            string[] expectedCareers =
            {
                "主唱", "舞者", "支援", "主唱", "舞者", "支援", "主唱", "舞者", "支援"
            };

            for (int index = 0; index < expectedRaces.Length; index++)
            {
                Assert.That(GameModel.Members[index].Race, Is.EqualTo(expectedRaces[index]),
                    $"成员 {GameModel.Members[index].Id} 的选秀种族映射不一致");
                Assert.That(GameModel.Members[index].Career, Is.EqualTo(expectedCareers[index]),
                    $"成员 {GameModel.Members[index].Id} 的职业定位不一致");
            }
        }

        [Test]
        public void EveryCatalogMemberHasLoadablePortraitAndThumbnailSprites()
        {
            for (int index = 0; index < GameModel.Members.Length; index++)
            {
                MemberDefinition member = GameModel.Members[index];
                Sprite portrait = Resources.Load<Sprite>(member.ResourcePath);
                Sprite thumbnail = Resources.Load<Sprite>(member.ThumbnailResourcePath);
                Assert.That(portrait, Is.Not.Null,
                    $"成员 {member.Id} 缺少立绘资源：{member.ResourcePath}");
                Assert.That(thumbnail, Is.Not.Null,
                    $"成员 {member.Id} 缺少缩略图资源：{member.ThumbnailResourcePath}");
            }
        }
    }
}

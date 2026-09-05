using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class MemberCatalogIntegrationTests
    {
        [Test]
        public void LatestDocumentCareersAgreeAcrossAuthoredCatalogRuntimeAndBattleUnits()
        {
            CollectionAssert.AreEqual(new[] { "主唱", "主舞", "Rapper", "门面" }, MemberCareers.All);
            var catalog = JsonUtility.FromJson<MemberCatalogManifest>(
                Resources.Load<TextAsset>(MemberCatalog.DefaultManifestResourcePath).text);
            var tactics = JsonUtility.FromJson<TacticsManifest>(Resources.Load<TextAsset>("Data/tactics").text);
            var units = tactics.Units.ToDictionary(unit => unit.Id);
            foreach (MemberCatalogEntry entry in catalog.Members)
                Assert.That(MemberCareers.All, Does.Contain(entry.Role),
                    $"正式成员清单不能重新写入旧职业：{entry.Id} / {entry.Role}");
            foreach (MemberDefinition member in GameModel.Members)
            {
                Assert.That(units.ContainsKey(member.Id), Is.True, member.Id);
                Assert.That(units[member.Id].Role, Is.EqualTo(member.Career),
                    $"档案与战斗的职业必须一致：{member.Id}");
            }
        }

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
                "主唱", "主舞", "门面", "Rapper", "主舞", "门面", "主唱", "主舞", "门面"
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
        public void EveryRuntimeMemberHasOneSupportedRaceAndCareer()
        {
            var races = new HashSet<string>
            {
                "魅族", "魔族 · 恶魔", "海灵族 · 人鱼", "血精灵"
            };
            var careers = new HashSet<string>(MemberCareers.All);

            foreach (MemberDefinition member in GameModel.Members)
            {
                Assert.That(races.Contains(member.Race), Is.True,
                    $"成员 {member.Id} 缺少统一种族资料：{member.Race}");
                Assert.That(careers.Contains(member.Career), Is.True,
                    $"成员 {member.Id} 缺少统一职业资料：{member.Career}");
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

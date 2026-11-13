using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 舞台五维成长回归：招募起点被压低到可培养区间（全池含老成员 ≤ ~80，且每维至少留 20 点
    /// 培养空间）、0–100 量表全程封顶、训练成长单调有效、旧档已投入字段原样保留。
    /// 候选五维由 <see cref="GameModel.StageStats"/> 纯函数推导；未签约成员的存储等级是招募基准
    /// 而非投入，随图鉴回调只降不升——已解锁成员、货币与训练记录一律不动。
    /// </summary>
    public sealed class StageStatGrowthBalanceTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 9, 15, 12, 0, 0);

        private static GameModel NewModel(ISaveStore store = null) =>
            new GameModel(() => FixedNow, store ?? new InMemorySaveStore());

        private static int[] Five(int memberIndex, int channel, int level)
        {
            GameModel.StageStats(GameModel.Members[memberIndex], memberIndex, channel, level,
                out int vocal, out int rhythm, out int presence, out int resonance, out int charm);
            return new[] { vocal, rhythm, presence, resonance, charm };
        }

        private static IEnumerable<int> UnsignedMembers(GameModel model) =>
            Enumerable.Range(0, GameModel.Members.Length).Where(index => !model.IsUnlocked(index));

        // ------------------------------------------------------------------ 招募起点分布

        [Test]
        public void OnlineCandidatesStartInLoweredOrdinaryBand()
        {
            GameModel model = NewModel();
            // 新档候选全员 1 级起步、线上预览即 1 级——本条普通档约束覆盖全部线上候选人，
            // 没有任何成员被豁免。
            var stats = UnsignedMembers(model)
                .Where(index => model.PreviewSigningLevel(index, 0) == 1)
                .SelectMany(index => Five(index, 0, 1))
                .ToArray();
            Assert.That(stats.Length, Is.GreaterThan(0));
            foreach (int value in stats)
                Assert.That(value, Is.InRange(30, 55), "线上签约起点必须落在压低后的普通档 30–55。");
            Assert.That(stats.Max() - stats.Min(), Is.GreaterThanOrEqualTo(15),
                "成员散列与职业加成必须保留可见差异，不能把所有人压成同一个数。");
        }

        [Test]
        public void OfflineCandidatesReadHigherThanOnline()
        {
            GameModel model = NewModel();
            foreach (int index in UnsignedMembers(model))
            {
                int[] offline = Five(index, 1, model.PreviewSigningLevel(index, 1));
                int[] online = Five(index, 0, model.PreviewSigningLevel(index, 0));
                Assert.That(offline.Sum(), Is.GreaterThan(online.Sum()),
                    "同一成员线下签约读数必须高于线上，报价差才有实际意义。");
            }
        }

        [Test]
        public void EveryCandidateInBothPoolsKeepsSubstantialTrainingHeadroom()
        {
            GameModel model = NewModel();
            foreach (int index in UnsignedMembers(model))
            foreach (int channel in new[] { 0, 1 })
            foreach (int value in Five(index, channel, model.PreviewSigningLevel(index, channel)))
            {
                // 全池无例外：新签约读数不得越过 ~80，任何一维都至少留 20 点培养空间，
                // 瑶光/琥珀等老成员同样适用——“高等级老成员例外”不是验收口径。
                Assert.That(value, Is.LessThanOrEqualTo(80),
                    $"成员 {GameModel.Members[index].Id} 渠道 {channel} 的新签约读数不得超过 ~80 上限。");
                Assert.That(GameModel.StageStatMax - value, Is.GreaterThanOrEqualTo(20),
                    "每一维都必须保留实质培养空间，签约即接近满值等于训练失去意义。");
            }
        }

        [Test]
        public void RarityStillSeparatesCandidateStrength()
        {
            GameModel model = NewModel();
            // 同级比较：稀有度差异由 BasePower 偏移承担，等级溢价不该混进这条信号。
            double MeanOf(string rarity) => UnsignedMembers(model)
                .Where(index => GameModel.Members[index].Rarity == rarity)
                .SelectMany(index => Five(index, 1, 1))
                .DefaultIfEmpty(0).Average();
            double ssr = MeanOf("SSR"), rare = MeanOf("R");
            Assert.That(ssr, Is.GreaterThan(rare), "稀有度战力偏移必须让 SSR 候选仍明显强于 R。");
        }

        // ------------------------------------------------------------------ 量表上限

        [Test]
        public void StageStatsNeverExceedCapAtAnyLevelOrChannel()
        {
            int[] levels = { 1, 5, 9, 20, 40, 60, 80, 99, GameModel.MaxMemberLevel };
            for (int index = 0; index < GameModel.Members.Length; index++)
            foreach (int channel in new[] { 0, 1 })
            foreach (int level in levels)
            foreach (int value in Five(index, channel, level))
                Assert.That(value, Is.InRange(1, GameModel.StageStatMax),
                    $"成员 {GameModel.Members[index].Id} 渠道 {channel} 等级 {level} 的舞台五维不得超出 0–100 量表。");
        }

        // ------------------------------------------------------------------ 训练成长

        [Test]
        public void StageStatsGrowMonotonicallyAndMeaningfullyWithTraining()
        {
            for (int index = 0; index < GameModel.Members.Length; index++)
            {
                foreach (int channel in new[] { 0, 1 })
                {
                    int[] atOne = Five(index, channel, 1);
                    int[] atFive = Five(index, channel, 5);
                    int[] atFifty = Five(index, channel, 50);
                    int[] atCap = Five(index, channel, GameModel.MaxMemberLevel);
                    for (int stat = 0; stat < 5; stat++)
                    {
                        Assert.That(atFive[stat] - atOne[stat], Is.EqualTo(2),
                            "低段每两级必须恰好 +1，成长斜率被改陡或改平都要被发现。");
                        Assert.That(atFifty[stat] - atOne[stat], Is.GreaterThanOrEqualTo(20),
                            "练到 50 级必须拉开明显差距，训练才有长期意义。");
                        Assert.That(atCap[stat], Is.GreaterThanOrEqualTo(atFifty[stat]),
                            "成长不得在中途倒退。");
                    }

                    for (int level = 1; level < GameModel.MaxMemberLevel; level++)
                    {
                        int[] current = Five(index, channel, level);
                        int[] next = Five(index, channel, level + 1);
                        for (int stat = 0; stat < 5; stat++)
                            Assert.That(next[stat], Is.GreaterThanOrEqualTo(current[stat]),
                                $"成员 {GameModel.Members[index].Id} 渠道 {channel} 第 {level}→{level + 1} 维 {stat} 不得下降。");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 签约一致性（跨页单一模型）

        [Test]
        public void SignedProfileKeepsExactlyTheInterviewedStatsAfterReload()
        {
            foreach (int channel in new[] { 0, 1 })
            {
                var store = new InMemorySaveStore();
                GameModel model = NewModel(store);
                int member = model.InterviewCandidates(channel).First();
                int[] shown = Five(member, channel, model.PreviewSigningLevel(member, channel));
                model.Save.Diamonds = 10000;
                Assert.That(model.SignInterviewCandidate(channel, member, model.CurrentInterviewCycle,
                    model.InterviewQuote(channel, member), out string message), Is.True, message);

                GameModel reloaded = NewModel(store);
                Assert.That(reloaded.LevelOf(member), Is.EqualTo(model.LevelOf(member)));
                int[] persisted = Five(member, reloaded.SigningChannelOf(member), reloaded.LevelOf(member));
                Assert.That(persisted, Is.EqualTo(shown),
                    "面试卡、成员档案与重载后读数必须来自同一个公式，不允许两套数值。");
            }
        }

        // ------------------------------------------------------------------ 旧档保留与幂等

        [Test]
        public void OldSaveKeepsSignedLevelsReseedsUnsignedVeteransAndStaysIdempotent()
        {
            var store = new InMemorySaveStore();
            var save = new GameSave
            {
                Diamonds = 4321,
                Gold = 87654,
                Stamina = 77,
                UnlockedMembers = new List<int> { 0, 1, 2, 6 },
                Team = new List<int> { 0, 1, 2, 6 },
                MemberLevels = Enumerable.Repeat(1, GameModel.Members.Length).ToList(),
                MemberRisk = Enumerable.Repeat(0, GameModel.Members.Length).ToList(),
                MemberSigningChannel = Enumerable.Repeat(-1, GameModel.Members.Length).ToList(),
            };
            save.MemberLevels[0] = 68;   // 初始解锁老成员的真实等级
            save.MemberLevels[6] = 54;   // 旧版图鉴基准下线下签约的老成员（46+8），含真实投入
            save.MemberLevels[5] = 49;   // 从未签约的琥珀：存储的只是旧版图鉴招募基准
            save.MemberLevels[4] = 52;   // 从未签约的瑶光：同上
            save.MemberRisk[0] = 7;
            save.MemberRisk[6] = 33;
            save.MemberSigningChannel[6] = 1;
            store.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));

            GameModel loaded = NewModel(store);
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(4321), "旧档星钻不得被重置");
            Assert.That(loaded.Save.Gold, Is.EqualTo(87654), "旧档星光币不得被重置");
            Assert.That(loaded.Save.Stamina, Is.EqualTo(77));
            Assert.That(loaded.Save.UnlockedMembers, Is.EqualTo(new[] { 0, 1, 2, 6 }.Concat(GameModel.GiftedMembers1112.Select(GameModel.IndexOfMember))), "已签约名单必须保留（另加 1112 赠送的三人）");
            Assert.That(loaded.LevelOf(0), Is.EqualTo(68), "训练等级是既有投入，必须原样保留");
            Assert.That(loaded.LevelOf(6), Is.EqualTo(54), "已签老成员在旧基准下获得的等级同样是既有投入");
            Assert.That(loaded.SigningChannelOf(6), Is.EqualTo(1), "签约渠道记录必须保留");
            Assert.That(loaded.RiskOf(0), Is.EqualTo(7), "已休整过的风险值必须保留");
            Assert.That(loaded.RiskOf(6), Is.EqualTo(33));

            // 未签约成员的存储等级不是玩家投入：随图鉴招募基准封顶（只降不升），新报价立即带头部空间。
            Assert.That(loaded.LevelOf(5), Is.EqualTo(14), "未签约老成员应按现行图鉴基准重新出价");
            Assert.That(loaded.LevelOf(4), Is.EqualTo(16));
            Assert.That(loaded.PreviewSigningLevel(5, 1), Is.EqualTo(22), "预览等级必须如实反映压降后的招募基准");
            Assert.That(loaded.PreviewSigningLevel(4, 1), Is.EqualTo(24));
            foreach (int vet in new[] { 4, 5 })
            {
                int previewLevel = loaded.PreviewSigningLevel(vet, 1);
                foreach (int value in Five(vet, 1, previewLevel))
                {
                    Assert.That(value, Is.LessThanOrEqualTo(80),
                        "旧存档里重新出价的老成员同样不得越过 ~80。");
                    Assert.That(GameModel.StageStatMax - value, Is.GreaterThanOrEqualTo(20));
                }
            }

            // 高等级旧档成员（含已签老成员）的五维按新公式重算后仍落在量表内。
            foreach (int value in Five(0, 1, loaded.LevelOf(0)))
                Assert.That(value, Is.InRange(1, GameModel.StageStatMax));

            GameModel again = NewModel(store);
            Assert.That(again.Save.MemberLevels, Is.EqualTo(loaded.Save.MemberLevels),
                "基准重设必须幂等：第二次载入不得再次改写等级。");
            Assert.That(Five(5, 1, again.LevelOf(5)), Is.EqualTo(Five(5, 1, loaded.LevelOf(5))));
            Assert.That(again.LevelOf(6), Is.EqualTo(54));
        }

        // ------------------------------------------------------------------ 价格与风险不动

        [Test]
        public void ChannelPricesAndRisksStillFavourThePricingIntent()
        {
            GameModel model = NewModel();
            foreach (int index in UnsignedMembers(model).Take(12))
            {
                Assert.That(model.InterviewQuote(1, index),
                    Is.GreaterThan(model.InterviewQuote(0, index)), "线下报价必须仍高于线上。");
                Assert.That(model.InterviewRisk(0, index),
                    Is.GreaterThan(model.InterviewRisk(1, index)), "线上风险必须仍高于线下。");
            }
        }
    }
}

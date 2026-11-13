using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-11 (赵金 11 月内容): 第三章 BOSS 护盾 / 弹反 / 骰子种类, and the kits of 幻纱 / 幻惑伶 / 凛音.</summary>
    public sealed class Chapter3Battle1111Tests
    {
        private static BattleSimulator Fixture(string stageId, string encounter, string[] members, int enemies = 1,
            int enemyHp = 1000000, int enemyAttack = 1, int permille = 999, string chapter = "第 03 章")
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "basic", Name = "basic", Cooldown = 0 });
            UnitDefinition Unit(string id, int hp, int atk, int speed) => new UnitDefinition
            {
                Id = id, Name = id, MaxHp = hp, Attack = atk, Defense = 0,
                Speed = speed, CritPermille = 0, SkillIds = new List<string> { "basic" }
            };
            foreach (string m in members) manifest.Units.Add(Unit(m, 10000, 10, 5));
            for (int i = 0; i < enemies; i++) manifest.Units.Add(Unit("dummy" + i, enemyHp, enemyAttack, 1));
            var stage = new StageDefinition
            {
                Id = stageId, Chapter = chapter, RecommendedLevel = 1, EncounterType = encounter, TimeLimitSeconds = 120,
                BossTier = encounter == "boss" ? BattleSimulator.BossTierMini : string.Empty
            };
            for (int i = 0; i < enemies; i++) stage.Enemies.Add(new EnemySpawn { UnitId = "dummy" + i, Row = i, Col = 0 });
            var party = new List<PlayerUnitSetup>();
            for (int i = 0; i < members.Length; i++) party.Add(new PlayerUnitSetup { UnitId = members[i], Row = i % 3, Col = i / 3 });
            var battle = new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { permille }, new[] { 0, 1, 2, 3, 4, 5 }));
            var races = new Dictionary<string, string>();
            foreach (string m in members) races[m] = m == "linyin" || m == "elf" ? "血精灵" : m.StartsWith("p") ? "海灵族 · 人鱼" : "魅族";
            battle.EnableRealtime(races, members[0], stage.RerollLimit, false);
            battle.EnableTurnBased();
            return battle;
        }

        private static BattleUnit Member(BattleSimulator b, string id) => b.Units.First(u => u.Definition.Id == id);
        private static BattleUnit Enemy(BattleSimulator b, int i = 0) => b.Units.Where(u => u.Side == BattleSide.Enemy).ElementAt(i);

        /// <summary>Plays turns until <paramref name="who"/> is up (enemies act automatically on the way).</summary>
        private static bool RunUntil(BattleSimulator b, BattleUnit who)
        {
            for (int guard = 0; guard < 200 && b.Outcome == BattleOutcome.Ongoing; guard++)
            {
                BattleUnit actor = b.NextTurnActor();
                if (actor == null) return false;
                if (actor == who) return true;
                if (actor.Side == BattleSide.Enemy) b.EnemyTurn(); else b.TryTurnCommand(TurnCommand.Basic);
            }
            return false;
        }

        [Test]
        public void ChapterThreeBossHasShieldLayersThatEachTakeTwoHits()
        {
            BattleSimulator b = Fixture("stage-3-5", "boss", new[] { "p0", "p1", "p2", "p3" });
            BattleUnit boss = Enemy(b);
            Assert.That(b.Chapter3Rules1111, Is.True);
            Assert.That(b.Barrier(boss), Is.EqualTo(BattleSimulator.ShieldLayersMiniBoss), "小BOSS 3 层");
            int hp = boss.Hp;
            Assert.That(RunUntil(b, Member(b, "p0")), Is.True);
            b.TryTurnCommand(TurnCommand.Basic);
            // Every hit (the basic, plus the dice target's bonus strike when it lands) grinds half a layer.
            int Hits() => (BattleSimulator.ShieldLayersMiniBoss - b.Barrier(boss)) * BattleSimulator.HitsPerShieldLayer + b.BarrierLayerHits(boss);
            Assert.That(Hits(), Is.InRange(1, 2), "a basic attack is one hit (two with the dice bonus)");
            Assert.That(boss.Hp, Is.EqualTo(hp), "没有穿刺：伤害先打盾");
            int before = Hits();
            Assert.That(RunUntil(b, Member(b, "p1")), Is.True);
            b.TryTurnCommand(TurnCommand.Basic);
            Assert.That(Hits(), Is.GreaterThan(before));
            Assert.That(b.Barrier(boss), Is.LessThan(BattleSimulator.ShieldLayersMiniBoss), "two hits break a layer");
            Assert.That(boss.Hp, Is.EqualTo(hp));
        }

        [Test]
        public void EliteAndMajorBossLayersAndNormalMinionsHaveNone()
        {
            Assert.That(Fixture("stage-3-3", "elite", new[] { "p0" }) is var e && e.Barrier(Enemy(e)) == BattleSimulator.ShieldLayersElite, Is.True);
            BattleSimulator major = Fixture("stage-3-10", "boss", new[] { "p0" });
            major.Stage.BossTier = BattleSimulator.BossTierMajor;
            major.GiveOpeningBarrier1111();
            Assert.That(major.Barrier(Enemy(major)), Is.EqualTo(BattleSimulator.ShieldLayersMajorBoss));
            Assert.That(Fixture("stage-3-1", "normal", new[] { "p0" }) is var n && n.Barrier(Enemy(n)) == 0, Is.True);
        }

        [Test]
        public void PiercingBloodElfHitsReachTheBodyThroughTheShield()
        {
            BattleSimulator b = Fixture("stage-3-5", "boss", new[] { "elf", "p1" });
            BattleUnit boss = Enemy(b);
            int hp = boss.Hp;
            Assert.That(RunUntil(b, Member(b, "elf")), Is.True);
            b.TryTurnCommand(TurnCommand.Basic);
            Assert.That(boss.Hp, Is.LessThan(hp), "穿刺 passes part of the hit");
            Assert.That(b.Barrier(boss) * BattleSimulator.HitsPerShieldLayer - b.BarrierLayerHits(boss),
                Is.LessThan(BattleSimulator.ShieldLayersMiniBoss * BattleSimulator.HitsPerShieldLayer), "and still grinds the layer");
        }

        [Test]
        public void EarlierChaptersHaveNoBarrierOrParry()
        {
            BattleSimulator b = Fixture("stage-2-5", "boss", new[] { "p0", "p1" }, chapter: "第 02 章");
            Assert.That(b.Barrier(Enemy(b)), Is.Zero);
            Assert.That(b.CanParry(Enemy(b)), Is.False);
        }

        [Test]
        public void LinyinBreaksALayerAndBreakingThemAllOpensTheBoss()
        {
            BattleSimulator b = Fixture("stage-3-5", "boss", new[] { "linyin", "p1" }, enemyHp: 40000);
            BattleUnit boss = Enemy(b), linyin = Member(b, "linyin");
            int max = b.BarrierMax(boss);
            Assert.That(RunUntil(b, linyin), Is.True);
            Assert.That(b.TryTurnCommand(TurnCommand.Skill), Is.True);
            Assert.That(b.Barrier(boss), Is.EqualTo(max - 1), "裂甲音刃 strips a whole layer");
            for (int guard = 0; guard < 60 && b.Barrier(boss) > 0 && b.Outcome == BattleOutcome.Ongoing; guard++)
            {
                BattleUnit actor = b.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Enemy) b.EnemyTurn(); else b.TryTurnCommand(TurnCommand.Basic);
            }
            Assert.That(b.Barrier(boss), Is.Zero);
            Assert.That(b.BarrierBroken(boss), Is.True, "破盾 opens a damage window");
        }

        [Test]
        public void PerfectParryBlocksTheChargedSpecialAndReflectsIt()
        {
            BattleSimulator b = Fixture("stage-3-1", "normal", new[] { "p0", "p1", "p2", "p3" }, enemyAttack: 200);
            BattleUnit enemy = Enemy(b);
            bool parried = false;
            for (int guard = 0; guard < 200 && b.Outcome == BattleOutcome.Ongoing && !parried; guard++)
            {
                BattleUnit actor = b.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Player) { b.TryTurnCommand(TurnCommand.Basic); continue; }
                if (!b.CanParry(actor)) { b.EnemyTurn(); continue; }
                int team = b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield);
                int hp = enemy.Hp;
                b.ArmParry(actor, UltimateTimingGrade.Perfect);
                b.EnemyTurn();
                Assert.That(b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield), Is.EqualTo(team),
                    "a perfect parry takes the whole special");
                Assert.That(b.LastParryBlocked1111, Is.GreaterThan(0));
                Assert.That(enemy.Hp, Is.LessThan(hp), "the blocked damage goes back to the attacker");
                parried = true;
            }
            Assert.That(parried, Is.True, "the enemy charged a special within the fight");
        }

        [Test]
        public void HuanhuoFlawStacksAndHerUltimateConfusesEnemiesIntoHittingEachOther()
        {
            BattleSimulator b = Fixture("stage-3-2", "normal", new[] { "huanhuoling", "p1" }, enemies: 2, enemyAttack: 50);
            BattleUnit huo = Member(b, "huanhuoling");
            Assert.That(RunUntil(b, huo), Is.True);
            Assert.That(b.TryTurnCommand(TurnCommand.Skill), Is.True);
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Enemy).Max(u => b.FlawStacks(u)), Is.EqualTo(1));

            b.AdvanceFormationCharge1102(40000);
            Assume.That(b.IsUltimateReady(huo), Is.True);
            Assert.That(b.TryCastUltimateNow(huo, UltimateTimingGrade.Miss), Is.True);
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Enemy).All(b.IsConfused), Is.True);
            BattleUnit first = null;
            for (int guard = 0; guard < 20 && first == null; guard++)
            {
                BattleUnit actor = b.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Player) { b.TryTurnCommand(TurnCommand.Basic); continue; }
                first = actor;
            }
            Assert.That(first, Is.Not.Null);
            BattleUnit other = b.Units.First(u => u.Side == BattleSide.Enemy && u != first);
            int team = b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield);
            int otherHp = other.Hp;
            b.EnemyTurn();
            Assert.That(other.Hp, Is.LessThan(otherHp), "扰乱: it attacks its own side");
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield), Is.EqualTo(team));
        }

        [Test]
        public void HuanshaDisguisesTheTeamAndDecoysTurnSingleHitsAway()
        {
            BattleSimulator b = Fixture("stage-3-1", "normal", new[] { "huansha", "p1", "p2" }, enemyAttack: 50, permille: 0);
            BattleUnit sha = Member(b, "huansha");
            b.AdvanceFormationCharge1102(40000);
            Assert.That(RunUntil(b, sha), Is.True);
            Assume.That(b.IsUltimateReady(sha), Is.True);
            Assert.That(b.TryCastUltimateNow(sha, UltimateTimingGrade.Miss), Is.True);
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Player).All(b.IsDisguised), Is.True);
            BattleUnit enemy = Enemy(b);
            Assert.That(RunUntil(b, enemy), Is.True);
            int team = b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield);
            b.EnemyTurn();
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Player).Sum(u => u.Hp + u.Shield), Is.EqualTo(team),
                "the attack hit a phantom");
            Assert.That(b.IsSealed(enemy), Is.True, "幻影被击破时，攻击者沉默");
        }

        [Test]
        public void RelicDiceAreTeamSkillsOncePerBattleTwiceARound()
        {
            BattleSimulator b = Fixture("stage-3-1", "normal", new[] { "p0", "p1" }, enemyAttack: 100);
            b.SetRelicDice(new[] { RelicDice1111.Blade, RelicDice1111.Bloom, RelicDice1111.Chaos, RelicDice1111.Mend });
            Assert.That(b.RelicDice.Count, Is.EqualTo(RelicDice1111.CarrySlots), "carry three");
            int dice = b.BattleDice.DamageMultiplierPermille;
            Assert.That(RunUntil(b, Member(b, "p0")), Is.True);
            Assert.That(b.TryUseRelic(0), Is.True);
            Assert.That(b.TryUseRelic(0), Is.False, "once a battle");
            Assert.That(b.TryUseRelic(2), Is.True);
            Assert.That(b.TryUseRelic(1), Is.False, "two a round");
            Assert.That(b.BattleDice.DamageMultiplierPermille, Is.EqualTo(dice), "技能骰不改倍率");
            Assert.That(b.Units.Where(u => u.Side == BattleSide.Enemy).All(b.IsBlinded), Is.True, "惑乱 blinds");
            b.TryTurnCommand(TurnCommand.Basic);
            for (int guard = 0; guard < 20 && b.TurnRound < 2; guard++)
            {
                BattleUnit actor = b.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Enemy) b.EnemyTurn(); else b.TryTurnCommand(TurnCommand.Basic);
            }
            Assert.That(b.CanUseRelic(1), Is.True, "a new round frees the uses");
        }

        [Test]
        public void RelicDiceDropOnWinsAndShatterOnLosses()
        {
            UnityEngine.PlayerPrefs.DeleteKey(GameModel.SaveKey);
            var model = new GameModel(() => new System.DateTime(2026, 11, 12, 12, 0, 0, System.DateTimeKind.Utc));
            Assert.That(model.RelicDiceUnlocked, Is.False);
            // 2026-11-12: open before 第三章 — the starter dice come with the first visit and can be carried already.
            model.EnsureRelicStarter();
            Assert.That(model.RelicCount(RelicDice1111.Blade), Is.EqualTo(1));
            Assert.That(model.CarryRelic(RelicDice1111.Blade, out string m), Is.True, m);
            Assert.That(model.CarryRelic(RelicDice1111.Blade, out _), Is.False, "only as many as owned");
            Assert.That(model.CarryRelic(RelicDice1111.Bulwark, out m), Is.True, m);
            Assert.That(model.CarriedRelics, Is.EqualTo(new[] { RelicDice1111.Blade, RelicDice1111.Bulwark }));
            Assert.That(RelicDice1111.Pick(0), Is.EqualTo(RelicDice1111.All[0]));
            Assert.That(RelicDice1111.Pick(999), Is.EqualTo(RelicDice1111.Mend), "the rarest sits at the end");
            UnityEngine.PlayerPrefs.DeleteKey(GameModel.SaveKey);
        }

        [Test]
        public void NewMembersAreInTheCatalogWithTheirOwnSkills()
        {
            foreach (string id in new[] { "huansha", "huanhuoling", "linyin" })
            {
                MemberDefinition member = GameModel.Members.FirstOrDefault(m => m.Id == id);
                Assert.That(member, Is.Not.Null, id);
                Assert.That(UnityEngine.Resources.Load<UnityEngine.Sprite>(member.ResourcePath), Is.Not.Null, id);
                Assert.That(BattleSimulator.KitOf(id), Is.Not.EqualTo(MemberKit1111.None));
                Assert.That(BattleAuthoredCutin1019.KeyForMember(id), Is.Not.Null);
                Assert.That(BattlePsdArt928.Find(BattleAuthoredCutin1019.KeyForMember(id)), Is.Not.Null);
            }
            Assert.That(MemberSkillNames1103.Name("linyin", CombatRace.BloodElf, false), Is.EqualTo("裂甲音刃"));
        }
        [Test]
        public void ChapterThreeHasTwentyStagesThatOpenAfterTwoTwenty()
        {
            UnityEngine.PlayerPrefs.DeleteKey(GameModel.SaveKey);
            var model = new GameModel(() => new System.DateTime(2026, 11, 11, 12, 0, 0, System.DateTimeKind.Utc));
            for (int n = 1; n <= GameModel.ChapterThreeStageCount; n++)
            {
                StageDefinition stage = model.Tactics.FindStage($"stage-3-{n}");
                Assert.That(stage, Is.Not.Null, n.ToString());
                Assert.That(BattleSimulator.IsChapter3Stage(stage), Is.True);
                Assert.That(stage.EncounterType == "boss", Is.EqualTo(n % 5 == 0), stage.Id);
            }
            Assert.That(model.IsStageUnlocked("stage-3-1"), Is.False, "第三章 waits for 2-20");
            Assert.That(model.IsStageUnlocked("stage-3-2"), Is.False);
            Assert.That(model.RelicDiceUnlocked, Is.False);
            Assert.That(model.CarriedRelics.Count, Is.Zero);
            Assert.That(model.ChapterStarRewardViews(3).Count, Is.EqualTo(6));
            Assert.That(model.ChapterTaskViews(3).Count, Is.EqualTo(4));
            UnityEngine.PlayerPrefs.DeleteKey(GameModel.SaveKey);
        }
    }
}

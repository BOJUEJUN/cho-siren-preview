using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
namespace ChoSiren.Tests
{
    public sealed class BattleMeeting929Tests
    {
        static BattleSimulator Party()
        {
            var args=new object[]{null,null};
            return (BattleSimulator)typeof(BattleEnergyUltimateTests).GetMethod("CreateTwoMemberBattle",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        }
        static void Hand(BattleSimulator b,params int[] values)
        {
            var a=(int[])typeof(DiceTurn).GetField("values",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(b.BattleDice);
            values.CopyTo(a,0);typeof(DiceTurn).GetMethod("RefreshHand",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b.BattleDice,null);
        }
        [Test] public void HighHandsAccelerateCaptainOnlyAndFractionalGainsAreRetained()
        {
            var b=Party();b.AutoCastUltimates=false;var captain=b.FindUnit(1);var ally=b.FindUnit(2);
            Hand(b,1,2,3,4,5,5);int low=b.EnergyGainPermille(captain);
            Hand(b,6,6,6,6,6,6);
            Assert.That(b.EnergyGainPermille(captain),Is.GreaterThan(low));
            Assert.That(b.EnergyGainPermille(captain),Is.EqualTo(DiceRules.MultiplierOf(DicePattern.SixKind)*1250/1000)); // 2026-10-31 六同 ×1.8
            Assert.That(b.EnergyGainPermille(ally),Is.EqualTo(1000));
            var gain=typeof(BattleSimulator).GetMethod("GainUltimateEnergy",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<4;i++)gain.Invoke(b,new object[]{captain,1});
            Assert.That(b.EnergyOf(captain),Is.EqualTo(4*b.EnergyGainPermille(captain)/1000),"small grants must preserve fractional energy");
        }
        [Test] public void DiceDamageMultiplierAffectsOnlyCaptain()
        {
            var b=Party();Hand(b,6,6,6,6,6,6);
            var deal=typeof(BattleSimulator).GetMethod("Deal",BindingFlags.Instance|BindingFlags.NonPublic);
            var target=b.Units.Last();
            foreach(var u in b.Units.Where(u=>u.Side==BattleSide.Player))deal.Invoke(b,new object[]{u,target,"rt-basic",1000,0});
            var hits=b.Log.Where(e=>e.Kind==BattleEventKind.Damage).ToArray();
            Assert.That(hits.Length,Is.EqualTo(2));
            Assert.That(hits[0].Amount,Is.GreaterThan(hits[1].Amount));
            Assert.That(hits[1].Amount,Is.EqualTo(100));
        }
        [Test] public void UltimateEmptiesEnergyAndOwnMultiTargetHitsCannotRefillIt()
        {
            var b=Party();b.AutoCastUltimates=false;
            b.AdvanceRealtime(26000);var c=b.FindUnit(1);
            Assert.That(b.TryCastUltimate(c.Id),Is.True);
            Assert.That(b.EnergyOf(c),Is.Zero);
            Assert.That(b.TryCastUltimate(c.Id),Is.False);
        }
        [Test] public void GuardIsOneUseAcrossTimeAndOnlyNewEncounterRestoresIt()
        {
            var b=Party();Assert.That(b.TryTacticalGuard(),Is.True);b.AdvanceRealtime(20000);
            Assert.That(b.TacticalGuardUsed,Is.True);Assert.That(b.TryTacticalGuard(),Is.False);
            Assert.That(Party().TryTacticalGuard(),Is.True);
        }
        [Test] public void AutoUltimatesAreSpacedAndEachMemberHasALongerCooldown()
        {
            var b=Party();b.AutoCastUltimates=true;for(int i=0;i<9;i++)b.AdvanceRealtime(10000);
            var casts=b.Log.Where(e=>e.Kind==BattleEventKind.ActionStarted && e.SkillId!=null && e.SkillId.EndsWith("-big")).ToArray();
            Assert.That(casts.Length,Is.GreaterThanOrEqualTo(4));
            for(int i=1;i<casts.Length;i++)Assert.That(casts[i].TimeMilliseconds-casts[i-1].TimeMilliseconds,Is.GreaterThanOrEqualTo(4000));
            foreach(var actor in casts.GroupBy(e=>e.ActorId))
            {
                var times=actor.Select(e=>e.TimeMilliseconds).ToArray();Assert.That(times.Length,Is.LessThanOrEqualTo(3));
                for(int i=1;i<times.Length;i++)Assert.That(times[i]-times[i-1],Is.GreaterThanOrEqualTo(28000));
            }
        }
    }
}

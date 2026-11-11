using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's side of CampHarmMath: the facts of a harm or a danger read from the map, the camp's group scattering
    /// only when the colony meant it (<see cref="Trigger_CampHarmed"/>), and what counts as danger to a camp.
    /// </summary>
    public static class CampHarm
    {
        /// <summary>
        /// What the rule knows of <paramref name="by"/>, who hurt <paramref name="victim"/> (one of a camp of
        /// <paramref name="camp"/>'s people) aiming at <paramref name="intended"/>; or, with no victim, of a danger about on the land.
        /// </summary>
        public static CampHarmFacts Facts(Thing by, Pawn victim, Faction camp, Thing intended = null)
        {
            var f = new CampHarmFacts { instigator = by != null };
            if (by == null) return f;
            var p = by as Pawn;
            var race = p?.RaceProps;
            var their = by.Faction;
            f.colony = their != null && their.IsPlayer;
            f.animal = race != null && race.Animal;
            f.insect = race != null && race.Insect;
            f.noFaction = their == null;
            f.hive = their != null && their == Faction.OfInsects;
            f.mad = (p != null && p.InAggroMentalState) || (victim != null && victim.InAggroMentalState);
            f.hostileToCamp = their != null && camp != null && their != camp && their.HostileTo(camp);
            f.hostileToColony = their != null && !their.IsPlayer && their.HostileTo(Faction.OfPlayer);
            f.crossfire = Crossfire(by, intended, camp);
            return f;
        }

        /// <summary>
        /// The game's own rule for a stray shot (Faction.IsMutuallyHostileCrossfire): aimed at someone hostile both to
        /// whoever fired and to the camp's people.
        /// </summary>
        private static bool Crossfire(Thing by, Thing intended, Faction camp)
        {
            if (by == null || intended == null || camp == null) return false;
            try
            {
                if (intended.HostileTo(by) && intended.HostileTo(camp)) return true;
                return intended.Faction != null && by.Faction != null && intended.Faction.HostileTo(by.Faction) && intended.Faction.HostileTo(camp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp harm crossfire", e);
                return false;
            }
        }

        /// <summary>What a danger about on the land is, to a camp of these people.</summary>
        public static CampHarmBy ByOf(Thing t, Faction camp) => CampHarmMath.By(Facts(t, null, camp));

        /// <summary>
        /// A real danger to a camp of these people is about on the land: a raider, a people at war with theirs, or the
        /// colony's own, active (the game's own test). Beasts, insects and wild men alone are no reason to go.
        /// </summary>
        public static bool RealThreatOn(Map map, Faction camp)
        {
            if (map == null || !MapFacts.ThreatOn(map)) return false;
            try
            {
                foreach (var t in map.attackTargetsCache.TargetsHostileToColony)
                    if (t?.Thing != null && GenHostility.IsActiveThreatToPlayer(t) && CampHarmMath.RealAttack(Facts(t.Thing, null, camp))) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp real threat", e);
                return true;
            }
            return false;
        }

        /// <summary>The nearest beast, insect or wild man come at a camp: within reach of their fire or at one of them, up and hostile to them.</summary>
        public static Pawn Menace(Map map, Faction camp, IntVec3 fire, System.Collections.Generic.List<Pawn> theirs)
        {
            if (map == null || camp == null || !fire.IsValid) return null;
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || p.Downed || p.Faction == camp || theirs.Contains(p)) continue;
                float d = p.Position.DistanceTo(fire);
                bool near = d <= CampHarmMath.FendRadius;
                if (!near)
                    foreach (var m in theirs)
                        if (p.Position.InHorDistOf(m.Position, CampHarmMath.KeepClear))
                        {
                            near = true;
                            break;
                        }
                if (!near || d >= bestD || !p.HostileTo(camp)) continue;
                if (!CampHarmMath.FendOff(ByOf(p, camp))) continue;
                best = p;
                bestD = d;
            }
            return best;
        }
    }

    /// <summary>
    /// A camp's group scatters (LordJob_Camp) only when the colony meant to harm one of them: its people, animals, turrets
    /// or traps, never a shot meant for an enemy of both or someone out of their mind (CampHarmMath.Scatters). Beasts and
    /// insects are no reason to go: the camp fights them off (CampHarm.cs, Camp.Fend).
    /// </summary>
    public class Trigger_CampHarmed : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (!Trigger_PawnHarmed.SignalIsHarm(signal)) return false;
            var by = signal.dinfo.Instigator;
            if (by == null) return false;
            return CampHarmMath.Scatters(CampHarm.Facts(by, signal.Pawn, lord?.faction, signal.dinfo.IntendedTarget));
        }
    }

    // The camp's side: a beast or an insect at their fire is fought off by those who can fight, the rest keep clear, and
    // the camp stays (the player's report of 10 November 2026).
    public partial class Camp
    {
        /// <summary>They stand by their fire against a beast or an insect (LordToil_CampGuard), not a band (see <see cref="guarding"/>).</summary>
        public bool fending;

        private void ExposeHarm()
        {
            Scribe_Values.Look(ref fending, "fending");
        }

        /// <summary>
        /// Every few seconds (Tick250): a beast, an insect or a wild man come at the camp is fought off by those of them
        /// who can fight, by their fire, and kept clear of by the rest; once none is near, back to their day.
        /// </summary>
        private void Fend()
        {
            if (!Settled || !LordAlive || guarding || CampRaids.Against(this) != null) return;
            var here = Here;
            var menace = here.Count == 0 ? null : CampHarm.Menace(map, faction, FireCell, here);
            if (menace == null)
            {
                if (fending && !CampHarm.RealThreatOn(map, faction)) StopFending();
                return;
            }
            bool anyFights = false;
            foreach (var p in here)
            {
                bool fights = CampHarmMath.Fights(!p.DevelopmentalStage.Juvenile(), !p.WorkTagIsDisabled(WorkTags.Violent) && !p.Downed, p.equipment?.Primary != null);
                anyFights |= fights;
                if (!CampHarmMath.Runs(fights, p.Position.DistanceTo(menace.Position)) || p.Downed || p.InMentalState || p.CurJobDef == JobDefOf.Flee) continue;
                try
                {
                    var run = FleeUtility.FleeJob(p, menace, CampHarmMath.RunFrom);
                    if (run != null) p.jobs.StartJob(run, JobCondition.InterruptForced);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp keeps clear", e);
                }
            }
            if (fending || !anyFights) return;
            fending = true;
            lord.ReceiveMemo(LordJob_Camp.MemoGuard);
            Note(Text("CC_Camp_FendOff", null, null, 0, menace.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, menace, true);
        }

        /// <summary>Nothing at their fire any more: back to their day.</summary>
        private void StopFending()
        {
            fending = false;
            if (!guarding && LordAlive) lord.ReceiveMemo(LordJob_Camp.MemoAtEase);
        }
    }
}

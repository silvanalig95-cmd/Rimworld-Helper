using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// What a war band on the colony's map does besides the game's own hunt (the player, 15 November 2026, of a band called
    /// against a sleeping mech cluster: "the delegation came but is neither attacking the enemies nor following my pawns").
    /// The game's hunt only goes after enemies that are awake, so a band facing a sleeping mech cluster stood idle at the
    /// edge. Now, in order (the duty Rimfolk_WarBandHunt, Defs/Misc/Rimfolk_WarBand.xml):
    /// - they fight any awake enemy near them (the game's own fight);
    /// - while any colonist on the map is drafted, they go with the nearest one;
    /// - they go after awake enemies further off (the game's own);
    /// - with only sleeping enemies they can reach and see, they wait out of the enemy's waking range on the colony's side
    ///   (CampStoryMath.MusterPoint) for the colony's fighters, and once they have waited CampStoryMath.SleepersWaitHours
    ///   with nobody drafted, they go in on their own and wake them by attacking;
    /// - with the enemy shut away where they can't reach it (sealed in the fog), they stand by near the colony.
    /// The colony is told when they start to wait and when they go in (Tell.Message, on the band).
    /// </summary>
    public static class WarBandOrders
    {
        public static DutyDef HuntDuty => DefDatabase<DutyDef>.GetNamedSilentFail("Rimfolk_WarBandHunt");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The war band a pawn belongs to, on its own map: null for anyone else.</summary>
        public static WarBand BandOf(Pawn pawn)
        {
            if (pawn?.Map == null) return null;
            var band = CampTracker.WarBandAt(pawn.Map);
            return band != null && band.LordAlive && band.lord == pawn.GetLord() ? band : null;
        }

        /// <summary>
        /// An enemy asleep: a mech cluster or an ancient danger's sleepers (their duty keeps them out of every fight), a
        /// dormant mechanoid, or a pawn its game keeps inactive. The game's own hunt never looks at them.
        /// </summary>
        public static bool Asleep(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || !p.Spawned) return false;
            if (p.mindState?.duty?.def != null && p.mindState.duty.def.threatDisabled) return true;
            if (p.mindState != null && !p.mindState.Active) return true;
            var dormant = p.TryGetComp<CompCanBeDormant>();
            return dormant != null && !dormant.Awake;
        }

        /// <summary>The nearest sleeping enemy of the colony's that this fighter can see and walk to, or null.</summary>
        public static Pawn Sleeper(Pawn pawn)
        {
            var map = pawn?.Map;
            if (map?.attackTargetsCache == null) return null;
            var sleepers = new List<Pawn>();
            foreach (var target in map.attackTargetsCache.TargetsHostileToFaction(Faction.OfPlayer))
                if (target?.Thing is Pawn p && Asleep(p) && !p.Position.Fogged(map) && p.HostileTo(pawn))
                    sleepers.Add(p);
            sleepers.SortBy(p => p.Position.DistanceToSquared(pawn.Position));
            foreach (var p in sleepers)
                if (pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                    return p;
            return null;
        }

        /// <summary>
        /// Whether any enemy on the map is one the band can see and walk to, awake or asleep. False when every enemy left is
        /// shut away (sealed in the fog), out of their reach: then they gave their time waiting, not fighting.
        /// </summary>
        public static bool AnyInReach(WarBand b)
        {
            var map = b?.map;
            if (map?.attackTargetsCache == null) return false;
            var here = b.Here;
            if (here.Count == 0) return false;
            var walker = here[0];
            foreach (var target in map.attackTargetsCache.TargetsHostileToFaction(Faction.OfPlayer))
            {
                var thing = target?.Thing;
                if (thing == null || !thing.Spawned || thing.Position.Fogged(map)) continue;
                if (thing is Pawn p && (p.Dead || p.Downed)) continue;
                if (walker.CanReach(thing, PathEndMode.Touch, Danger.Deadly)) return true;
            }
            return false;
        }

        /// <summary>The nearest drafted colonist on the fighter's map it can walk to, or null.</summary>
        public static Pawn Leader(Pawn pawn)
        {
            var map = pawn?.Map;
            if (map == null) return null;
            Pawn best = null;
            int bestDist = int.MaxValue;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c == null || !c.Drafted || c.Dead || c.Downed) continue;
                int d = c.Position.DistanceToSquared(pawn.Position);
                if (d < bestDist && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    best = c;
                    bestDist = d;
                }
            }
            return best;
        }

        private static bool AnyDrafted(Map map)
        {
            if (map == null) return false;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (c != null && c.Drafted && !c.Downed)
                    return true;
            return false;
        }

        /// <summary>
        /// Each hour while enemies are on the land (WarCamps.Watch, and as they walk in): with no enemy in their reach
        /// (<paramref name="inReach"/> false: shut away in the fog), the word that they stand by near the colony; with a
        /// sleeping enemy in reach, the spot they wait at, the word to the colony that they wait, and, once they have waited
        /// long enough with nobody drafted, the word that they go in.
        /// </summary>
        public static void Hour(WarBand b, bool inReach)
        {
            if (b?.map == null || b.dismissed || b.holdBack) return;
            var here = b.Here;
            if (here.Count == 0) return;
            var people = PactWords.People(b.faction).Named("PEOPLE");
            if (!inReach)
            {
                b.asleepSince = -1;
                b.muster = IntVec3.Invalid;
                if (!b.toldShut && !b.metEnemy)
                {
                    b.toldShut = true;
                    // apart: they stand by just outside the colony (the duty's focus), seen.
                    Tell.Message(NewsKind.World, Importance.Notable, b.map, "CC_WarBand_StandBy".Translate(people, PactWords.People(b.enemy).Named("ENEMY")).Resolve(), new LookTargets(here),
                        MessageTypeDefOf.NeutralEvent);
                }
                return;
            }
            var sleeper = Sleeper(here[0]);
            if (sleeper == null)
            {
                b.asleepSince = -1;
                b.muster = IntVec3.Invalid;
                return;
            }
            b.muster = MusterCell(b.map, sleeper, here[0]);
            if (b.asleepSince < 0) b.asleepSince = Now;
            var enemy = PactWords.People(b.enemy ?? sleeper.Faction).Named("ENEMY");
            if (!b.toldAsleep)
            {
                b.toldAsleep = true;
                // apart: they walk to the spot out of the sleepers' waking range and stand there, seen.
                Tell.Message(NewsKind.World, Importance.Notable, b.map, "CC_WarBand_WaitAsleep".Translate(people, enemy).Resolve(), new LookTargets(here), MessageTypeDefOf.NeutralEvent);
            }
            if (!b.goIn && CampStoryMath.GoIn(b.asleepSince, Now) && !AnyDrafted(b.map))
            {
                b.goIn = true;
                // apart: they walk to the nearest sleeper and attack it, which wakes them.
                Tell.Message(NewsKind.World, Importance.Notable, b.map, "CC_WarBand_GoIn".Translate(people, enemy).Resolve(), new LookTargets(here), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>The band's pawns on the game's own hunt from before this (an older save) get the band's duty.</summary>
        public static void Refresh(WarBand b)
        {
            var duty = HuntDuty;
            if (duty == null || !b.LordAlive || !(b.lord.CurLordToil is LordToil_WarBandHunt toil)) return;
            foreach (var p in b.lord.ownedPawns)
                if (p?.mindState != null && p.mindState.duty?.def != duty)
                {
                    toil.UpdateAllDuties();
                    return;
                }
        }

        private static IntVec3 MusterCell(Map map, Pawn sleeper, Pawn walker)
        {
            var colony = CampSite.ColonyCenter(map);
            var (x, z) = CampStoryMath.MusterPoint(sleeper.Position.x, sleeper.Position.z, colony.x, colony.z);
            var point = new IntVec3(x, 0, z).ClampInsideMap(map);
            if (CellFinder.TryFindRandomReachableNearbyCell(point, map, 8f, TraverseParms.For(walker, Danger.Deadly),
                    c => c.Standable(map) && !c.Fogged(map), null, out var cell))
                return cell;
            return walker.Position;
        }

        /// <summary>Wait at the spot near the sleeping enemy: walk there, then stand ready.</summary>
        public static Job WaitAt(Pawn pawn, IntVec3 spot)
        {
            if (!spot.IsValid) return null;
            if (pawn.Position.DistanceToSquared(spot) > 36)
            {
                if (!CellFinder.TryFindRandomReachableNearbyCell(spot, pawn.Map, 4f, TraverseParms.For(pawn, Danger.Deadly), c => c.Standable(pawn.Map), null, out var dest))
                    dest = spot;
                var go = JobMaker.MakeJob(JobDefOf.Goto, dest);
                go.expiryInterval = 600;
                go.checkOverrideOnExpire = true;
                return go;
            }
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, 250, true);
        }

        /// <summary>
        /// Attack a sleeping enemy, which wakes it: in melee, or from where their weapon reaches it. The game's own fight
        /// takes over once it is awake.
        /// </summary>
        public static Job Attack(Pawn pawn, Pawn target)
        {
            var verb = pawn.TryGetAttackVerb(target, !pawn.IsColonist);
            if (verb == null) return null;
            if (verb.verbProps.IsMeleeAttack)
            {
                var melee = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                melee.expiryInterval = 480;
                melee.checkOverrideOnExpire = true;
                return melee;
            }
            if (verb.CanHitTarget(target))
            {
                var shoot = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                shoot.endIfCantShootTargetFromCurPos = true;
                shoot.expiryInterval = 400;
                shoot.checkOverrideOnExpire = true;
                return shoot;
            }
            var req = new CastPositionRequest { caster = pawn, target = target, verb = verb, maxRangeFromTarget = verb.EffectiveRange, wantCoverFromTarget = verb.EffectiveRange > 5f };
            var go = JobMaker.MakeJob(JobDefOf.Goto, CastPositionFinder.TryFindCastPosition(req, out var dest) ? (LocalTargetInfo)dest : (LocalTargetInfo)target);
            go.expiryInterval = 500;
            go.checkOverrideOnExpire = true;
            return go;
        }
    }

    /// <summary>
    /// The game's hunt, with the band's own duty (Rimfolk_WarBandHunt): its data and its place in the group stay the
    /// game's, so a band from an older save loads into it unchanged. The duty's focus is a spot just outside the colony,
    /// where they stand by when the enemy is out of their reach.
    /// </summary>
    public class LordToil_WarBandHunt : LordToil_HuntEnemies
    {
        public LordToil_WarBandHunt(IntVec3 fallbackLocation) : base(fallbackLocation)
        {
        }

        public override void UpdateAllDuties()
        {
            var duty = WarBandOrders.HuntDuty;
            if (duty == null)
            {
                base.UpdateAllDuties();
                return;
            }
            var data = (LordToilData_HuntEnemies)this.data;
            IntVec3 standBy = IntVec3.Invalid;
            foreach (var p in lord.ownedPawns)
                if (p.Spawned && RCellFinder.TryFindRandomSpotJustOutsideColony(p, out standBy) && standBy.IsValid)
                    break;
            if (!standBy.IsValid) standBy = data.fallbackLocation;
            foreach (var p in lord.ownedPawns)
            {
                p.mindState.duty = new PawnDuty(duty, standBy);
                p.mindState.duty.focusSecond = data.fallbackLocation;
            }
        }
    }

    /// <summary>While any colonist on the map is drafted, the band goes with the nearest one and stands ready beside them.</summary>
    public class JobGiver_WarBandWithColony : ThinkNode_JobGiver
    {
        private const float Radius = 5f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (WarBandOrders.BandOf(pawn) == null) return null;
            var lead = WarBandOrders.Leader(pawn);
            if (lead == null) return null;
            if (JobDriver_FollowClose.FarEnoughAndPossibleToStartJob(pawn, lead, Radius))
            {
                var job = JobMaker.MakeJob(JobDefOf.FollowClose, lead);
                job.expiryInterval = 120;
                job.checkOverrideOnExpire = true;
                job.followRadius = Radius;
                return job;
            }
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, 90, true);
        }
    }

    /// <summary>
    /// A sleeping enemy in their reach: they wait out of its waking range for the colony's fighters, and once they go in
    /// (<see cref="WarBand.goIn"/>), they attack it.
    /// </summary>
    public class JobGiver_WarBandSleepers : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var band = WarBandOrders.BandOf(pawn);
            if (band == null) return null;
            var target = WarBandOrders.Sleeper(pawn);
            if (target == null) return null;
            return band.goIn ? WarBandOrders.Attack(pawn, target) : WarBandOrders.WaitAt(pawn, band.muster.IsValid ? band.muster : pawn.Position);
        }
    }
}

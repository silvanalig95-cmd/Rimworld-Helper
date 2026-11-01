using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The dead that mourners carry, as a real body in someone's arms: one of their people, dead at a good age, dressed,
    /// with nothing in their hands, named as the words name them ({NAME} of a world event). Mourners passing on the road
    /// (VisitEffect_Come bearDead) carry them past the colony and off the far side; a mourners' camp (CampKindDef.bearsDead)
    /// carries them in, lays them by its lamp while it stays and carries them home when it goes (Camp.CarryDead). The body
    /// is kept from rotting while it's with them, and the colony's haulers leave it be.
    /// </summary>
    public static class Borne
    {
        /// <summary>The body of one of a people, unspawned, or null if the game couldn't make one.</summary>
        public static Corpse MakeDead(Faction f, PawnKindDef kind, string name, Map map)
        {
            if (f == null || kind == null || map == null) return null;
            try
            {
                int g = VisitorActsMath.GenderOfName(name);
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, fixedGender: g == 1 ? Gender.Male : g == 2 ? Gender.Female : (Gender?)null,
                    biologicalAgeRange: new FloatRange(60f, 80f), dontGiveWeapon: true);
                req.Tile = map.Tile;
                var p = PawnGenerator.GeneratePawn(req);
                if (p == null) return null;
                p.equipment?.DestroyAllEquipment();
                p.inventory?.DestroyAll();
                if (!name.NullOrEmpty()) p.Name = new NameSingle(name);
                if (p.relations != null) p.relations.hidePawnRelations = true;
                // Dead off the map, of nothing that leaves a mark: the game makes the body, as it does for the old dead of ruins.
                p.Kill(null);
                var corpse = p.Corpse;
                // Passed to the world as one of its dead instead: the body made here, out of the world's keeping.
                if (corpse == null && p.Dead && !p.Discarded && p.holdingOwner == null)
                {
                    if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                    corpse = p.MakeCorpse(null, null);
                }
                if (corpse == null) return null;
                Keep(corpse);
                return corpse;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mourners' dead", e);
                return null;
            }
        }

        /// <summary>Kept from rotting while the mourners have it (set again after a load and at each look: the game doesn't save it).</summary>
        public static void Keep(Thing dead)
        {
            var rot = (dead as ThingWithComps)?.GetComp<CompRottable>();
            if (rot == null) return;
            rot.disabled = true;
            if (rot.RotProgress > 0f) rot.RotProgress = 0f;
        }

        /// <summary>Grown, on their feet, free-handed and able to carry.</summary>
        public static bool CanBear(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.DevelopmentalStage.Juvenile() && p.carryTracker != null
            && !p.WorkTagIsDisabled(WorkTags.Hauling) && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);

        /// <summary>The body set down beside <paramref name="bearer"/> and taken up: false if it couldn't be laid there.</summary>
        public static bool SetDownBy(Pawn bearer, Thing dead)
        {
            var map = bearer.Map;
            if (!CellFinder.TryFindRandomCellNear(bearer.Position, map, 2, c => c.Standable(map) && c.GetFirstItem(map) == null && c.GetEdifice(map) == null, out var cell)) cell = bearer.Position;
            if (GenSpawn.Spawn(dead, cell, map) == null) return false;
            dead.SetForbidden(true, false);
            return true;
        }

        /// <summary>
        /// <paramref name="bearer"/> takes the body up and carries it: to <paramref name="layAt"/> to lay it down there, or,
        /// with no place given, with the others of their group wherever it goes.
        /// </summary>
        public static bool Bear(Pawn bearer, Thing dead, IntVec3 layAt)
        {
            if (!CanBear(bearer) || dead == null || !dead.Spawned) return false;
            var job = JobMaker.MakeJob(VisitorActsDefOf.Rimfolk_VisitBear, dead, layAt.IsValid ? (LocalTargetInfo)layAt : LocalTargetInfo.Invalid);
            job.count = 1;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            bearer.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return bearer.CurJob == job;
        }

        // ---------------- Mourners passing on the road ----------------

        /// <summary>As the mourners set out from the edge: their dead, and one of them (not whoever walks at their head) to carry them.</summary>
        public static void Begin(VisitorTrip t)
        {
            if (t?.map == null || !t.bearDead || t.dead != null) return;
            var bearer = t.people.Skip(1).FirstOrDefault(CanBear) ?? t.people.FirstOrDefault(CanBear);
            if (bearer == null) return;
            var corpse = MakeDead(t.faction, Arrivals.PersonKind(t.faction) ?? bearer.kindDef, t.deadName, t.map);
            if (corpse == null || !SetDownBy(bearer, corpse)) return;
            t.dead = corpse;
            Bear(bearer, corpse, IntVec3.Invalid);
        }

        /// <summary>While they come and stay: their dead put down on the way (a bearer hungry, hurt, called away) is taken up again.</summary>
        public static void Watch(VisitorTrip t)
        {
            var dead = t?.dead;
            if (dead == null) return;
            if (dead.Destroyed || dead.MapHeld != t.map)
            {
                t.dead = null;
                return;
            }
            Keep(dead);
            // Staying the night at their spot, the dead lies there beside them until they go.
            if (t.kind == VisitKind.Stay && t.state == VisitState.Here) return;
            if (!dead.Spawned || t.Here.Any(p => p.CurJob != null && p.CurJob.targetA.Thing == dead)) return;
            var bearer = t.Standing.Where(p => p.carryTracker?.CarriedThing == null && CanBear(p) && p.CanReserveAndReach(dead, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(dead.Position)).FirstOrDefault();
            if (bearer != null) Bear(bearer, dead, IntVec3.Invalid);
        }

        /// <summary>
        /// They're going: what's theirs that's lying about (their dead, a kill not yet dressed) is taken up by one of them
        /// and carried off the map; and one carrying it still who isn't on their way out with it turns for the edge now.
        /// </summary>
        public static void CarryOff(VisitorTrip t, bool hurry)
        {
            if (t?.map == null || !t.MapThere) return;
            var taken = new HashSet<Pawn>();
            foreach (var thing in new[] { t.dead, t.kill })
            {
                if (thing == null || thing.Destroyed || thing.MapHeld != t.map) continue;
                if (thing.ParentHolder is Pawn_CarryTracker ct)
                {
                    var holder = ct.pawn;
                    // Already on their way out with it (taking it off the map, or walking with the others).
                    if (holder.CurJobDef == CampDefOf.Council_CampCarryHome || holder.CurJobDef == VisitorActsDefOf.Rimfolk_VisitBear) continue;
                    if (!t.people.Contains(holder)) continue;
                    if (TakeOff(holder, thing, t, hurry)) taken.Add(holder);
                    continue;
                }
                if (!thing.Spawned || t.Here.Any(p => p.CurJobDef == CampDefOf.Council_CampCarryHome && p.CurJob.targetA.Thing == thing)) continue;
                var bearer = t.Standing.Where(p => !taken.Contains(p) && p.carryTracker?.CarriedThing == null && CanBear(p) && p.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderBy(p => p.Position.DistanceToSquared(thing.Position)).FirstOrDefault();
                if (bearer != null && TakeOff(bearer, thing, t, hurry)) taken.Add(bearer);
            }
        }

        /// <summary>The game's own taking a thing and leaving the map with it: to their way off on the far side if they're going on, else the nearest edge.</summary>
        private static bool TakeOff(Pawn p, Thing thing, VisitorTrip t, bool hurry)
        {
            try
            {
                IntVec3 exit;
                if (p.GetLord()?.CurLordToil is LordToil_Travel && t.exit.IsValid && p.CanReach(t.exit, PathEndMode.OnCell, Danger.Some)) exit = t.exit;
                else if (!RCellFinder.TryFindBestExitSpot(p, out exit)) return false;
                var job = JobMaker.MakeJob(CampDefOf.Council_CampCarryHome, thing, exit);
                job.count = 1;
                job.locomotionUrgency = hurry ? LocomotionUrgency.Jog : LocomotionUrgency.Walk;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return p.CurJob == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitors carry theirs off", e);
                return false;
            }
        }
    }

    /// <summary>
    /// One of the mourners carrying their dead in their arms: the body taken up, then either carried to where it's to lie
    /// (a camp's lamp) and laid down there, or carried with the others wherever their group goes (to their spot, on past
    /// the colony), until they go off the map, when the game's own leaving with it takes over.
    /// </summary>
    public class JobDriver_VisitBear : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => job.targetA.Thing == null || job.targetA.Thing.Destroyed);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            if (job.targetB.IsValid)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                var lay = ToilMaker.MakeToil("BearLay");
                lay.initAction = () =>
                {
                    if (!pawn.carryTracker.TryDropCarriedThing(job.targetB.Cell, ThingPlaceMode.Near, out var put) || put == null) return;
                    put.SetForbidden(true, false);
                    Borne.Keep(put);
                };
                lay.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return lay;
                yield break;
            }
            var walk = ToilMaker.MakeToil("BearWalk");
            walk.initAction = Step;
            walk.tickIntervalAction = delta =>
            {
                if (pawn.IsHashIntervalTick(90, delta)) Step();
            };
            walk.defaultCompleteMode = ToilCompleteMode.Never;
            yield return walk;
        }

        /// <summary>Keep with the group: to where it's travelling or the spot it keeps to; once it's leaving the map, off with the body.</summary>
        private void Step()
        {
            var toil = pawn.GetLord()?.CurLordToil;
            var dead = pawn.carryTracker?.CarriedThing;
            if (toil == null || dead == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (toil is LordToil_ExitMap || toil is LordToil_ExitMapAndDefendSelf)
            {
                // The game's own leaving with something takes it from here.
                if (RCellFinder.TryFindBestExitSpot(pawn, out var exit))
                {
                    var off = JobMaker.MakeJob(CampDefOf.Council_CampCarryHome, dead, exit);
                    off.count = 1;
                    pawn.jobs.jobQueue.EnqueueFirst(off);
                }
                EndJobWith(JobCondition.Succeeded);
                return;
            }
            var dest = toil.FlagLoc;
            if (!dest.IsValid || pawn.Position.InHorDistOf(dest, 3f))
            {
                if (pawn.pather.Moving) pawn.pather.StopDead();
                // Staying the night at their spot: the dead is laid down there beside them, and taken up again when they go.
                if (toil is LordToil_VisitHere && VisitorTracker.Instance?.TripOf(pawn)?.kind == VisitKind.Stay)
                {
                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var put) && put != null)
                    {
                        put.SetForbidden(true, false);
                        Borne.Keep(put);
                    }
                    EndJobWith(JobCondition.Succeeded);
                }
                return;
            }
            if (pawn.pather.Moving && pawn.pather.Destination.Cell.InHorDistOf(dest, 3f)) return;
            var map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(dest, map, 2, c => c.Standable(map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some), out var to)) to = dest;
            pawn.pather.StartPath(to, PathEndMode.OnCell);
        }
    }
}

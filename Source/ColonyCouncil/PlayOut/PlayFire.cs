using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A bonfire with no fire to gather at (<see cref="PlayOut.buildFire"/>): before it starts, one of the colony carries
    /// wood out from the stores to a place in the open near the colony's middle, lays a campfire there (the game's own)
    /// and lights it, and the gathering is held round it once it burns. When the gathering is over, the fire laid for it
    /// burns down to ash (LordJob_PlayOut.laidFire). With the wood already paid for by the answer, they lay it with what
    /// they find there. With no wood in the stores, or nowhere in the open to lay it, there is no fire, and the answer's
    /// other places are tried, as before.
    /// </summary>
    public static class PlayFire
    {
        private static ThingDef Def => ThingDefOf.Campfire;

        /// <summary>The wood a campfire takes (its cost), carried out to it.</summary>
        public static int Cost => Def?.costList?.FirstOrDefault(c => c.thingDef == ThingDefOf.WoodLog)?.count ?? 20;

        /// <summary>
        /// The gathering waits for its fire: a fire is being laid, or one is set going now. False: there's a fire to
        /// gather at already (or it was laid), or none can be laid (the venue is found as before).
        /// </summary>
        public static bool Waits(PlayOut p, Pawn organizer, List<Pawn> able)
        {
            var map = p.map;
            if (!p.buildFire || map == null || Def == null || p.spots == null || !p.spots.Contains(PlaySpot.Fire)) return false;
            if (p.fireLaid != null && p.fireLaid.Spawned) return false;
            if (PlaySpots.ColonyFire(map, organizer) != null) return false;
            // Someone is out there laying it.
            if (Laying(p))
            {
                p.waitReason = "CC_Play_Why_LayingFire".Translate(p.fireBy.Named("PAWN")).Resolve();
                return true;
            }
            p.fireBy = null;
            if (!Def.IsResearchFinished || !Def.BuildableByPlayer) return false;
            var by = new[] { organizer }.Concat(able).FirstOrDefault(x => Can(x, map));
            if (by == null) return false;
            Thing wood = null;
            if (!p.firePaid)
            {
                wood = Wood(map, by);
                if (wood == null) return false;
            }
            var cell = Cell(map, by);
            if (!cell.IsValid) return false;
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutLayFire, cell, wood);
            job.count = wood != null ? Math.Min(Cost, wood.stackCount) : 1;
            // together: the fire is laid out in the open before the colony gathers round it.
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (by.CurJob != job) return false;
            p.fireBy = by;
            p.fireCell = cell;
            Messages.Message("CC_Play_Begin_LayFire".Translate(by.Named("PAWN")).Resolve(), new LookTargets(by), MessageTypeDefOf.NeutralEvent, false);
            p.waitReason = "CC_Play_Why_LayingFire".Translate(by.Named("PAWN")).Resolve();
            return true;
        }

        /// <summary>The one laying the fire is still at it.</summary>
        private static bool Laying(PlayOut p) =>
            p.fireBy != null && !p.fireBy.Dead && p.fireBy.Spawned && p.fireBy.CurJobDef == PlayOutDefOf.Council_PlayOutLayFire && p.fireBy.CurJob.targetA.Cell == p.fireCell;

        /// <summary>The fire is laid and lit: the gathering it was for holds it, to let it burn down after.</summary>
        public static void Laid(Pawn by, IntVec3 cell, Thing fire)
        {
            var p = PlayOutTracker.Instance?.plays.FirstOrDefault(x => x.fireBy == by && x.fireCell == cell && x.state == PlayState.Pending);
            if (p != null) p.fireLaid = fire;
        }

        private static bool Can(Pawn x, Map map) =>
            x != null && x.Spawned && x.Map == map && !x.DevelopmentalStage.Juvenile() && x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            && !Beliefs.Forbids(x, Belief.Indoors);

        /// <summary>Wood from the colony's stores, the stack nearest the one who carries it, or null with too little anywhere.</summary>
        private static Thing Wood(Map map, Pawn by)
        {
            var def = ThingDefOf.WoodLog;
            if (def == null || map.resourceCounter.GetCount(def) < Cost) return null;
            return map.listerThings.ThingsOfDef(def)
                .Where(t => t.Spawned && !t.IsForbidden(by) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]) && by.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderByDescending(t => t.stackCount >= Cost).ThenBy(t => t.Position.DistanceToSquared(by.Position)).FirstOrDefault();
        }

        /// <summary>Open ground near the colony's middle where a campfire can go, with room to stand round it.</summary>
        public static IntVec3 Cell(Map map, Pawn by)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 18f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetZone(map) != null) continue;
                if (c.GetTerrain(map)?.IsWater ?? false) continue;
                if (!PlaySpots.Ok(c, map, by) || !GenConstruct.CanPlaceBlueprintAt(Def, c, Rot4.North, map).Accepted) continue;
                if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Count(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null) < 6) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The campfire on its cell, burning: the wood carried out goes into it.</summary>
        public static Thing Light(Map map, IntVec3 cell, Pawn by)
        {
            if (map == null || Def == null || !cell.InBounds(map) || cell.GetEdifice(map) != null) return null;
            var fire = ThingMaker.MakeThing(Def);
            fire.SetFaction(Faction.OfPlayer);
            fire = GenSpawn.Spawn(fire, cell, map);
            var refuel = fire.TryGetComp<CompRefuelable>();
            refuel?.Refuel(refuel.Props.fuelCapacity);
            FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, 1.2f);
            Laid(by, cell, fire);
            return fire;
        }
    }

    /// <summary>
    /// Laying a fire for a gathering (PlayFire.cs): to the wood in the stores and up with it, out to the place, a while
    /// on their knees setting it and coaxing it alight, and the campfire is there, burning.
    /// </summary>
    public class JobDriver_PlayOutLayFire : JobDriver
    {
        private bool lit;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.GetTarget(TargetIndex.B).HasThing || pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn));
            AddFinishAction(cond =>
            {
                try
                {
                    if (!lit && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fire laying end", e);
                }
            });
            if (job.GetTarget(TargetIndex.B).HasThing)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            }
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var lay = ToilMaker.MakeToil("PlayOutLayFire");
            lay.defaultCompleteMode = ToilCompleteMode.Delay;
            lay.defaultDuration = 500;
            lay.handlingFacing = true;
            lay.tickIntervalAction = delta =>
            {
                var cell = job.GetTarget(TargetIndex.A).Cell;
                pawn.rotationTracker.FaceCell(cell);
                if (Rand.Chance(0.04f * delta)) FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), pawn.Map);
            };
            lay.WithProgressBarToilDelay(TargetIndex.A);
            yield return lay;
            var done = ToilMaker.MakeToil("PlayOutFireLit");
            done.initAction = () =>
            {
                try
                {
                    var fire = PlayFire.Light(pawn.Map, job.GetTarget(TargetIndex.A).Cell, pawn);
                    lit = fire != null;
                    // The wood goes into the fire.
                    if (lit && pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.DestroyCarriedThing();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fire lit", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lit, "lit");
        }
    }
}

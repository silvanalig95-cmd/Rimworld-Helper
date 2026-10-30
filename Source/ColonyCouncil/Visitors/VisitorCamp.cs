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
    /// A visit's camp out on the colony's land (pilgrims and prospectors by the water, gleaners and drovers on the far
    /// ground, a stargazer by the road): once they're there for the night they lay a campfire of their own wood, keep
    /// it fed while they stay, sit round it of an evening, and put it out when they go, leaving its ash. Colonists may
    /// walk out to sit with them by it (JoyGiver_SitWithVisitors). Never at the gate or for guests shown a corner inside.
    /// </summary>
    public static class VisitorCamp
    {
        /// <summary>Staying the night out on the land, not by the gate and not inside.</summary>
        public static bool Camps(VisitorTrip t)
        {
            if (t == null || t.kind != VisitKind.Stay || t.state != VisitState.Here) return false;
            var where = t.staySpot != VisitSpot.Gate ? t.staySpot : t.spotKind;
            if (where == VisitSpot.Gate) return false;
            return !(t.lord?.LordJob is LordJob_RimfolkVisit j && j.indoors);
        }

        /// <summary>The fire of the trip whose people this is, if it burns.</summary>
        public static Thing FireOf(Pawn p) =>
            p?.GetLord()?.LordJob is LordJob_RimfolkVisit j && VisitorTracker.Instance?.Get(j.tripId)?.fire is Thing f && f.Spawned ? f : null;

        /// <summary>Each step while they're here: the fire laid once they've settled, and fed.</summary>
        public static void Tend(VisitorTrip t)
        {
            if (!Camps(t)) return;
            try
            {
                if (t.fire == null && !t.fireLaid) Lay(t);
                var fuel = (t.fire as ThingWithComps)?.GetComp<CompRefuelable>();
                if (fuel != null && fuel.FuelPercentOfMax < 0.5f) fuel.Refuel(fuel.Props.fuelCapacity - fuel.Fuel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor camp fire", e);
            }
        }

        private static void Lay(VisitorTrip t)
        {
            t.fireLaid = true;
            var map = t.map;
            var def = ThingDefOf.Campfire;
            if (map == null || def == null || !t.spot.IsValid) return;
            foreach (var c in GenRadial.RadialCellsAround(t.spot, 4f, true))
            {
                if (!VisitorWork.OutsideOk(c, map) || c.Roofed(map) || c.GetFirstPawn(map) != null || c.GetFirstItem(map) != null || c.GetFirstBuilding(map) != null) continue;
                var terrain = c.GetTerrain(map);
                if (terrain == null || terrain.IsWater || def.terrainAffordanceNeeded != null && !terrain.affordances.Contains(def.terrainAffordanceNeeded)) continue;
                var fire = ThingMaker.MakeThing(def);
                fire.SetFaction(t.faction);
                GenSpawn.Spawn(fire, c, map);
                var fuel = (fire as ThingWithComps)?.GetComp<CompRefuelable>();
                fuel?.Refuel(fuel.Props.fuelCapacity);
                t.fire = fire;
                return;
            }
        }

        /// <summary>Their fire put out as they go: the ash stays where it burned.</summary>
        public static void PutOut(VisitorTrip t)
        {
            var fire = t?.fire;
            if (fire == null) return;
            t.fire = null;
            try
            {
                if (fire.Destroyed || !fire.Spawned) return;
                var map = fire.Map;
                var cell = fire.Position;
                fire.Destroy(DestroyMode.Vanish);
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Ash, 2);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor camp fire out", e);
            }
        }

        /// <summary>A visit's fire that colonists may walk out to: burning, its people there and awake, a few of them by it.</summary>
        public static Thing OpenFire(Pawn colonist)
        {
            var map = colonist?.Map;
            var tracker = VisitorTracker.Instance;
            if (map == null || tracker == null) return null;
            Thing best = null;
            float bestDist = 80f * 80f;
            foreach (var t in tracker.trips)
            {
                var fire = t.fire;
                if (fire == null || !fire.Spawned || fire.Map != map || t.state != VisitState.Here || t.faction == null || t.faction.HostileTo(Faction.OfPlayer)) continue;
                if (!t.Standing.Any(p => p.Awake() && p.Position.InHorDistOf(fire.Position, 6f))) continue;
                float d = (fire.Position - colonist.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = fire;
                }
            }
            return best;
        }

        /// <summary>A place in the ring round a fire, facing it.</summary>
        public static bool RingCell(Pawn p, IntVec3 middle, out IntVec3 at)
        {
            var map = p.Map;
            at = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(middle, 3f, false).InRandomOrder())
            {
                if (c.DistanceTo(middle) < 1.5f || !c.Standable(map) || c.Roofed(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null) continue;
                if (map.zoneManager.ZoneAt(c) is Zone_Growing || !p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                at = c;
                return true;
            }
            return false;
        }
    }

    /// <summary>Colonists at leisure walk out to sit with visitors by their fire a while: a chat, a seat in the ring.</summary>
    public class JoyGiver_SitWithVisitors : JoyGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.IsFreeColonist || pawn.Drafted || pawn.DevelopmentalStage.Baby() || pawn.needs?.joy == null || MapFacts.ThreatOn(pawn.Map)) return null;
            var fire = VisitorCamp.OpenFire(pawn);
            if (fire == null) return null;
            // A few of the colony's at a fire at once, no more.
            int there = pawn.Map.mapPawns.FreeColonistsSpawned.Count(c => c.CurJobDef == def.jobDef && c.CurJob?.targetB.Thing == fire);
            if (there >= 3 || !VisitorCamp.RingCell(pawn, fire.Position, out var cell)) return null;
            return JobMaker.MakeJob(def.jobDef, cell, fire);
        }
    }

    /// <summary>A colonist sitting with visitors by their fire, facing it and talking, until their recreation is full or the fire is out.</summary>
    public class JobDriver_SitWithVisitors : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => job.targetB.Thing == null || !job.targetB.Thing.Spawned || job.targetB.Thing.Map != pawn.Map);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var sit = ToilMaker.MakeToil("SitWithVisitors");
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = Mathf.Max(600, job.def.joyDuration);
            sit.socialMode = RandomSocialMode.SuperActive;
            sit.handlingFacing = true;
            sit.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetB.Cell);
                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob);
            };
            yield return sit;
        }
    }
}

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
    /// What visitors do at their spot when the letter says they came to do something (a visit's <c>work</c>): the
    /// prospectors pan the creek by day and on into the evening by lamplight, gleaners go over the colony's stubble,
    /// pilgrims sing at their camp after dark, hunters walk the far side watching for game. And where visitors out of
    /// doors may be: they sleep when they're tired or the night has come, on open ground near their spot, and walk only
    /// there, never in the colony's rooms or on its fields (the duties in Defs/Misc/Rimfolk_Visitors.xml).
    /// </summary>
    public static class VisitorWork
    {
        public const string Prospect = "prospect", Glean = "glean", Sing = "sing", Hunt = "hunt";

        public static readonly string[] Kinds = { Prospect, Glean, Sing, Hunt };

        /// <summary>The hours of the day the work is done in: panning goes on by lamplight, singing waits for dark.</summary>
        public static bool InHours(string work, int hour)
        {
            switch (work)
            {
                case Prospect: return hour >= 6 && hour < 22;
                case Glean: return hour >= 6 && hour < 18;
                case Sing: return hour >= 19 && hour < 23;
                case Hunt: return hour >= 5 && hour < 19;
                default: return false;
            }
        }

        /// <summary>The work of the visit this pawn is with, if any.</summary>
        public static string WorkOf(Pawn p) => p?.GetLord()?.LordJob is LordJob_RimfolkVisit j ? VisitorTracker.Instance?.Get(j.tripId)?.work : null;

        /// <summary>A cell visitors out of doors may stand, walk or sleep on: in the open, off the colony's fields, with nothing of the colony's built on it.</summary>
        public static bool OutsideOk(IntVec3 c, Map map)
        {
            if (map == null || !c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) return false;
            if (!c.UsesOutdoorTemperature(map)) return false;
            if (map.zoneManager.ZoneAt(c) is Zone_Growing) return false;
            var edifice = c.GetEdifice(map);
            return edifice == null || edifice.Faction != Faction.OfPlayer;
        }

        /// <summary>Asleep soon: tired, or the night has come and they could do with it.</summary>
        public static bool WantsSleep(Pawn p)
        {
            var rest = p.needs?.rest;
            if (rest == null) return false;
            float level = rest.CurLevelPercentage;
            if (level < 0.28f) return true;
            int hour = GenLocalDate.HourOfDay(p.Map);
            return (hour >= 22 || hour < 6) && level < 0.8f;
        }

        private static IntVec3 Focus(Pawn p) => p.mindState?.duty != null && p.mindState.duty.focus.IsValid ? p.mindState.duty.focus.Cell : p.Position;

        /// <summary>Open ground near their spot to sleep on.</summary>
        public static IntVec3 SleepCell(Pawn p)
        {
            var map = p.Map;
            var focus = Focus(p);
            foreach (int radius in new[] { 5, 9, 12 })
                if (CellFinder.TryFindRandomCellNear(focus, map, radius, c => OutsideOk(c, map) && c.GetFirstPawn(map) == null && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                    return cell;
            return IntVec3.Invalid;
        }

        private static bool Water(IntVec3 c, Map map) => c.InBounds(map) && (c.GetTerrain(map)?.IsWater ?? false);

        /// <summary>A place on the bank (or in the shallows) to work the water from, and the water to face.</summary>
        private static bool Bank(Pawn p, out IntVec3 at, out IntVec3 face)
        {
            var map = p.Map;
            var focus = Focus(p);
            at = face = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(focus, 12f, true).InRandomOrder().Take(160))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || c.GetFirstPawn(map) != null) continue;
                if (!Water(c, map) && !OutsideOk(c, map)) continue;
                var water = GenAdj.CardinalDirections.Select(d => c + d).FirstOrDefault(n => Water(n, map));
                if (!water.IsValid && !Water(c, map)) continue;
                if (!p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                at = c;
                face = water.IsValid ? water : c + p.Rotation.FacingCell;
                return true;
            }
            return false;
        }

        /// <summary>A bare cell of the colony's fields: the stubble a harvest leaves.</summary>
        private static bool Stubble(Pawn p, out IntVec3 at)
        {
            var map = p.Map;
            at = IntVec3.Invalid;
            var cells = new List<IntVec3>();
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in zone.Cells)
                    if (c.GetPlant(map) == null && !c.Roofed(map) && c.Standable(map)) cells.Add(c);
            foreach (var c in cells.OrderBy(c => c.DistanceToSquared(p.Position)).Take(80).InRandomOrder())
            {
                if (c.GetFirstPawn(map) != null || !p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                at = c;
                return true;
            }
            return false;
        }

        /// <summary>A place in the ring round their camp, facing its middle.</summary>
        private static bool Ring(Pawn p, out IntVec3 at, out IntVec3 face)
        {
            var map = p.Map;
            face = Focus(p);
            at = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(face, 3f, false).InRandomOrder())
            {
                if (c.DistanceTo(face) < 1.5f || !OutsideOk(c, map) || c.GetFirstPawn(map) != null || !p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                at = c;
                return true;
            }
            return false;
        }

        /// <summary>Somewhere on the far side to stand and watch for game, away from the colony.</summary>
        private static bool Lookout(Pawn p, out IntVec3 at, out IntVec3 face)
        {
            var map = p.Map;
            var focus = Focus(p);
            at = face = IntVec3.Invalid;
            if (!CellFinder.TryFindRandomCellNear(focus, map, 18, c => OutsideOk(c, map) && !(map.areaManager?.Home?[c] ?? false) && c.GetFirstPawn(map) == null
                                                                  && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return false;
            at = cell;
            face = cell + (cell - focus).ToVector3().normalized.ToIntVec3() * 4;
            if (!face.InBounds(map) || face == cell) face = cell + Rot4.Random.FacingCell * 4;
            return true;
        }

        /// <summary>The next piece of work for one of them, or null when there's none to do now.</summary>
        public static Job JobFor(Pawn p, string work)
        {
            if (p?.Map == null || work.NullOrEmpty() || !InHours(work, GenLocalDate.HourOfDay(p.Map)) || WantsSleep(p)) return null;
            IntVec3 at, face;
            switch (work)
            {
                case Prospect:
                    return Bank(p, out at, out face) ? JobMaker.MakeJob(VisitorDefOf.Rimfolk_VisitProspect, at, face) : null;
                case Glean:
                    return Stubble(p, out at) ? JobMaker.MakeJob(VisitorDefOf.Rimfolk_VisitGlean, at, at) : null;
                case Sing:
                    return Ring(p, out at, out face) ? JobMaker.MakeJob(VisitorDefOf.Rimfolk_VisitSing, at, face) : null;
                case Hunt:
                    return Lookout(p, out at, out face) ? JobMaker.MakeJob(VisitorDefOf.Rimfolk_VisitHunt, at, face) : null;
            }
            return null;
        }
    }

    /// <summary>The game's own rest for visitors, only when they're tired or night has come, and out of doors on open ground near their spot rather than inside the colony.</summary>
    public class JobGiver_VisitRest : JobGiver_GetRest
    {
        /// <summary>Guests shown a corner inside: they sleep where the game finds them a place, in their room.</summary>
        public bool indoors;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (JobGiver_VisitRest)base.DeepCopy(resolve);
            copy.indoors = indoors;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || !VisitorWork.WantsSleep(pawn)) return null;
            var job = base.TryGiveJob(pawn);
            if (job == null || job.def != JobDefOf.LayDown || indoors) return job;
            if (!job.targetA.HasThing && VisitorWork.OutsideOk(job.targetA.Cell, pawn.Map)) return job;
            var c = VisitorWork.SleepCell(pawn);
            return c.IsValid ? JobMaker.MakeJob(JobDefOf.LayDown, c) : null;
        }
    }

    /// <summary>Visitors (and their animals) walk about near their spot, never into the colony's rooms or onto its fields.</summary>
    public class JobGiver_VisitWander : JobGiver_WanderNearDutyLocation
    {
        public JobGiver_VisitWander()
        {
            wanderDestValidator = (pawn, loc, root) => VisitorWork.OutsideOk(loc, pawn.Map);
        }
    }

    /// <summary>The work the visit came to do, in its hours (VisitorWork.JobFor).</summary>
    public class JobGiver_VisitWork : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => VisitorWork.JobFor(pawn, VisitorWork.WorkOf(pawn));
    }

    /// <summary>How a visitor's work looks: the game's effect while it's done (dirt for digging, chaff for gleaning), notes over a singer's head, how long one stint lasts.</summary>
    public class VisitWorkExtension : DefModExtension
    {
        public string work;
        public EffecterDef effecter;
        public bool music;
        public IntRange ticks = new IntRange(1200, 2400);
    }

    /// <summary>A stint of a visitor's work: walk to the place, then work there facing the water, the field or the camp's middle, until the stint or its hours are over.</summary>
    public class JobDriver_VisitWork : JobDriver
    {
        private static FleckDef musicFleck;
        private static bool looked;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ext = job.def.GetModExtension<VisitWorkExtension>() ?? new VisitWorkExtension();
            this.FailOn(() => !ext.work.NullOrEmpty() && !VisitorWork.InHours(ext.work, GenLocalDate.HourOfDay(pawn.Map)));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var work = ToilMaker.MakeToil("VisitWork");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, ext.ticks.RandomInRange);
            work.handlingFacing = true;
            work.tickIntervalAction = delta =>
            {
                if (job.targetB.IsValid && job.targetB.Cell != pawn.Position) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                if (ext.music && VisualTouches.On && pawn.IsHashIntervalTick(240, delta))
                {
                    if (!looked)
                    {
                        looked = true;
                        musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                    }
                    if (musicFleck != null) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, musicFleck);
                }
            };
            if (ext.effecter != null) work.WithEffect(ext.effecter, TargetIndex.B);
            yield return work;
        }
    }
}

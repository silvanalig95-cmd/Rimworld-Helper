using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampCleanDefOf
    {
        public static JobDef Rimfolk_CampClean;

        static CampCleanDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampCleanDefOf));
        }
    }

    // Clean ground round their camp (CampDayMath.Cleans): in their free hours one of them cleans the mess on their own
    // ground (blood, vomit, ash, trash, dirt, rubble), the game's own cleaning to look at, the orderly kinds often and the
    // careless seldom; going in their own time, the orderly tidy their site before they set off. Never the colony's ground,
    // never a room of the colony's.
    public partial class Camp
    {
        [Unsaved] private int nextCleanSearch = -1;
        [Unsaved] private int filthCountTick = -1, filthCount;

        /// <summary>How tidy their kind keeps its ground (CampKindDef.Tidy).</summary>
        public CampTidy Tidy => kind?.Tidy ?? CampTidy.Usual;

        /// <summary>How far round their fire their own ground reaches, for cleaning.</summary>
        public float CleanRadius => CampSite.CoreRadius + 2f;

        /// <summary>The facts about a bit of filth near them, for whether they clean it (<paramref name="huts"/>: the insides of their own huts and tents).</summary>
        private CleanFacts CleanFactsOf(Filth f, Pawn cleaner, HashSet<IntVec3> huts)
        {
            var c = f.Position;
            var facts = new CleanFacts
            {
                mess = CampDayMath.Mess(f.def.defName),
                onTheirGround = c.InHorDistOf(FireCell, CleanRadius),
                colonyGround = ColonyGround(c)
            };
            var room = c.GetRoom(map);
            if (room != null && !room.PsychologicallyOutdoors && !huts.Contains(c)) facts.colonyRoom = true;
            facts.reserved = cleaner != null ? !cleaner.CanReserve(f) : map.reservationManager.IsReservedByAnyoneOf(f, faction);
            return facts;
        }

        /// <summary>The colony's ground: its home area within a few steps, a zone, a plan or a mark on the cell.</summary>
        private bool ColonyGround(IntVec3 c)
        {
            if (map.zoneManager.ZoneAt(c) != null || CampSite.Marked(c, map)) return true;
            var home = map.areaManager.Home;
            if (home == null) return false;
            foreach (var x in GenRadial.RadialCellsAround(c, 3f, true))
                if (x.InBounds(map) && home[x]) return true;
            return false;
        }

        /// <summary>The filth on their ground they would clean.</summary>
        private List<Filth> FilthToClean(Pawn cleaner)
        {
            var list = new List<Filth>();
            if (map == null) return list;
            HashSet<IntVec3> huts = null;
            foreach (var t in GenRadial.RadialDistinctThingsAround(FireCell, map, CleanRadius, true))
            {
                if (!(t is Filth f) || !f.Spawned || f.Position.Fogged(map) || !CampDayMath.Mess(f.def.defName)) continue;
                huts ??= HutCells;
                if (CampDayMath.Cleans(CleanFactsOf(f, cleaner, huts))) list.Add(f);
            }
            return list;
        }

        /// <summary>How much filth lies on their ground now (counted at most once an hour).</summary>
        public int FilthOnGround()
        {
            if (filthCountTick >= 0 && Now - filthCountTick < GenDate.TicksPerHour) return filthCount;
            filthCountTick = Now;
            filthCount = FilthToClean(null).Count;
            return filthCount;
        }

        /// <summary>
        /// A round of cleaning for one of them: the filth on their ground nearest them and up to a dozen bits round it, when
        /// there is enough of it for their kind to bother (CampDayMath.CleanFrom) and no more of them are at it already than
        /// their kind would send (<paramref name="leaving"/>: tidying the site before they go, every last bit).
        /// </summary>
        public Job CleanJob(Pawn p, bool leaving = false)
        {
            if (p == null || map == null || p.DevelopmentalStage.Baby() || p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)) return null;
            if (Now < nextCleanSearch) return null;
            var tidy = Tidy;
            if (Here.Count(o => o != p && o.CurJobDef == CampCleanDefOf.Rimfolk_CampClean) >= CampDayMath.Cleaners(tidy, Here.Count)) return null;
            var filth = FilthToClean(p);
            if (filth.Count == 0 || (!leaving && filth.Count < CampDayMath.CleanFrom(tidy)))
            {
                nextCleanSearch = Now + GenDate.TicksPerHour;
                return null;
            }
            var first = filth.Where(f => p.CanReach(f, PathEndMode.Touch, Danger.Some)).OrderBy(f => f.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (first == null)
            {
                nextCleanSearch = Now + GenDate.TicksPerHour;
                return null;
            }
            var job = JobMaker.MakeJob(CampCleanDefOf.Rimfolk_CampClean);
            job.AddQueuedTarget(TargetIndex.A, first);
            foreach (var f in filth.Where(x => x != first && x.Position.InHorDistOf(first.Position, 6f)).OrderBy(x => x.Position.DistanceToSquared(first.Position)).Take(11))
                job.AddQueuedTarget(TargetIndex.A, f);
            if (job.targetQueueA.Count >= 5) job.targetQueueA.SortBy(t => t.Cell.DistanceToSquared(p.Position));
            filthCountTick = -1;
            return job;
        }

        /// <summary>The orderly, going in their own time, still have filth on their ground to clean before they set off.</summary>
        public bool ToTidy() => packingFor != null && CampDayMath.TidiesBeforeLeaving(Tidy, CampPackMath.Orderly(packingFor)) && FilthOnGround() > 0;

        /// <summary>Filth they may clean: still lying on their own ground (the cleaning job asks it before each bit).</summary>
        public bool MayClean(Thing t) => t is Filth f && f.Spawned && f.Map == map && CampDayMath.Mess(f.def.defName) && CampDayMath.Cleans(CleanFactsOf(f, null, HutCells));

        /// <summary>The insides of their huts and tents, worked out once an hour (for the cleaning job's checks).</summary>
        private HashSet<IntVec3> HutCells
        {
            get
            {
                if (hutCells == null || Now - hutCellsTick > GenDate.TicksPerHour)
                {
                    hutCells = new HashSet<IntVec3>(CampSite.HutInside(plans));
                    hutCellsTick = Now;
                }
                return hutCells;
            }
        }

        [Unsaved] private HashSet<IntVec3> hutCells;
        [Unsaved] private int hutCellsTick = -1;
    }

    /// <summary>
    /// A camper cleaning filth on their camp's ground: the game's own cleaning (its look, its sound, its pace), bit by bit
    /// through the queue, without the colony's rule that cleaning is only for its home area. Each bit is asked again before
    /// it's cleaned: never one on the colony's ground or in a room of the colony's.
    /// </summary>
    public class JobDriver_CampClean : JobDriver
    {
        private float cleaningWorkDone, totalCleaningWorkDone, totalCleaningWorkRequired;

        private Filth Filth => job.GetTarget(TargetIndex.A).Thing as Filth;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            return true;
        }

        private bool NotTheirs()
        {
            var camp = CampTracker.CampOf(pawn);
            return camp == null || !camp.MayClean(job.GetTarget(TargetIndex.A).Thing);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CampTracker.CampOf(pawn) == null);
            var next = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A);
            yield return next;
            yield return Toils_JobTransforms.SucceedOnNoTargetInQueue(TargetIndex.A);
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, next).JumpIf(NotTheirs, next);
            var clean = ToilMaker.MakeToil("CampClean");
            clean.initAction = () =>
            {
                cleaningWorkDone = 0f;
                totalCleaningWorkDone = 0f;
                var f = Filth;
                totalCleaningWorkRequired = f == null ? 1f : Mathf.Max(1f, f.def.filth.cleaningWorkToReduceThickness * f.thickness);
            };
            clean.tickIntervalAction = delta =>
            {
                var f = Filth;
                if (f == null || f.Destroyed) return;
                float factor = f.Position.GetTerrain(f.Map)?.GetStatValueAbstract(StatDefOf.CleaningTimeFactor) ?? 1f;
                float work = pawn.GetStatValue(StatDefOf.CleaningSpeed) * delta;
                if (factor != 0f) work /= factor;
                cleaningWorkDone += work;
                totalCleaningWorkDone += work;
                if (cleaningWorkDone > f.def.filth.cleaningWorkToReduceThickness)
                {
                    f.ThinFilth();
                    cleaningWorkDone = 0f;
                    if (f.Destroyed) ReadyForNextToil();
                }
            };
            clean.defaultCompleteMode = ToilCompleteMode.Never;
            clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
            clean.WithProgressBar(TargetIndex.A, () => totalCleaningWorkDone / totalCleaningWorkRequired, true);
            clean.PlaySustainerOrSound(() =>
            {
                var def = Filth?.def;
                return def?.filth != null && !def.filth.cleaningSound.NullOrUndefined() ? def.filth.cleaningSound : SoundDefOf.Interact_CleanFilth;
            });
            clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, next);
            clean.JumpIf(() => job.GetTarget(TargetIndex.A).Thing?.Destroyed ?? true, next);
            yield return clean;
            yield return Toils_Jump.Jump(next);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cleaningWorkDone, "cleaningWorkDone");
            Scribe_Values.Look(ref totalCleaningWorkDone, "totalCleaningWorkDone");
            Scribe_Values.Look(ref totalCleaningWorkRequired, "totalCleaningWorkRequired");
        }
    }
}

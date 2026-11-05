using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class TaskDefOf
    {
        public static JobDef Rimfolk_TaskPatrol;

        static TaskDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TaskDefOf));
        }
    }

    /// <summary>The marshal's patrol: a few cells along the edges of the colony's land, the nearest first, then round.</summary>
    public static class TaskJobs
    {
        /// <summary>
        /// Up to TaskMath.PatrolPoints standable cells a little in from the edges the marshal can reach (one near each side,
        /// in walking order from where they stand), or fewer when the land allows fewer.
        /// </summary>
        public static List<IntVec3> PatrolCells(Map map, Pawn p)
        {
            var list = new List<IntVec3>();
            if (map == null || p == null) return list;
            int inset = TaskMath.EdgeIn;
            var size = map.Size;
            var sides = new[]
            {
                new IntVec3(size.x / 2, 0, inset), new IntVec3(size.x - 1 - inset, 0, size.z / 2), new IntVec3(size.x / 2, 0, size.z - 1 - inset), new IntVec3(inset, 0, size.z / 2)
            };
            foreach (var near in sides)
            {
                if (list.Count >= TaskMath.PatrolPoints) break;
                if (CellFinder.TryFindRandomCellNear(near, map, 10, c => c.Standable(map) && !c.Fogged(map) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                    list.Add(cell);
            }
            // Walked in order: from the nearest one, round the land.
            if (list.Count > 1)
            {
                int start = list.IndexOf(list.OrderBy(c => c.DistanceToSquared(p.Position)).First());
                list = list.Skip(start).Concat(list.Take(start)).ToList();
            }
            return list;
        }
    }

    /// <summary>The marshal walks the edges at dawn (the job's queue of cells, in turn), and the step is noted (CouncilTasks.Patrolled: a word when all was quiet).</summary>
    public class JobDriver_TaskPatrol : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Drafted || pawn.Downed);
            for (int i = 0; i < TaskMath.PatrolPoints; i++)
            {
                int leg = i;
                var go = ToilMaker.MakeToil("PatrolLeg");
                go.initAction = () =>
                {
                    var q = job.targetQueueA;
                    if (q == null || leg >= q.Count || !q[leg].Cell.IsValid)
                    {
                        ReadyForNextToil();
                        return;
                    }
                    pawn.pather.StartPath(q[leg].Cell, PathEndMode.OnCell);
                };
                go.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                yield return go;
                var look = ToilsWait(90);
                yield return look;
            }
            var done = ToilMaker.MakeToil("PatrolDone");
            done.initAction = () => CouncilTasks.Patrolled(pawn);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        /// <summary>A short look about at each point.</summary>
        private Toil ToilsWait(int ticks)
        {
            var t = ToilMaker.MakeToil("PatrolLook");
            t.defaultCompleteMode = ToilCompleteMode.Delay;
            t.defaultDuration = ticks;
            return t;
        }
    }
}

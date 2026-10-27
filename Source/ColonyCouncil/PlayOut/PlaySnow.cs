using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A snowman an answer says someone built (<see cref="DilemmaEffect_Snowman"/>): a little way into what the answer
    /// set going (the snow day out in the open, the shoveling), they go off to a patch of deep enough snow close by and
    /// build one with the game's own snowman job, and it stands there after. With no snow deep enough near them, there's
    /// no snowman.
    /// </summary>
    public static class PlaySnow
    {
        private static JobDef build;
        private static JobDef Build => build ??= DefDatabase<JobDef>.GetNamedSilentFail("BuildSnowman");

        /// <summary>Snow at least this deep to build in (the game's own snowmen want about as much).</summary>
        public const float Depth = 0.5f;

        /// <summary>Each look while it runs: once it's a little way in, the builder goes to build it.</summary>
        public static void Step(PlayOut p, int now)
        {
            var x = p.snowman;
            if (x == null || x.Dead)
            {
                p.snowmanDone = true;
                return;
            }
            if (p.started < 0 || now - p.started < p.durationTicks / 6) return;
            if (!x.Spawned || x.Map != p.map || x.Downed || x.InMentalState || x.Drafted || !x.Awake() || x.CurJobDef == Build) return;
            p.snowmanDone = true;
            if (Build == null) return;
            try
            {
                var cell = Cell(x);
                if (!cell.IsValid) return;
                var job = JobMaker.MakeJob(Build, cell);
                // together: they build it there and then, while the others are out with them.
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out snowman", e);
            }
        }

        /// <summary>Deep enough snow close by them, out in the open, with nothing on it.</summary>
        private static IntVec3 Cell(Pawn x)
        {
            var map = x.Map;
            foreach (var c in GenRadial.RadialCellsAround(x.Position, 10f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.Fogged(map)) continue;
                if (map.snowGrid.GetDepth(c) < Depth || c.IsForbidden(x) || !x.CanReserveAndReach(c, PathEndMode.Touch, Danger.None)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }
    }

    /// <summary>
    /// The one the words say built a snowman (A unless given) builds one a little way into what the answer set going:
    /// its gathering, work party or walk (PlaySnow.cs). With nothing set going (answers on the map off), nothing.
    /// </summary>
    public class DilemmaEffect_Snowman : DilemmaEffect
    {
        public PlayWho who = PlayWho.A;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: only marks who builds it; the building is the game's own job, during the answer's play-out.
            var x = PlayOuts.Who(who, ctx);
            if (x == null || x.Dead || !PlayOuts.OurPerson(x)) return;
            var p = PlayOuts.Collected().FirstOrDefault(o => o.state != PlayState.Done && (o.IsGathering || o.IsGroup));
            if (p != null) p.snowman = x;
        }
    }
}

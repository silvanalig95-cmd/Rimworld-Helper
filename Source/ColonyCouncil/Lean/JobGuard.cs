using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A job of no kind is never started, and one already stuck on a pawn is cleared when a game loads (the player, 20
    /// November 2026: after a medal at the gate "the whole map screen turned gray ... while I was unpausing the game. Once I
    /// paused, the map just returned to normal", then "there's an invisible pawn here"). Three of Rimfolk's lists of its
    /// own jobs (Harm/NightWalks.cs, HarmSix.cs, HarmSeven.cs) lacked the game's [DefOf] mark, so their jobs were made with
    /// no kind: the game took the job but couldn't make its driver. The pawn was then drawn as its name only (drawing it
    /// threw, and the game skips a pawn it can't draw), and every frame time passed its facing read the missing driver and
    /// threw before the map was drawn, so the map stayed grey until the game was paused. The lists now carry the mark; this
    /// guard keeps any such job, Rimfolk's or another mod's, from being started, and mends a save that has one.
    /// </summary>
    public static class JobGuard
    {
        /// <summary>
        /// Pawns on the maps with a job of no kind, or a job without its driver, sent back to choosing their next job, and jobs
        /// of no kind taken out of their queues; how many pawns were mended.
        /// </summary>
        public static int Repair()
        {
            int n = 0;
            var maps = Find.Maps;
            if (maps == null) return 0;
            foreach (var map in maps)
            {
                var pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null) continue;
                foreach (var p in pawns.ToList())
                {
                    var jobs = p?.jobs;
                    if (jobs == null) continue;
                    try
                    {
                        bool mended = false;
                        if (jobs.curJob != null && (jobs.curJob.def == null || jobs.curDriver == null))
                        {
                            jobs.curJob = null;
                            jobs.curDriver = null;
                            mended = true;
                        }
                        if (jobs.jobQueue != null && jobs.jobQueue.Any(q => q?.job == null || q.job.def == null))
                        {
                            jobs.jobQueue.RemoveAll(p, j => j == null || j.def == null);
                            mended = true;
                        }
                        if (mended)
                        {
                            n++;
                            // A visitor left so without a group of theirs to leave with goes home now, as their visit would have ended.
                            if (p.Faction != null && !p.Faction.IsPlayer && !p.IsPrisoner && !p.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike
                                && p.GetLord() == null)
                                Arrivals.SendAway(new[] { p });
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("mending a pawn's job", e);
                    }
                }
            }
            if (n > 0) Log.Message("[Rimfolk] " + n + " pawn(s) had a job of no kind (a fault Rimfolk has since fixed) and were sent back to choosing their next job.");
            return n;
        }
    }

    /// <summary>A job of no kind is never started: it would leave the pawn without a driver (Lean/JobGuard.cs).</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_JobGuard
    {
        private static bool told;

        public static bool Prefix(Job __0, Pawn ___pawn)
        {
            if (__0 == null || __0.def != null) return true;
            if (!told)
            {
                told = true;
                Log.Warning("[Rimfolk] a job of no kind was refused for " + (___pawn?.LabelShort ?? "a pawn") + " (it would have left them without a driver; told once): " + Environment.StackTrace);
            }
            return false;
        }
    }
}

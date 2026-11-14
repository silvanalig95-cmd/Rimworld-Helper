using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Pawns taken out of their lord (the game's group that gives them their duties), and the lord ended once that leaves
    /// nobody in it. The game ends a lord that can't go on without pawns when it loses one (killed, gone off the map, gone
    /// over to another side), never when one is moved out of it with Lord.RemovePawn; so a group Rimfolk emptied by moving
    /// its people into another one stayed in the save and ticked for good. Every place Rimfolk moves pawns out of a lord
    /// does it here, and a lord left empty is ended the way the game ends it (LordManager.RemoveLord, when Lord.ShouldExist
    /// says so). Rimfolk's own lords left empty by older versions are ended on loading and once a day (<see cref="Sweep"/>).
    /// </summary>
    public static class Lords
    {
        /// <summary>Out of whatever lord they're in; that lord ended if nobody is left in it.</summary>
        public static void Leave(Pawn p) => Leave(p?.GetLord(), p);

        /// <summary>Out of this lord; it is ended if nobody is left in it.</summary>
        public static void Leave(Lord lord, Pawn p)
        {
            if (lord == null || p == null) return;
            lord.RemovePawn(p);
            EndIfEmpty(lord);
        }

        /// <summary>Each out of their own lord; each lord left with nobody is ended once they are all out.</summary>
        public static void Leave(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            var left = new List<Lord>();
            foreach (var p in pawns.ToList())
            {
                var lord = p?.GetLord();
                if (lord == null) continue;
                lord.RemovePawn(p);
                if (!left.Contains(lord)) left.Add(lord);
            }
            foreach (var lord in left) EndIfEmpty(lord);
        }

        /// <summary>All of them out of this lord; it is ended if nobody is left in it.</summary>
        public static void Leave(Lord lord, IEnumerable<Pawn> pawns)
        {
            if (lord == null || pawns == null) return;
            foreach (var p in pawns.ToList())
                if (p != null) lord.RemovePawn(p);
            EndIfEmpty(lord);
        }

        /// <summary>
        /// Ended the game's way if nobody is left in it and the game would end it for that (<see cref="LordsMath.ShouldExist"/>);
        /// a lord already ended, or one that may go on empty (a gathering others can still join), is left. True if it was ended.
        /// </summary>
        public static bool EndIfEmpty(Lord lord)
        {
            var manager = lord?.lordManager;
            var job = lord?.LordJob;
            if (manager == null || job == null || !manager.lords.Contains(lord)) return false;
            try
            {
                if (LordsMath.ShouldExist(lord.ownedPawns.Count, lord.ownedBuildings.Count, job.ShouldExistWithoutPawns, job.KeepExistingWhileHasAnyBuilding)) return false;
                manager.RemoveLord(lord);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("an empty group ended", e);
                return false;
            }
        }

        /// <summary>
        /// Rimfolk's own lords on any map with nobody in them, no building of their own and a job that can't go on without
        /// pawns (<see cref="LordsMath.Swept"/>), left by older versions that moved people out without ending the group:
        /// ended the game's way, and named in the log. The game's and other mods' lords are left as they are. How many were
        /// ended.
        /// </summary>
        public static int Sweep()
        {
            var ended = new List<string>();
            foreach (var map in Find.Maps.ToList())
            {
                var manager = map?.lordManager;
                if (manager?.lords == null) continue;
                foreach (var lord in manager.lords.ToList())
                {
                    try
                    {
                        var job = lord?.LordJob;
                        if (job == null || !LordsMath.Swept(job.GetType().Namespace, lord.ownedPawns.Count, lord.ownedBuildings.Count, job.ShouldExistWithoutPawns)) continue;
                        string name = job.GetType().Name + " (" + lord.GetUniqueLoadID() + ")";
                        manager.RemoveLord(lord);
                        ended.Add(name);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("an empty group left in the save ended", e);
                    }
                }
            }
            if (ended.Count > 0) Log.Message("[Rimfolk] Ended " + ended.Count + " of Rimfolk's groups left with nobody in them: " + string.Join(", ", ended) + ".");
            return ended.Count;
        }
    }
}

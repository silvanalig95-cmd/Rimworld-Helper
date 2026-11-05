using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What an older save still holds of the council's tasks and the keeper of secrets, dropped by the player's decision
    /// (5 November 2026). The tasks themselves ("tasks" on a home's part of the diplomacy book), the marshal's drill, a
    /// vassal's dues season and the setting are left unread; a keeper's seat is let go with its holder (Offices.ExposeData)
    /// and its race in an election (ElectionRace.ExposeData); a letter of the council's business not yet read is called
    /// off (NeighbourLetters.Step); a sounding out under way ends (JobDriver_OutsiderTalk); an officer's visit planned or
    /// on the road goes on as the colony's own journey. The secrets the keeper learned are Secret ties on the ledger:
    /// nothing reads or presses them now, so they are let go at the day's look, a pressed one too (called in, it has no end
    /// of its own). The history keeps what was written.
    /// </summary>
    public static class Retired
    {
        /// <summary>Where the keeper's Secret ties came from (its origin on the ledger).</summary>
        public const string KeeperOrigin = "keeper";

        public static void Daily()
        {
            foreach (var t in WorldLedger.LiveTies(Tie.Kind.Secret))
                if (t.origin == KeeperOrigin)
                    WorldLedger.Set(t, Tie.State.Ended);
        }
    }

    /// <summary>
    /// An older save can still hold the marshal's dawn patrol under way, and a keeper's seat with its hediff: the game
    /// reads both by name, and a name it can't find is an error in the log. So the job and the hediff stay as defs
    /// (Defs/Misc/Rimfolk_Retired.xml) with these stand-ins, which end them at once and quietly.
    /// </summary>
    public class JobDriver_TaskPatrol : JobDriver
    {
        /// <summary>The patrol had four legs with a look about at each, then its end: a saved place in it is still found.</summary>
        private const int OldToils = 9;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Ended at its first look, wherever the save left it (the walk to a leg's cell, a look about).
            this.FailOn(() => true);
            for (int i = 0; i < OldToils; i++)
            {
                var t = ToilMaker.MakeToil("Retired");
                t.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return t;
            }
        }
    }

    /// <summary>A hediff of a retired def: gone at the pawn's next health tick, with nothing said.</summary>
    public class HediffComp_Retired : HediffComp
    {
        public override bool CompShouldRemove => true;
    }
}

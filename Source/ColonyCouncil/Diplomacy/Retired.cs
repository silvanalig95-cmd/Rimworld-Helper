using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's tasks and the keeper of secrets were taken out of the game (the player's decision, 5 November 2026).
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

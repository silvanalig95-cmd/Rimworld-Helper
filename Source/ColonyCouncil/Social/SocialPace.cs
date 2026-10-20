using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How often Rimfolk's own exchanges come for one colonist, across all of them: a moment (the groups' and the
    /// reputation's moments are moments too), a conversation (Social/Talk/) and a story passed on (Social/Gossip/). Each
    /// keeps its own wait (a moment six hours a starter; a talk four hours a starter and ten a pair; a story ten hours a
    /// teller and two days a pair). On top of those, someone who has just started one waits an hour before starting
    /// another of any kind, and nobody starts one with someone still in the middle of a conversation, so the game's own
    /// chitchat stays the bulk of what's said and bubbles don't pile up over one head. Not saved: after a load anyone
    /// may start again. With "Personalities and social life" off this changes nothing (the moments keep their own wait).
    /// </summary>
    public static class SocialPace
    {
        /// <summary>The wait after starting one of Rimfolk's exchanges before the same colonist starts another, of any kind.</summary>
        public const int StarterGapTicks = GenDate.TicksPerHour;

        private static readonly Dictionary<int, int> lastStarted = new Dictionary<int, int>();

        public static void Reset() => lastStarted.Clear();

        /// <summary>Whether <paramref name="starter"/> may start one of Rimfolk's exchanges with <paramref name="other"/> now.</summary>
        public static bool Ready(Pawn starter, Pawn other)
        {
            if (!Persona.On || starter == null || Find.TickManager == null) return true;
            int now = Find.TickManager.TicksGame;
            // (A time in the future means an older save was loaded: that wait is over.)
            if (lastStarted.TryGetValue(starter.thingIDNumber, out int t) && t <= now && now - t < StarterGapTicks) return false;
            var talking = TalkTracker.Instance?.exchanges;
            return talking == null || talking.Count == 0 || (!talking.Busy(starter) && !talking.Busy(other));
        }

        /// <summary>One of Rimfolk's exchanges was started by <paramref name="starter"/>.</summary>
        public static void Started(Pawn starter)
        {
            if (!Persona.On || starter == null || Find.TickManager == null) return;
            lastStarted[starter.thingIDNumber] = Find.TickManager.TicksGame;
            if (lastStarted.Count > 500) lastStarted.Clear();
        }
    }
}

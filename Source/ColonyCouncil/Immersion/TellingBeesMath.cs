using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Telling the bees (C23 of docs/content-update-proposals.md), its pure rules (Immersion/TellingBees.cs; Tools/Tests reads
    /// the same): Alpha Bees' beehouses by name, who tends them most, when the bees are told, how many hives a round takes,
    /// and how long the news waits for someone to tell it.
    /// </summary>
    public static class TellingBeesMath
    {
        /// <summary>Alpha Bees' beehouses (sarg.rimbees), by name: never comes without them.</summary>
        public static readonly string[] Hives = { "RB_Beehouse", "RB_AdvancedBeehouse" };

        /// <summary>Alpha Bees' own work of tending the hives.</summary>
        public const string BeeWork = "RB_BeekeepingWork";

        /// <summary>The most hives one round takes, and how long the teller stands at each (ticks).</summary>
        public const int MostHives = 4, StandTicks = 200;

        /// <summary>The news waits this many days for someone to tell it, then is let go (no penalty if nobody goes).</summary>
        public const float WaitDays = 3f;

        /// <summary>The newest pieces of news kept waiting, and the newcomers already told of.</summary>
        public const int MaxWaiting = 6, ToldKept = 30;

        /// <summary>The bees are told in the day's light hours, on a quiet day.</summary>
        public static bool Hour(int hour) => hour >= 7 && hour < 19;

        /// <summary>
        /// Who tells them: the one who tends the hives most, by their beekeeping priority (1 first; 0 or below is never), then by
        /// their skill with animals; the index into the lists, or -1 for nobody.
        /// </summary>
        public static int Keeper(IList<int> priorities, IList<int> skills)
        {
            if (priorities == null || skills == null) return -1;
            int best = -1;
            for (int i = 0; i < priorities.Count && i < skills.Count; i++)
            {
                if (priorities[i] <= 0) continue;
                if (best < 0 || priorities[i] < priorities[best] || priorities[i] == priorities[best] && skills[i] > skills[best]) best = i;
            }
            return best;
        }

        /// <summary>A piece of news still waiting (the days since it came within the wait).</summary>
        public static bool StillWaiting(float daysSince) => !float.IsNaN(daysSince) && daysSince >= 0f && daysSince <= WaitDays;
    }
}

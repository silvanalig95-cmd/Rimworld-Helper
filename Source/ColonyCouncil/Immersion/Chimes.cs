using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A small bell when a day on the colony calendar comes round: the game's own one-shot bell, a moment after the day's
    /// letter arrives (so the two don't sound over each other), while the home map is being looked at. Nothing is saved:
    /// after a load, the letters already there have rung.
    /// </summary>
    internal sealed class Chimes
    {
        /// <summary>Seconds after the letter's own sound.</summary>
        private const float Delay = 1.2f;
        /// <summary>A bell this long after the letter would sound like something else: it passes.</summary>
        private const float LateAfter = 8f;

        /// <summary>The arrival tick of the last calendar letter rung for (or let pass); -1 until the first pulse.</summary>
        private int seenTick = -1;

        public void Pulse(Map map, int now)
        {
            if (seenTick < 0)
            {
                seenTick = now;
                return;
            }
            var letters = Find.LetterStack?.LettersListForReading;
            if (letters == null) return;
            for (int i = letters.Count - 1; i >= 0; i--)
            {
                if (!(letters[i] is ChoiceLetter_CalendarDay day) || day.arrivalTick <= seenTick) continue;
                // A moment after it arrived; if the home map isn't in view by then (or it's long past), it passes without a bell.
                float since = Time.time - day.arrivalTime;
                if (since < Delay && since >= 0f) return;
                seenTick = day.arrivalTick;
                if (since <= LateAfter && map != null && map.IsPlayerHome) SoundDefOf.TinyBell?.PlayOneShotOnCamera(map);
                return;
            }
        }
    }
}

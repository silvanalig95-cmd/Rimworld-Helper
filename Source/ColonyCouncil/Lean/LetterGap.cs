using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's questions keep a little distance from each other. Its many systems each keep their own rhythm, and
    /// left alone two of them now and then ask on the same morning (Tools/Balance found about one question in five
    /// coming within half a day of another). A question that can wait (a dilemma, an event that asks something, a world
    /// event's first question) waits until <see cref="Hours"/> have passed since Rimfolk's last one. Questions tied to a
    /// day or a place (a calendar day, a meeting's vote, a camp at the gate, a world event already under way) come when
    /// they come, and count for the gap. Wishes and letters that only tell aren't questions and never wait.
    /// </summary>
    public static class LetterGap
    {
        /// <summary>The fewest in-game hours between two of Rimfolk's questions that can wait.</summary>
        public const int Hours = 12;

        private static int last = -999999;

        /// <summary>A choice letter from Rimfolk: one of its questions.</summary>
        public static bool IsQuestion(Letter l) =>
            l is ChoiceLetter && !(l is ChoiceLetter_News) && !(l is ChoiceLetter_Welcome) && l.GetType().Assembly == typeof(LetterGap).Assembly;

        /// <summary>One of Rimfolk's questions has just arrived.</summary>
        public static void Note()
        {
            if (Find.TickManager != null) last = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Whether a question that can wait may come now: none of Rimfolk's questions came in the last <see cref="Hours"/>
        /// (one still waiting in the letter stack counts too, so this holds even without the game hook).
        /// </summary>
        public static bool Open
        {
            get
            {
                if (Find.TickManager == null) return true;
                int now = Find.TickManager.TicksGame;
                int gap = Hours * GenDate.TicksPerHour;
                // A time in the future is another game's (one loaded since): that wait is over.
                if (last <= now && now - last < gap) return false;
                try
                {
                    var letters = Find.LetterStack?.LettersListForReading;
                    if (letters == null) return true;
                    for (int i = 0; i < letters.Count; i++)
                    {
                        var l = letters[i];
                        if (l != null && l.arrivalTick <= now && now - l.arrivalTick < gap && IsQuestion(l)) return false;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("letter gap", e);
                }
                return true;
            }
        }
    }

    /// <summary>Notes each of Rimfolk's questions as it arrives, for <see cref="LetterGap"/>.</summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_LetterGap
    {
        public static void Postfix(Letter let)
        {
            try
            {
                if (let != null && LetterGap.IsQuestion(let)) LetterGap.Note();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter gap hook", e);
            }
        }
    }
}
